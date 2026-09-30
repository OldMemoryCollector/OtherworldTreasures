using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using OtherworldTreasures.Scripts.Monsters;
using OtherworldTreasures.Scripts.Powers;
using STS2RitsuLib;
using STS2RitsuLib.Combat.Ui.ExtraCornerAmountLabels;
using STS2RitsuLib.Interactions.RightClick;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Relics;

// 桃太郎丸子（哆啦A梦的道具之一）：
// 拥有 3 颗丸子。战斗中右键点击遗物，选择一名当前生命不超过 10、最大生命不超过 80 的非首领敌人直接收服。
// 收服后：回满血、生命上限变为原来的 80%、成为跨战斗的永久随从、使用原本的招式与意图、无法被治疗；随从死亡后永久消失。
[RegisterRelic(typeof(SharedRelicPool))]
public class MomotaroDumplings : ModRelicTemplate,
    IModRightClickableRelic, IDoraemonItem,
    IRelicExtraIconAmountLabelSpecsProvider, IRelicExtraIconAmountLabelsChangeSource
{
    private const int InitialDumplings = 3;

    // 收服后随从的血上限倍率（比原怪低 20%）
    private const decimal TameHpRatio = 0.8m;

    // 剩余丸子数（跨战斗 static + 存档双保险，同孢子囊）
    private static int s_remaining = InitialDumplings;
    private int _savedRemaining = InitialDumplings;
    private string _savedPetsData = string.Empty;

    // 角标字号：RitsuLib 的角标字号继承自原版数量标签（很大），必须用 RichText 指定字号，
    // 与库拉的骰子保持一致（否则数字巨大）
    private const int BadgeFontSize = 16;

    [SavedProperty]
    public int SavedRemaining
    {
        get => _savedRemaining;
        set
        {
            AssertMutable();
            _savedRemaining = value;
        }
    }

    // 随从记录编码成一条字符串：[SavedProperty] 不支持 List<string>，
    // 用它会让存档序列化抛 JsonException（表现为获得遗物后黑屏）
    [SavedProperty]
    public string SavedPetsData
    {
        get => _savedPetsData;
        set
        {
            AssertMutable();
            _savedPetsData = value ?? string.Empty;
        }
    }

    private MomotaroDumplings? LiveRelic => Owner?.Relics?.OfType<MomotaroDumplings>().FirstOrDefault();

    public override RelicRarity Rarity => ModRelicRarity.Doraemon;

    public override int MerchantCost => 999999999;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://OtherworldTreasures/images/relics/Momotaro_Dumplings.png",
        IconOutlinePath: "res://OtherworldTreasures/images/relics/Momotaro_Dumplings.png",
        BigIconPath: "res://OtherworldTreasures/images/relics/Momotaro_Dumplings.png"
    );

    // === 剩余丸子角标 ===
    public event Action? RelicExtraIconAmountLabelsInvalidated;

    private void RefreshBadge()
    {
        var live = LiveRelic;
        if (live != null && !ReferenceEquals(live, this))
        {
            live.RelicExtraIconAmountLabelsInvalidated?.Invoke();
        }
        else
        {
            RelicExtraIconAmountLabelsInvalidated?.Invoke();
        }
    }

    public IReadOnlyList<ExtraIconAmountLabelSpec> GetRelicExtraIconAmountLabelSpecs()
    {
        // 局外（图鉴/收藏）不显示角标，避免把上一局的丸数带进图鉴
        if (!(RunManager.Instance?.IsInProgress ?? false))
        {
            return new List<ExtraIconAmountLabelSpec>();
        }
        return new List<ExtraIconAmountLabelSpec>
        {
            ExtraIconAmountLabelSpec.RichText(
                ExtraIconAmountLabelCorner.BottomRight,
                $"[font_size={BadgeFontSize}]{s_remaining}[/font_size]")
        };
    }

    // === 获得 / 读档 ===

    public override async Task AfterObtained()
    {
        s_remaining = InitialDumplings;
        SavedRemaining = InitialDumplings;
        TamedPets.ResetRun();
        SavedPetsData = string.Empty;
        RitsuLibFramework.EnsureModelIdentity(this);
        RefreshBadge();
        Entry.Logger.Info("[MomotaroDumplings] AfterObtained: 3 颗丸子，无随从");
        await base.AfterObtained();
    }

    // 存档权威：读档后用保存值覆盖 static（同孢子囊）
    private void SyncFromSave()
    {
        var live = LiveRelic ?? this;
        if (live._savedRemaining != s_remaining)
        {
            s_remaining = live._savedRemaining;
            RefreshBadge();
        }
        TamedPets.LoadRecords(live._savedPetsData);
        Entry.Logger.Info($"[MomotaroDumplings] 读档同步：丸子 {s_remaining}，随从 {TamedPets.Records.Count}");
    }

    // 把 static 状态写回持有的那件（可变）遗物
    private void Persist()
    {
        var live = LiveRelic;
        if (live != null && !ReferenceEquals(live, this))
        {
            if (live.IsMutable)
            {
                live.SavedRemaining = s_remaining;
                live.SavedPetsData = TamedPets.EncodeRecords();
            }
        }
        else if (IsMutable)
        {
            SavedRemaining = s_remaining;
            SavedPetsData = TamedPets.EncodeRecords();
        }
    }

    // === 右键：直接收服 ===

    public bool CanHandleRightClickLocal(ModRightClickContext context)
    {
        RitsuLibFramework.EnsureModelIdentity(this);
        return true;
    }

    public bool CanExecuteRightClick(ModRightClickExecutionContext context)
    {
        return Owner != null
            && s_remaining > 0
            && CombatManager.Instance.DebugOnlyGetState() != null;
    }

    // 右键直接用遗物收服：借用原版的目标选择界面（与药水/休息处选项同一套），
    // 只允许选中"可以驯服"的敌人，选完立即收服。
    public async Task OnRightClick(ModRightClickExecutionContext context)
    {
        var player = Owner;
        if (player == null || s_remaining <= 0 || CombatManager.Instance.DebugOnlyGetState() == null)
        {
            return;
        }

        var manager = NTargetManager.Instance;
        if (manager == null)
        {
            Entry.Logger.Info("[MomotaroDumplings] 拿不到 NTargetManager，中止");
            return;
        }

        var playerNode = NCombatRoom.Instance?.GetCreatureNode(player.Creature);
        var startPosition = playerNode?.GlobalPosition ?? Vector2.Zero;
        manager.StartTargeting(
            TargetType.AnyEnemy,
            startPosition,
            TargetMode.ClickMouseToTarget,
            () => !CombatManager.Instance.IsInProgress, // 战斗结束就退出选择
            node => node is NCreature candidate && TamedPets.CanTame(candidate.Entity, player.Creature));

        var selected = await manager.SelectionFinished();
        if (selected is not NCreature targetNode || !TamedPets.CanTame(targetNode.Entity, player.Creature))
        {
            Entry.Logger.Info("[MomotaroDumplings] 未选中有效目标，收服取消");
            return;
        }

        var enemyName = targetNode.Entity.Monster.Id.Entry;
        if (!await TameCreature(new ThrowingPlayerChoiceContext(), targetNode.Entity))
        {
            Entry.Logger.Info($"[MomotaroDumplings] 收服 {enemyName} 失败，不消耗丸子");
            return;
        }

        s_remaining--;
        Persist();
        RefreshBadge();
        Entry.Logger.Info($"[MomotaroDumplings] 收服完成，剩余 {s_remaining} 颗丸子");
    }

    // === 战斗开始：重召全部持久随从 ===

    public override async Task BeforeCombatStartLate()
    {
        SyncFromSave();

        if (Owner?.Creature?.CombatState != null)
        {
            // 快照遍历：召唤过程中 Records 可能被清理（怪物模型已不存在的旧档）
            foreach (var record in TamedPets.Records.ToList())
            {
                await SummonPet(record);
            }
            Persist();
        }
        await base.BeforeCombatStartLate();
    }

    // === 随从行动 ===

    // 玩家回合开始：随从摇招、显示原版意图图标，然后立刻按原版招式行动。
    //
    // 位置特意与植物召唤物（PlantSummonBase.AfterPlayerTurnStart）保持一致，原因是踩过两次坑：
    // 1) 放在敌方回合切换点（BeforeSideTurnStart）await —— 那里执行原版怪物招式（内部大量动画/暂停等待），
    //    一旦挂起会把整条 StartTurn 管线拖死（表现为手牌清空后回合推进不了）；
    // 2) 丢到后台任务里跑 —— 会与回合流程并发：StartTurn 先对当前侧生物取快照，
    //    随从在快照之后被移除/失联，随后遍历到 CombatState 为 null 的生物，
    //    在 Hook.IterateCombatHookListeners 里抛 NullReferenceException（整个回合循环死掉）。
    // 放在"玩家回合开始、玩家已可行动"的状态下顺序执行，两个问题都不会有。
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner)
        {
            // 按"最先抓到的先动"顺序行动（Records 顺序）；活动表是 Dictionary，
            // 有随从死亡被移除后字典会复用空位，顺序不再可靠
            var pets = TamedPets.ActiveInTameOrder(player);
            if (pets.Count > 0)
            {
                // 随从一只一只行动，这期间玩家出不了牌，所以先把原版"手牌不可用"的表现打出来
                // （手牌下沉 + 变暗，和敌方回合是同一套动画），否则玩家会以为是卡了
                SetHandVisuallyDisabled(true);
                try
                {
                    foreach (var pet in pets)
                    {
                        if (!CombatManager.Instance.IsInProgress || !pet.IsAlive)
                        {
                            continue;
                        }
                        var combatState = pet.CombatState;
                        if (combatState == null)
                        {
                            continue;
                        }
                        await SafeRollAndShow(pet);
                        // 稍等片刻，让玩家看清头顶的意图图标
                        await Cmd.CustomScaledWait(0.4f, 0.6f);
                        await SafePerform(pet, combatState);
                    }
                }
                finally
                {
                    SetHandVisuallyDisabled(false);
                }
            }
        }
        await base.AfterPlayerTurnStart(choiceContext, player);
    }

    // === 随从行动期间的手牌表现 ===

    // 直接复用原版 NPlayerHand 内部那套"手牌不可用"动画（手牌下沉 + 变暗），
    // 和敌方回合给玩家的信号完全一致，不另造一套自制的 UI。
    // 这三个成员在 NPlayerHand 里都是 private，只能反射取；取不到就只少个表现提示，不影响玩法。
    private static readonly MethodInfo? s_handAnimDisable = typeof(NPlayerHand)
        .GetMethod("AnimDisable", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly MethodInfo? s_handAnimEnable = typeof(NPlayerHand)
        .GetMethod("AnimEnable", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly FieldInfo? s_handIsDisabledField = typeof(NPlayerHand)
        .GetField("_isDisabled", BindingFlags.NonPublic | BindingFlags.Instance);

    // 切换手牌的"不可出牌"表现。已经处于目标状态就不动 —— 免得把别的流程（比如多人里的
    // 非当前玩家）设成禁用的手牌给解禁了。全程兜异常：这只是表现层，绝不能影响回合流程。
    private static void SetHandVisuallyDisabled(bool disabled)
    {
        try
        {
            var hand = NCombatRoom.Instance?.Ui?.Hand;
            var method = disabled ? s_handAnimDisable : s_handAnimEnable;
            if (hand == null || method == null)
            {
                return;
            }
            if (s_handIsDisabledField?.GetValue(hand) as bool? == disabled)
            {
                return;
            }
            method.Invoke(hand, null);
        }
        catch (Exception e)
        {
            Entry.Logger.Info($"[MomotaroDumplings] 手牌表现切换失败（已忽略）：{e.Message}");
        }
    }

    // === 承伤链：让驯服的随从替玩家挨打 ===
    // 顺序（坚果墙 > 骨手 > 驯服怪物 > 豌豆射手 > 向日葵 > 玩家）与溢出规则都在 PetAbsorb 里，
    // 这里只是接入点 —— 植物模型也调同一份逻辑，保证玩家没有植物时同样生效。

    public override Creature ModifyUnblockedDamageTarget(Creature target, decimal amount, ValueProp props, Creature? dealer)
        => PetAbsorb.ModifyTarget(Owner, target, props);

    public override decimal ModifyHpLostBeforeOsty(Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        PetAbsorb.ResetBeforeDamage(Owner, target);
        return amount;
    }

    public override decimal ModifyHpLostAfterOsty(Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
        => PetAbsorb.ModifyOverflow(Owner, target, amount, props, dealer);

    // === 死亡永久消失 / 战后保存血量 ===

    public override async Task AfterDeath(
        PlayerChoiceContext choiceContext, Creature creature,
        bool wasRemovalPrevented, float deathAnimLength)
    {
        if (TamedPets.IsTamed(creature))
        {
            var removed = TamedPets.NotifyDeath(creature);
            Persist();
            Entry.Logger.Info($"[MomotaroDumplings] 随从永久死亡：{removed?.MonsterId.Entry}");
        }
        await base.AfterDeath(choiceContext, creature, wasRemovalPrevented, deathAnimLength);
    }

    public override async Task AfterCombatVictory(CombatRoom room)
    {
        TamedPets.SnapshotAndClear();
        Persist();
        await base.AfterCombatVictory(room);
    }

    // === 驯服（由遗物右键直接调用）===
    // 返回是否真的收服成功（失败时调用方不该扣丸子）

    public async Task<bool> TameCreature(PlayerChoiceContext choiceContext, Creature enemy)
    {
        var player = Owner;
        if (player == null || !TamedPets.CanTame(enemy, player.Creature))
        {
            return false;
        }

        var id = enemy.Monster.Id;
        // 收服代价：随从的血上限比原怪低 20%（跨战斗重召也用的是这个值，存在记录里）
        var hp = Math.Max(1m, Math.Floor(enemy.MaxHp * TameHpRatio));
        var canonical = ModelDb.GetByIdOrNull<MonsterModel>(id);
        if (canonical == null)
        {
            Entry.Logger.Info($"[MomotaroDumplings] 无法驯服：ModelDb 找不到怪物 {id}");
            return false;
        }

        // 1. 在玩家侧创建同类怪物为随从，并把最大/当前生命设为原怪的最大生命（驯服即回满血）
        var record = new TamedPetRecord(id, hp, hp);
        var pet = await SummonPet(record);
        if (pet == null)
        {
            return false;
        }

        // 2. 登记为持久随从（活动实例与首招已由 SummonPet 处理）
        TamedPets.Records.Add(record);

        // 3. 原敌人以"逃跑"方式离场（非死亡、无死亡掉落，走原版离场流程；最后一只离场即胜利）
        await CreatureCmd.Escape(enemy);

        Flash();
        Persist();
        RefreshBadge();
        Entry.Logger.Info($"[MomotaroDumplings] 驯服 {id.Entry}（{hp} 血）");
        return true;
    }

    // === 内部工具 ===

    // 创建随从生物并放到玩家身边；失败/旧档失效返回 null（同时清理记录）
    private async Task<Creature?> SummonPet(TamedPetRecord record)
    {
        var player = Owner;
        var combatState = player?.Creature.CombatState;
        if (player == null || combatState == null)
        {
            return null;
        }

        var canonical = ModelDb.GetByIdOrNull<MonsterModel>(record.MonsterId);
        if (canonical == null)
        {
            Entry.Logger.Info($"[MomotaroDumplings] 旧档怪物已不存在，丢弃随从记录：{record.MonsterId}");
            TamedPets.Records.Remove(record);
            return null;
        }

        Creature? pet = null;
        try
        {
            pet = combatState.CreateCreature(canonical.ToMutable(), player.Creature.Side, null);
            // 先登记活动实例：AddPet 会触发 NCombatRoom.AddCreature，
            // 随从排版 patch（朝向/站位/血条）需要在那时就能认出它
            TamedPets.Register(pet, record);
            await PlayerCmd.AddPet(pet, player);
            await CreatureCmd.SetMaxHp(pet, record.MaxHp);
            await CreatureCmd.SetCurrentHp(pet, record.CurrentHp);
            await SafeRollAndShow(pet);
            // 固有特性：被驯服后出手变轻，攻击伤害 -50%（正面 buff，随从死亡即消失）
            await PowerCmd.Apply<TamedPetPower>(
                new ThrowingPlayerChoiceContext(), pet, 1m, player.Creature, null);
            Entry.Logger.Info($"[MomotaroDumplings] 重召随从 {record.MonsterId.Entry}：{record.CurrentHp}/{record.MaxHp}");
            return pet;
        }
        catch (Exception e)
        {
            // 某些特殊怪物（Boss 分体/事件专属）当随从可能炸，记录但不打断战斗开始
            Entry.Logger.Info($"[MomotaroDumplings] 重召 {record.MonsterId.Entry} 异常，丢弃该记录：{e.Message}");
            if (pet != null)
            {
                TamedPets.NotifyDeath(pet);
            }
            TamedPets.Records.Remove(record);
            return null;
        }
    }

    // 摇招 + 刷新意图 UI。回合管线里调用，全程兜异常防止卡死
    private static async Task SafeRollAndShow(Creature pet)
    {
        try
        {
            var combatState = pet.CombatState;
            if (combatState == null || !pet.IsAlive || pet.Monster.MoveStateMachine == null)
            {
                return;
            }
            pet.Monster.RollMove(combatState.Enemies);

            // 不适用招式（召唤小怪 / 给玩家塞状态牌）：把意图换成"格挡"，
            // 执行时改成给自己 6 点格挡（见 SafePerform），而不是白占一次行动
            if (IsUnusableMove(pet))
            {
                ConvertToBlockIntent(pet);
            }

            var node = NCombatRoom.Instance?.GetCreatureNode(pet);
            if (node != null)
            {
                await node.UpdateIntent(combatState.Enemies);
                // 直接显示、不做淡入：原版 RevealIntents 也用 tween 改同一个 modulate:a，
                // 两边各建一个 tween 抢同一属性会让意图偶现不显示（保持为 0）
                node.IntentContainer.Modulate = Colors.White;
            }
        }
        catch (Exception e)
        {
            Entry.Logger.Info($"[MomotaroDumplings] RollAndShow 异常（已忽略）：{pet.Monster?.Id.Entry} {e.Message}");
        }
    }

    // 执行一招：播放意图动画 -> 按原怪物招式行动（目标=存活敌人）
    private static async Task SafePerform(Creature pet, ICombatState combatState)
    {
        MoveState? move = null;
        try
        {
            if (!pet.IsAlive)
            {
                return;
            }

            move = pet.Monster?.NextMove;
            if (move == null)
            {
                return;
            }

            // 效果写在"玩家方"的招式一律不放（召唤小怪 / 给玩家塞状态牌）：
            // 随从已经在我方，这类招会去操作"玩家"，而目标被我们换成了敌人，
            // 解引用 target.Player 直接 NRE（比如蛙寄生虫的塞污染牌）。
            // 改为给自己 6 点格挡（意图在摇招时已换成"格挡"）。
            if (IsUnusableMove(pet))
            {
                await CreatureCmd.GainBlock(pet, UnusableMoveBlock, ValueProp.Move, null);
                Entry.Logger.Info($"[MomotaroDumplings] {pet.Monster.Id.Entry} 把不适用招式 {move.Id} 转为 {UnusableMoveBlock} 格挡");
                return;
            }

            var node = NCombatRoom.Instance?.GetCreatureNode(pet);
            if (node != null)
            {
                await node.PerformIntent();
            }
            var targets = combatState.Enemies.Where(e => e.IsAlive).ToList();
            await move.PerformMove(targets);
        }
        catch (Exception e)
        {
            Entry.Logger.Info($"[MomotaroDumplings] Perform 异常（已忽略）：{pet.Monster?.Id.Entry} {e.Message}");
            // 执行失败和"不适用招式"是同类情况：同样补成格挡，别让随从白占一次行动
            await GainFallbackBlock(pet);
        }
        finally
        {
            // 关键：无论这一招是成功、被跳过还是抛了异常，都必须让状态机往前走。
            // 否则会永远停在同一招上 —— 表现就是随从每回合都放同一个意图，卡死在那里。
            if (move != null)
            {
                MarkMovePerformed(move);
                pet.Monster?.MoveStateMachine?.OnMovePerformed(move);
            }
        }
    }

    // 兜底格挡：这次行动用不出来时，统一补成 6 点格挡
    private static async Task GainFallbackBlock(Creature pet)
    {
        try
        {
            if (pet.IsAlive && pet.CombatState != null)
            {
                await CreatureCmd.GainBlock(pet, UnusableMoveBlock, ValueProp.Move, null);
                Entry.Logger.Info($"[MomotaroDumplings] {pet.Monster?.Id.Entry} 兜底获得 {UnusableMoveBlock} 格挡");
            }
        }
        catch (Exception e)
        {
            Entry.Logger.Info($"[MomotaroDumplings] 兜底格挡失败（已忽略）：{e.Message}");
        }
    }

    // 招式效果目标是"玩家方"、随从执行会出错或坑自己方的招，一律不放而是转成格挡：
    //   Summon     召唤小怪（召唤物会进敌方）
    //   StatusCard 给玩家塞状态牌（解引用 target.Player 直接 NRE）
    //   CardDebuff 给玩家手牌上负面（同上是针对玩家手牌的）
    //   Escape     逃跑（随从逃了就直接离场）
    private static bool IsUnusableMove(Creature pet)
    {
        var move = pet.Monster?.NextMove;
        if (move == null)
        {
            return false;
        }
        return move.Intents.Any(i => i.IntentType is IntentType.Summon
            or IntentType.StatusCard
            or IntentType.CardDebuff
            or IntentType.Escape);
    }

    // 不适用招式被替换成的内容：给自己固定 6 点格挡
    private const int UnusableMoveBlock = 6;

    // 把当前招式的意图整体换成"格挡"，让随从头上显示的就是它真正会做的事。
    // Intents 是 private set 的自动属性，只能反射改；改完同一个 MoveState 会一直沿用，
    // 正好符合"这类招以后都当格挡用"的需求。
    private static void ConvertToBlockIntent(Creature pet)
    {
        try
        {
            var move = pet.Monster?.NextMove;
            if (move == null)
            {
                return;
            }
            s_moveIntentsField?.SetValue(move, new List<AbstractIntent> { new DefendIntent() });
        }
        catch (Exception e)
        {
            Entry.Logger.Info($"[MomotaroDumplings] 替换格挡意图失败（已忽略）：{e.Message}");
        }
    }

    private static readonly FieldInfo? s_moveIntentsField =
        typeof(MoveState).GetField("<Intents>k__BackingField",
            BindingFlags.NonPublic | BindingFlags.Instance);

    // 把招式标记为"已执行"。跳过不放时若不标记，MustPerformOnceBeforeTransitioning 的招式
    // 会让状态机永远停在原地（下回合仍摇到同一招）。
    private static void MarkMovePerformed(MoveState move)
    {
        try
        {
            s_performedOnceField?.SetValue(move, true);
        }
        catch
        {
            // 反射失败只会退化为一回合不放招，不影响其它逻辑
        }
    }

    private static readonly FieldInfo? s_performedOnceField =
        typeof(MoveState).GetField("_performedAtLeastOnce",
            BindingFlags.NonPublic | BindingFlags.Instance);
}
