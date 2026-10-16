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
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Powers;

public abstract class FourArmsPowerBase : ModPowerTemplate
{
    protected abstract string LocKey { get; }
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override LocString Title => new("powers", $"OTHERWORLD_TREASURES_POWER_FOUR_ARMS_{LocKey}.title");
    public override LocString Description => new("powers", $"OTHERWORLD_TREASURES_POWER_FOUR_ARMS_{LocKey}.description");
    public override PowerAssetProfile AssetProfile => AlienPowerArt.Profile($"four_arms/{LocKey}");
}

/// <summary>四臂之力：攻击计数跨回合；连击条件和一次性增益仅保留至本回合结束。</summary>
[RegisterPower]
public class FourArmsPassivePower : FourArmsPowerBase
{
    protected override string LocKey => "PASSIVE";
    public override PowerStackType StackType => PowerStackType.Single;
    public int AttacksTowardDouble { get; private set; }
    public int AttacksThisTurn { get; private set; }
    public int AttacksBeforeCurrent { get; private set; }
    public bool PreviousCardWasAttack { get; private set; }
    private CardType? _lastCardType;
    private CardPlay? _boostedPlay;
    private decimal _activeDamageBonus;
    public decimal NextAttackDamageBonus { get; set; }
    public bool NextAttackFree { get; set; }
    public bool NextHeavyAttackDouble { get; set; }
    private HashSet<CardPlay> _doubledPlays = new();
    private HashSet<CardPlay> _heavyBoostedPlays = new();
    public bool DoubledBy(CardPlay play) => _doubledPlays.Contains(play);
    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _doubledPlays = new();
        _heavyBoostedPlays = new();
    }

    public static decimal Strength(Creature creature) => creature.GetPower<StrengthPower>()?.Amount ?? 0;

    public Task DoubleStrength(PlayerChoiceContext context, CardModel? source)
        => DoubleStrength(context, Owner, source);

    public static async Task DoubleStrength(PlayerChoiceContext context, Creature owner, CardModel? source)
    {
        if (owner.GetPower<StrengthPower>() is { Amount: not 0 } strength)
            await PowerCmd.ModifyAmount(context, strength, strength.Amount, owner, source);

        if (owner.GetPower<FourArmsFrenzyPower>() is { } frenzy && owner.Player is { } player)
        {
            await CardPileCmd.Draw(context, frenzy.Amount, player);
            await PlayerCmd.GainEnergy(frenzy.Amount, player);
        }
        if (owner.GetPower<FourArmsAftershockPower>() is { } aftershock
            && owner.CombatState is { } combat && Strength(owner) > 0)
        {
            var enemies = combat.Enemies.Where(e => e.IsAlive).ToList();
            if (enemies.Count > 0)
                await CreatureCmd.Damage(context, enemies, Strength(owner) * aftershock.Amount,
                    ValueProp.Unpowered, owner, null, null);
        }
    }

    public override async Task BeforeCardPlayed(CardPlay play)
    {
        if (play.Player.Creature != Owner) return;
        PreviousCardWasAttack = _lastCardType == CardType.Attack;
        AttacksBeforeCurrent = AttacksThisTurn;
        _lastCardType = play.Card.Type;
        if (play.Card.Type != CardType.Attack) return;

        // 费用已在此钩子之前结算；此处仅消耗符合条件的免能标记。
        if (NextAttackFree) NextAttackFree = false;
        if (NextHeavyAttackDouble && IsHeavyAttack(play.Card))
        {
            NextHeavyAttackDouble = false;
            _heavyBoostedPlays.Add(play);
        }
        _boostedPlay = play;
        _activeDamageBonus = NextAttackDamageBonus;
        NextAttackDamageBonus = 0;
        AttacksThisTurn++;
        if (++AttacksTowardDouble >= 4)
        {
            AttacksTowardDouble = 0;
            _doubledPlays.Add(play);
            await DoubleStrength(new ThrowingPlayerChoiceContext(), play.Card);
        }
    }

    public static bool IsHeavyAttack(CardModel card)
        => card.Type == CardType.Attack && !card.EnergyCost.CostsX && card.EnergyCost.Canonical >= 2;

    public override bool TryModifyEnergyCostInCombatLate(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        bool free = card.Owner.Creature == Owner && card.Type == CardType.Attack
            && NextAttackFree;
        modifiedCost = free ? 0 : originalCost;
        return free;
    }

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (dealer != Owner || !props.IsPoweredAttack() || cardSource?.Type != CardType.Attack) return 0;
        // 原版此钩子的返回值是加数；整张牌的所有段、所有目标使用同一加成。
        return cardPlay != null ? (cardPlay == _boostedPlay ? _activeDamageBonus : 0) : NextAttackDamageBonus;
    }

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (dealer != Owner || !props.IsPoweredAttack() || cardSource == null) return 1m;
        bool doubled = cardPlay != null ? _heavyBoostedPlays.Contains(cardPlay)
            : NextHeavyAttackDouble && IsHeavyAttack(cardSource);
        return doubled ? 2m : 1m;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
    {
        _doubledPlays.Remove(play);
        _heavyBoostedPlays.Remove(play);
        if (play == _boostedPlay)
        {
            _boostedPlay = null;
            _activeDamageBonus = 0;
        }
        return Task.CompletedTask;
    }

    internal void ResetTurn()
    {
        AttacksThisTurn = 0;
        AttacksBeforeCurrent = 0;
        PreviousCardWasAttack = false;
        _lastCardType = null;
        NextAttackFree = false;
        NextHeavyAttackDouble = false;
        _doubledPlays.Clear();
        _heavyBoostedPlays.Clear();
        NextAttackDamageBonus = 0;
        _boostedPlay = null;
        _activeDamageBonus = 0;
    }

    public override Task AfterPlayerTurnStart(PlayerChoiceContext context, Player player)
    {
        if (player.Creature == Owner) ResetTurn();
        return Task.CompletedTask;
    }

    public override Task AfterSideTurnEnd(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner)) ResetTurn();
        return Task.CompletedTask;
    }
}

[RegisterPower]
public class FourArmsFrenzyPower : FourArmsPowerBase
{
    protected override string LocKey => "FRENZY";
}

[RegisterPower]
public class FourArmsAftershockPower : FourArmsPowerBase
{
    protected override string LocKey => "AFTERSHOCK";
}

[RegisterPower]
public class FourArmsOffenseDefensePower : FourArmsPowerBase
{
    protected override string LocKey => "OFFENSE_DEFENSE";
    public override async Task AfterDamageGiven(PlayerChoiceContext context, Creature? dealer,
        DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        // 被格挡的命中也算；未命中/伤害被完全取消不算，能力伤害不会触发。
        if (dealer != Owner || !props.IsPoweredAttack() || target.Side == Owner.Side || result.TotalDamage <= 0) return;
        decimal block = FourArmsPassivePower.Strength(Owner) * Amount;
        if (block > 0) await CreatureCmd.GainBlock(Owner, block, ValueProp.Unpowered, null);
    }
}
