using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using OtherworldTreasures.Scripts.Alien;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Powers;

/// <summary>
/// 燃尽手牌状态牌的公共入口（火焰人系列）。
/// "燃尽"= 把手牌里的状态牌消耗掉（CardCmd.Exhaust）。
/// 统一处理：
/// - 更新熔岩之躯上的本回合计数器（燃尽数、是否燃尽过）
/// - 通知【灼热反噬】（每张对全体造成伤害，每回合上限 4 次）
/// - 通知【灼热核心】（累计到第 3 张时全体伤害+格挡，每回合 1 次）
/// - 【熔火塑形】：若本次是某张牌主动燃尽且标记生效，退还该牌的能量费用
/// 返回燃尽结果（总数、其中灼伤牌数量）。
/// </summary>
internal static class HeatblastStatus
{
    /// <summary>燃尽结果。</summary>
    internal readonly struct BurnResult
    {
        public int Count { get; }
        public int BurnCardCount { get; }
        public bool BurnedAnyBurn => BurnCardCount > 0;

        public BurnResult(int count, int burnCardCount)
        {
            Count = count;
            BurnCardCount = burnCardCount;
        }
    }

    /// <summary>取手牌中所有状态牌的快照（燃尽过程中手牌会变化，必须先快照）。</summary>
    internal static List<CardModel> InHand(Player player)
    {
        return player.PlayerCombatState?.Hand.Cards.Where(c => c.Type == CardType.Status).ToList()
               ?? new List<CardModel>();
    }

    /// <summary>取手牌中所有【灼伤】状态牌。</summary>
    internal static List<CardModel> BurnsInHand(Player player)
    {
        return player.PlayerCombatState?.Hand.Cards.Where(IsBurnCard).ToList()
               ?? new List<CardModel>();
    }

    /// <summary>判断一张牌是否是原版【灼伤】(Burn) 状态牌。</summary>
    internal static bool IsBurnCard(CardModel card)
        => card.GetType() == typeof(Burn);

    /// <summary>
    /// 燃尽给定的状态牌。
    /// </summary>
    /// <param name="cardPlay">触发本次燃尽的牌（主动打牌时传入；被动回合结束燃烧传 null）。</param>
    /// <param name="fromPassive">是否来自熔岩之躯的回合结束被动燃烧（不触发熔火塑形免能）。</param>
    internal static async Task<BurnResult> Burn(
        PlayerChoiceContext choiceContext, Player player, IReadOnlyList<CardModel> statusCards,
        CardPlay? cardPlay, bool fromPassive)
    {
        if (statusCards.Count == 0)
        {
            Entry.Logger.Info(
                $"[Heatblast] 燃尽：手牌里没有状态牌（手牌共 {player.PlayerCombatState?.Hand.Cards.Count ?? 0} 张，fromPassive={fromPassive}）");
            return new BurnResult(0, 0);
        }
        Entry.Logger.Info(
            $"[Heatblast] 燃尽：找到状态牌 {statusCards.Count} 张（手牌共 {player.PlayerCombatState?.Hand.Cards.Count ?? 0} 张，fromPassive={fromPassive}）");

        // 统计其中灼伤牌数量（燃尽前判定，燃尽后牌就没了）
        int burnCardCount = statusCards.Count(IsBurnCard);

        foreach (var card in statusCards)
        {
            // 被动（回合结束）燃烧必须跳过卡牌飞行动画：回合结束时段那段 Tween 可能永不结束，
            // 会抛异常打断回合管线导致卡死（本仓库已记录过的硬规矩）。
            await CardCmd.Exhaust(choiceContext, card, causedByEthereal: false, skipVisuals: fromPassive);
        }
        if (fromPassive)
        {
            // 静默消耗不会撤掉手牌 UI 上的卡牌节点，会留下"看得见却没用"的幽灵牌，这里手动撤掉
            HandUiSync.RemoveFromHandUi(statusCards);
        }

        // 更新熔岩之躯计数器
        var passive = player.Creature?.GetPower<HeatblastPassivePower>();
        if (passive != null)
        {
            passive.BurnedStatusCountThisTurn += statusCards.Count;
            passive.HasBurnedStatusThisTurn = true;

            // 熔火塑形：主动打牌燃尽时，若标记生效则退还该牌能量
            if (!fromPassive && cardPlay != null && passive.NextBurnCardFreeEnergy)
            {
                passive.NextBurnCardFreeEnergy = false;
                int cost = cardPlay.Card?.EnergyCost.Canonical ?? 0;
                if (cost > 0)
                {
                    await PlayerCmd.GainEnergy(cost, player);
                }
            }
        }

        // 通知【灼热反噬】
        var feedback = player.Creature?.GetPower<HeatblastScorchingFeedbackPower>();
        if (feedback != null)
        {
            await feedback.OnStatusBurned(choiceContext, statusCards.Count);
        }

        // 通知【灼热核心】
        var core = player.Creature?.GetPower<HeatblastScorchingCorePower>();
        if (core != null)
        {
            await core.OnStatusBurned(choiceContext, player);
        }

        return new BurnResult(statusCards.Count, burnCardCount);
    }
}

