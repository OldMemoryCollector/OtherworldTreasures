using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Relics;
using OtherworldTreasures.Scripts.Relics;

namespace OtherworldTreasures.Scripts.TimeCloth;

// 单个生物"回合开始时"的状态快照
public sealed class CreatureSnapshot
{
    public int Hp;
    public int Block;
    // power 模型 Id.Entry → 层数
    public readonly Dictionary<string, int> PowerAmounts = new();
    // 敌人的当前意图（MoveState 的 StateId）
    public string? MoveStateId;
}

// 时光布的状态存取
// - 每个玩家回合开始时记录一次快照，保留"上一回合"与"本回合"两份
// - 回溯：还原到"上一回合开始时"
// - 加速：敌人只推进意图；自己只结算状态；遗物计数不随回合自增（无操作）
public static class TimeClothState
{
    // Creature.CurrentHp / Block 是私有 setter，直接改会绕开 Damage/Heal 的修正与特效，
    // 正是"精确还原"需要的；走反射调用 setter 仍会触发 CurrentHpChanged / BlockChanged 事件刷新 UI。
    private static readonly PropertyInfo? HpProperty =
        typeof(Creature).GetProperty("CurrentHp", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private static readonly PropertyInfo? BlockProperty =
        typeof(Creature).GetProperty("Block", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private static Dictionary<Creature, CreatureSnapshot>? _previousTurn;
    private static Dictionary<Creature, CreatureSnapshot>? _currentTurn;
    private static Dictionary<Type, int>? _previousRelicCounters;
    private static Dictionary<Type, int>? _currentRelicCounters;

    // 由 TimeClothCard 在两步选择之间传递"回溯 or 加速"
    public static bool PendingIsRewind;

    // 是否正在进行【时光布】的目标选择：此时点击遗物格应视为"选目标"，
    // 而不是原版的"打开遗物介绍"（见 GamePatches 里对 NRelicInventory.OnRelicClicked 的拦截）
    public static bool IsTargeting;

    public static void Clear()
    {
        _previousTurn = null;
        _currentTurn = null;
        _previousRelicCounters = null;
        _currentRelicCounters = null;
        PendingIsRewind = false;
        IsTargeting = false;
    }

    // 玩家回合开始：把"本回合"滚动为"上一回合"，再记录新快照
    public static void CaptureAtPlayerTurnStart(Player owner)
    {
        var combatState = owner?.Creature?.CombatState;
        if (combatState == null)
        {
            return;
        }

        _previousTurn = _currentTurn;
        var turn = new Dictionary<Creature, CreatureSnapshot>();
        foreach (var creature in combatState.Creatures)
        {
            if (creature == null || !creature.IsAlive)
            {
                continue;
            }
            turn[creature] = Capture(creature);
        }
        _currentTurn = turn;

        _previousRelicCounters = _currentRelicCounters;
        _currentRelicCounters = CaptureRelicCounters(owner);
    }

    // === 回溯：还原到"上一回合开始时" ===
    public static async Task Rewind(Creature target)
    {
        if (target == null || !target.IsAlive)
        {
            return;
        }
        if (_previousTurn == null || !_previousTurn.TryGetValue(target, out var snapshot))
        {
            Entry.Logger.Info("[TimeCloth] Rewind skipped: no previous-turn snapshot for target");
            return;
        }

        SetBlock(target, snapshot.Block);
        SetHp(target, snapshot.Hp);
        await RestorePowers(target, snapshot);
        RestoreIntent(target, snapshot);
        Entry.Logger.Info($"[TimeCloth] Rewind applied: hp={snapshot.Hp} block={snapshot.Block} powers={snapshot.PowerAmounts.Count}");
    }

    // === 加速·敌人：只把意图推进到下一个（不执行行动） ===
    public static void AdvanceIntent(Creature target)
    {
        var monster = target?.Monster;
        if (monster == null)
        {
            return;
        }

        var current = monster.NextMove;
        if (current?.FollowUpState is MoveState next)
        {
            monster.SetMoveImmediate(next, forceTransition: true);
            Entry.Logger.Info($"[TimeCloth] Intent advanced: {current.Id} -> {next.Id}");
            return;
        }

        // 后继是分支状态（不确定）→ 按原版规则重新抽一个意图
        var combatState = target!.CombatState;
        if (combatState != null)
        {
            monster.RollMove(combatState.PlayerCreatures);
            monster.SetMoveImmediate(monster.NextMove, forceTransition: true);
            Entry.Logger.Info($"[TimeCloth] Intent advanced (rolled): {monster.NextMove.Id}");
        }
    }

    // === 加速·自己：清空格挡 + 结算"每回合"状态 ===
    public static async Task AdvanceStatus(Creature target)
    {
        if (target == null || !target.IsAlive)
        {
            return;
        }
        SetBlock(target, 0);

        foreach (var power in target.Powers.ToList())
        {
            // 中毒：直接触发一次结算
            if (power is PoisonPower poison)
            {
                await poison.Trigger();
                continue;
            }
            // 其余"可计数的负面状态"各减 1 层（近似经历一个回合）
            if (power.StackType != PowerStackType.Counter || power.Type != PowerType.Debuff)
            {
                continue;
            }
            if (power.Amount <= 1)
            {
                await PowerCmd.Remove(power);
            }
            else
            {
                await PowerCmd.Decrement(power);
            }
        }
        Entry.Logger.Info("[TimeCloth] Self status settled (block cleared, per-turn states ticked)");
    }

    // === 原版目标选择：允许点选"生物（自己/敌人）"与"带回合计数的遗物图标" ===
    // - 回溯：我们自己的计数遗物（ITimeClothCounter）+ 原版回合计数遗物
    // - 加速：只有原版"回合计数"遗物有意义（其他计数的推进没有回合含义）
    public static bool AllowTargetNode(Node node)
    {
        switch (node)
        {
            case NCreature:
                return true;
            case NRelicInventoryHolder holder:
                var relic = holder.Relic?.Model;
                if (relic == null)
                {
                    return false;
                }
                if (FindTurnCounterProperty(relic) != null)
                {
                    return true;
                }
                return PendingIsRewind && relic is ITimeClothCounter;
            default:
                return false;
        }
    }

    // 根据点中的节点结算
    public static async Task ApplyToNode(Node node, bool isRewind)
    {
        switch (node)
        {
            case NCreature creatureNode when creatureNode.Entity != null:
                var creature = creatureNode.Entity;
                if (creature.IsPlayer)
                {
                    if (isRewind)
                    {
                        await Rewind(creature);
                    }
                    else
                    {
                        await AdvanceStatus(creature);
                    }
                }
                else if (isRewind)
                {
                    await Rewind(creature);
                }
                else
                {
                    AdvanceIntent(creature);
                }
                break;

            case NRelicInventoryHolder holder:
                var relic = holder.Relic?.Model;
                if (relic == null)
                {
                    break;
                }
                if (isRewind)
                {
                    RewindRelicCounter(relic);
                }
                else
                {
                    AdvanceRelicTurnCounter(relic);
                }
                break;
        }
    }

    // === 遗物计数 ===
    // 原版"回合计数"遗物（开心小花/钟摆/花粉核心…）的计数是一个公开的 [SavedProperty] int 属性，
    // 名字形如 TurnsSeen；写入会走它自己的 setter（内部 AssertMutable + InvokeDisplayAmountChanged）。
    // 苦无/笔尖那种攻击计数是私有字段（AttacksPlayed），不会命中这里，因此天然被排除。
    private static readonly Dictionary<Type, PropertyInfo?> _turnCounterPropertyCache = new();

    private static PropertyInfo? FindTurnCounterProperty(RelicModel relic)
    {
        var type = relic.GetType();
        if (_turnCounterPropertyCache.TryGetValue(type, out var cached))
        {
            return cached;
        }
        var property = type
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(p => p.PropertyType == typeof(int) && p.CanRead && p.CanWrite && p.Name.Contains("Turn"));
        _turnCounterPropertyCache[type] = property;
        return property;
    }

    // 回溯：把计数还原到"上一回合开始时"
    public static void RewindRelicCounter(RelicModel relic)
    {
        if (_previousRelicCounters == null || !_previousRelicCounters.TryGetValue(relic.GetType(), out int value))
        {
            Entry.Logger.Info("[TimeCloth] Rewind skipped: no previous counter for this relic");
            return;
        }

        if (relic is ITimeClothCounter counter)
        {
            counter.TimeClothCounter = value;
            counter.RefreshTimeClothCounterUi();
            Entry.Logger.Info($"[TimeCloth] Relic counter rewound: {relic.GetType().Name} = {value}");
            return;
        }

        var property = FindTurnCounterProperty(relic);
        if (property == null)
        {
            Entry.Logger.Info($"[TimeCloth] {relic.GetType().Name} has no rewindable counter");
            return;
        }
        property.SetValue(relic, value);
        Entry.Logger.Info($"[TimeCloth] Turn counter rewound: {relic.GetType().Name}.{property.Name} = {value}");
    }

    // 加速：回合计数 +1（已处于"即将触发"状态时不再推进，否则会跳过触发条件）
    public static void AdvanceRelicTurnCounter(RelicModel relic)
    {
        var property = FindTurnCounterProperty(relic);
        if (property == null)
        {
            Entry.Logger.Info($"[TimeCloth] {relic.GetType().Name} has no turn counter to advance");
            return;
        }
        if (relic.Status == RelicStatus.Active)
        {
            Entry.Logger.Info($"[TimeCloth] {relic.GetType().Name} is already about to trigger, not advancing");
            return;
        }
        if (property.GetValue(relic) is not int current)
        {
            return;
        }
        property.SetValue(relic, current + 1);
        Entry.Logger.Info($"[TimeCloth] Turn counter advanced: {relic.GetType().Name}.{property.Name} {current} -> {current + 1}");
    }

    // === 内部实现 ===
    private static CreatureSnapshot Capture(Creature creature)
    {
        var snapshot = new CreatureSnapshot
        {
            Hp = creature.CurrentHp,
            Block = creature.Block
        };
        foreach (var power in creature.Powers)
        {
            snapshot.PowerAmounts[power.Id.Entry] = power.Amount;
        }
        if (creature.Monster != null)
        {
            snapshot.MoveStateId = creature.Monster.NextMove?.Id;
        }
        return snapshot;
    }

    private static Dictionary<Type, int> CaptureRelicCounters(Player owner)
    {
        var result = new Dictionary<Type, int>();
        if (owner == null)
        {
            return result;
        }
        foreach (var relic in owner.Relics)
        {
            // 我们自己的计数遗物（计数是 static，走接口）
            if (relic is ITimeClothCounter counter)
            {
                result[relic.GetType()] = counter.TimeClothCounter;
                continue;
            }
            // 原版回合计数遗物（如开心小花的 TurnsSeen）
            var property = FindTurnCounterProperty(relic);
            if (property?.GetValue(relic) is int value)
            {
                result[relic.GetType()] = value;
            }
        }
        return result;
    }

    private static async Task RestorePowers(Creature target, CreatureSnapshot snapshot)
    {
        // 现存的、快照里没有的 → 移除
        foreach (var power in target.Powers.ToList())
        {
            if (!snapshot.PowerAmounts.ContainsKey(power.Id.Entry))
            {
                await PowerCmd.Remove(power);
            }
        }

        // 快照里的 → 精确还原层数；已经丢掉的 → 重新施加
        foreach (var kv in snapshot.PowerAmounts)
        {
            var existing = target.Powers.FirstOrDefault(p => p.Id.Entry == kv.Key);
            if (existing != null)
            {
                existing.SetAmount(kv.Value, silent: true);
                continue;
            }
            var canonical = ModelDb.AllPowers.FirstOrDefault(p => p.Id.Entry == kv.Key);
            if (canonical == null)
            {
                continue;
            }
            await PowerCmd.Apply(
                new ThrowingPlayerChoiceContext(), canonical.ToMutable(), target, kv.Value, null, null, silent: true);
        }
    }

    private static void RestoreIntent(Creature target, CreatureSnapshot snapshot)
    {
        var monster = target.Monster;
        if (monster == null || snapshot.MoveStateId == null)
        {
            return;
        }
        var machine = monster.MoveStateMachine;
        if (machine == null)
        {
            return;
        }
        if (machine.States.TryGetValue(snapshot.MoveStateId, out var state) && state is MoveState move)
        {
            monster.SetMoveImmediate(move, forceTransition: true);
        }
    }

    private static void SetHp(Creature creature, int value)
    {
        if (HpProperty == null)
        {
            return;
        }
        HpProperty.SetValue(creature, Math.Clamp(value, 0, creature.MaxHp));
    }

    private static void SetBlock(Creature creature, int value)
    {
        if (BlockProperty == null)
        {
            return;
        }
        BlockProperty.SetValue(creature, Math.Max(0, value));
    }
}