/// <summary>
/// 火焰人·太阳耀斑（能力牌，烈焰爆破核心）：
/// - 每回合首次给予灼伤时，额外 +2 层（触发标记存在本实例上，每个玩家回合开始时重置）。
/// - 每回合开始时，将 1 张【灼伤】状态牌加入手牌。
/// 只在自己的回合内（出牌时）触发；首次加成的查询由 AlienScorch.Apply 统一处理。
/// </summary>
[RegisterPower]
public class HeatblastSolarFlarePower : ModPowerTemplate
{
    // 每回合首次施加灼伤的额外层数
    internal const int BonusStacks = 2;

    // 本回合是否已触发过"首次加成"
    private bool _triggeredThisTurn;

    public override PowerType Type => PowerType.Buff;

    // 固定效果、不层叠：不显示数字
    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://OtherworldTreasures/images/heroes/Heatblast.png",
        BigIconPath: "res://OtherworldTreasures/images/heroes/Heatblast.png");

    public override LocString Title => new LocString("powers", "OTHERWORLD_TREASURES_POWER_HEATBLAST_SOLAR_FLARE.title");

    public override LocString Description => new LocString("powers", "OTHERWORLD_TREASURES_POWER_HEATBLAST_SOLAR_FLARE.description");

    /// <summary>本回合首次施加灼伤：返回 true 表示这次要吃额外层数加成（并顺便记为已触发）。</summary>
    internal bool TryTriggerFirstScorch()
    {
        if (_triggeredThisTurn)
        {
            return false;
        }
        _triggeredThisTurn = true;
        return true;
    }

    // 每个玩家回合开始：重置"本回合是否已触发"，并把 1 张【灼伤】状态牌加入手牌（只处理自己持有者的回合）
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner)
        {
            return;
        }
        _triggeredThisTurn = false;

        // 手牌满时不加入（由统一入口判断），并打上"我方"标记
        if (player.Creature?.CombatState is not { } combatState)
        {
            return;
        }
        await HeatblastBurns.AddToHand(combatState, player, 1);
    }
}

/// <summary>
/// 火焰人·灼热反噬（熔岩塑形能力）：
/// 直到回合结束：每当你燃尽一张状态牌，对所有敌人造成 2 点伤害。每回合最多触发 4 次。
/// 由 HeatblastStatus.Burn 在燃尽当刻回调。
/// </summary>
[RegisterPower]
public class HeatblastScorchingFeedbackPower : ModPowerTemplate
{
    private const int DamagePerBurn = 2;
    private const int MaxTriggersPerTurn = 4;

    // 本回合已触发次数
    private int _triggersThisTurn;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://OtherworldTreasures/images/heroes/Heatblast.png",
        BigIconPath: "res://OtherworldTreasures/images/heroes/Heatblast.png");

    public override LocString Title => new LocString("powers", "OTHERWORLD_TREASURES_POWER_HEATBLAST_SCORCHING_FEEDBACK.title");
    public override LocString Description => new LocString("powers", "OTHERWORLD_TREASURES_POWER_HEATBLAST_SCORCHING_FEEDBACK.description");

    // 每回合开始重置触发次数
    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature == Owner)
        {
            _triggersThisTurn = 0;
        }
        return Task.CompletedTask;
    }

    /// <summary>由 HeatblastStatus.Burn 回调：每燃尽 1 张触发一次（直到上限）。</summary>
    internal async Task OnStatusBurned(PlayerChoiceContext choiceContext, int count)
    {
        var combatState = Owner?.CombatState;
        if (combatState == null)
        {
            return;
        }
        for (int i = 0; i < count && _triggersThisTurn < MaxTriggersPerTurn; i++)
        {
            _triggersThisTurn++;
            var enemies = combatState.Enemies.Where(e => e.IsAlive).ToList();
            if (enemies.Count > 0)
            {
                // 用 CreatureCmd.Damage 直接结算。不能走 DamageCmd.Attack(...).FromCard(null!, null!)：
                // 空卡牌会在 AttackCommand.FromCard 里 NRE，异常打断回合管线直接把战斗打死（已实测卡住）。
                await CreatureCmd.Damage(
                    choiceContext, enemies, DamagePerBurn,
                    MegaCrit.Sts2.Core.ValueProps.ValueProp.Move, Owner, null, null);
            }
        }
    }
}

/// <summary>
/// 火焰人·炽热领域（熔岩塑形能力）：
/// 回合结束时，若本回合燃尽过至少 3 张状态牌，获得 7 点格挡。
/// 若此时仍有敌人拥有灼伤，额外获得 3 点格挡。
/// </summary>
[RegisterPower]
public class HeatblastScorchingDomainPower : ModPowerTemplate
{
    private const int Threshold = 3;
    private const int BaseBlock = 7;
    private const int BonusBlock = 3;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://OtherworldTreasures/images/heroes/Heatblast.png",
        BigIconPath: "res://OtherworldTreasures/images/heroes/Heatblast.png");

    public override LocString Title => new LocString("powers", "OTHERWORLD_TREASURES_POWER_HEATBLAST_SCORCHING_DOMAIN.title");
    public override LocString Description => new LocString("powers", "OTHERWORLD_TREASURES_POWER_HEATBLAST_SCORCHING_DOMAIN.description");

    // 回合结束时结算（在熔岩之躯燃尽之后，用同一个 BeforeSideTurnEnd 钩子，靠能力挂载顺序自然先后）
    public override async Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner == null || !participants.Contains(Owner))
        {
            return;
        }
        // 回合结束管线里的钩子抛异常会打断整条管线，必须兜住
        try
        {
            var passive = Owner.GetPower<HeatblastPassivePower>();
            if (passive == null || passive.BurnedStatusCountThisTurn < Threshold)
            {
                return;
            }
            decimal block = BaseBlock;
            bool anyScorch = Owner.CombatState?.Enemies.Any(e => e.IsAlive && e.HasPower<AlienScorchPower>()) ?? false;
            if (anyScorch)
            {
                block += BonusBlock;
            }
            await CreatureCmd.GainBlock(Owner, block, MegaCrit.Sts2.Core.ValueProps.ValueProp.Move, null);
        }
        catch (System.Exception e)
        {
            Entry.Logger.Warn($"[Heatblast] 炽热领域回合结束结算失败（已忽略，避免打断回合管线）：{e}");
        }
    }
}

/// <summary>
/// 火焰人·灼热核心（熔岩塑形核心）：
/// 每回合你累计燃尽到第 3 张状态牌时，对所有敌人造成 6 点伤害并获得 6 点格挡。每回合最多触发 1 次。
/// 由 HeatblastStatus.Burn 在燃尽当刻回调。
/// </summary>
[RegisterPower]
public class HeatblastScorchingCorePower : ModPowerTemplate
{
    private const int Threshold = 3;
    private const int Damage = 6;
    private const int Block = 6;

    // 本回合是否已触发
    private bool _triggeredThisTurn;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://OtherworldTreasures/images/heroes/Heatblast.png",
        BigIconPath: "res://OtherworldTreasures/images/heroes/Heatblast.png");

    public override LocString Title => new LocString("powers", "OTHERWORLD_TREASURES_POWER_HEATBLAST_SCORCHING_CORE.title");
    public override LocString Description => new LocString("powers", "OTHERWORLD_TREASURES_POWER_HEATBLAST_SCORCHING_CORE.description");

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature == Owner)
        {
            _triggeredThisTurn = false;
        }
        return Task.CompletedTask;
    }

    /// <summary>由 HeatblastStatus.Burn 回调：检查累计是否达到阈值。</summary>
    internal async Task OnStatusBurned(PlayerChoiceContext choiceContext, Player player)
    {
        if (_triggeredThisTurn || Owner == null)
        {
            return;
        }
        var passive = Owner.GetPower<HeatblastPassivePower>();
        if (passive == null || passive.BurnedStatusCountThisTurn < Threshold)
        {
            return;
        }
        _triggeredThisTurn = true;

        var combatState = Owner.CombatState;
        if (combatState != null)
        {
            var enemies = combatState.Enemies.Where(e => e.IsAlive).ToList();
            if (enemies.Count > 0)
            {
                // 同上：用 CreatureCmd.Damage，不要用 FromCard(null!)
                await CreatureCmd.Damage(
                    choiceContext, enemies, Damage,
                    MegaCrit.Sts2.Core.ValueProps.ValueProp.Move, Owner, null, null);
            }
        }
        await CreatureCmd.GainBlock(Owner, Block, MegaCrit.Sts2.Core.ValueProps.ValueProp.Move, null);
    }
}

/// <summary>
/// 火焰人·超燃加速（喷射推进核心）：
/// 本回合每当你通过卡牌或能力获得额外能量，下一次攻击造成 +3 伤害。每回合最多触发 3 次。
/// 按"获得能量事件"触发：一次 +2 能量只触发 1 次。
/// 额外能量通过 HeatblastEnergy.GainExtra 获得时会回调本能力。
/// </summary>
[RegisterPower]
public class HeatblastSupercombustionPower : ModPowerTemplate
{
    private const int DamagePerTrigger = 3;
    private const int MaxTriggersPerTurn = 3;

    // 本回合已积攒的充能数（每个充能 = +3 伤害，下一次攻击消耗全部）
    private int _charges;
    // 本回合已触发的能量事件数
    private int _triggersThisTurn;
    // 正在结算的攻击（防止多段攻击重复加成）
    private CardPlay? _pendingAttack;
    private decimal _pendingBonus;
    private bool _bonusApplied;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://OtherworldTreasures/images/heroes/Heatblast.png",
        BigIconPath: "res://OtherworldTreasures/images/heroes/Heatblast.png");

    public override LocString Title => new LocString("powers", "OTHERWORLD_TREASURES_POWER_HEATBLAST_SUPERCOMBUSTION.title");
    public override LocString Description => new LocString("powers", "OTHERWORLD_TREASURES_POWER_HEATBLAST_SUPERCOMBUSTION.description");

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature == Owner)
        {
            _charges = 0;
            _triggersThisTurn = 0;
            _pendingAttack = null;
            _pendingBonus = 0;
            _bonusApplied = false;
        }
        return Task.CompletedTask;
    }

    /// <summary>由 HeatblastEnergy.GainExtra 回调：每次获得额外能量触发 1 次（上限 3）。</summary>
    internal void OnExtraEnergyGained()
    {
        if (_triggersThisTurn < MaxTriggersPerTurn)
        {
            _triggersThisTurn++;
            _charges++;
        }
    }

    // 攻击打出前：把积攒的充能转为该次攻击的待结算伤害加成
    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (Owner == null || cardPlay.Card?.Owner?.Creature != Owner)
        {
            return Task.CompletedTask;
        }
        if (cardPlay.Card.Type != CardType.Attack)
        {
            return Task.CompletedTask;
        }
        if (_charges > 0)
        {
            _pendingAttack = cardPlay;
            _pendingBonus = _charges * DamagePerTrigger;
            _bonusApplied = false;
            _charges = 0;
        }
        return Task.CompletedTask;
    }

    // 修改攻击伤害：对待结算的攻击施加一次性加成（多段攻击只加在第一段）
    public override decimal ModifyDamageAdditive(
        Creature? target, decimal amount, MegaCrit.Sts2.Core.ValueProps.ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (!_bonusApplied && cardPlay != null && cardPlay == _pendingAttack)
        {
            _bonusApplied = true;
            return amount + _pendingBonus;
        }
        return amount;
    }

    // 攻击打出后：清理待结算状态
    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay == _pendingAttack)
        {
            _pendingAttack = null;
            _pendingBonus = 0;
            _bonusApplied = false;
        }
        return Task.CompletedTask;
    }
}
