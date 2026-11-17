using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Konoe;

[RegisterPower]
public sealed class KonoeFrozenPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerAssetProfile AssetProfile => KonoePowerArt.For("frozen");
    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? source, CardPlay? play)
        => dealer == Owner && props.IsPoweredAttack() ? -2 * Amount : 0;
}

[RegisterPower]
public sealed class KonoeWetPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerAssetProfile AssetProfile => KonoePowerArt.For("wet");
    public override bool TryModifyPowerAmountReceived(PowerModel power, Creature target, decimal amount, Creature? applier, out decimal modified)
    {
        modified = amount;
        if (target != Owner || power is not KonoeFrozenPower || amount <= 0) return false;
        modified += KonoeAttributeRules.WetFreezeBonus(Amount);
        return modified != amount;
    }
    public override async Task AfterPowerAmountChanged(PlayerChoiceContext context, PowerModel power, decimal amount, Creature? applier, CardModel? source)
    {
        if (power.Owner != Owner || power is not KonoeFrozenPower || amount <= 0) return;
        int used = KonoeAttributeRules.WetFreezeBonus(Amount) * 8;
        if (used > 0) await PowerCmd.ModifyAmount(context, this, -used, applier, source);
    }
}

[RegisterPower]
public sealed class KonoeBurnPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerAssetProfile AssetProfile => KonoePowerArt.For("burn");
    // Resolve after the side's turn has actually started (same timing as vanilla PoisonPower).
    // BeforeSideTurnStart runs while the turn loop is still iterating the creaturesStartingTurn snapshot;
    // killing a monster there detaches it from combat (CombatState = null) but leaves it in that snapshot,
    // so the following Creature.AfterTurnStart -> ClearBlock -> Hook.ShouldClearBlock(null, ...) throws
    // a NullReferenceException and the whole turn loop dies (combat stuck until the room restarts).
    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != CombatSide.Enemy || Owner.CombatState == null || !participants.Contains(Owner) || !Owner.IsAlive) return;
        var context = new BlockingPlayerChoiceContext();
        int stacks = Amount;
        await CreatureCmd.Damage(context, Owner, stacks, ValueProp.Unpowered, Owner);
        if (Owner.IsAlive && Owner.CombatState != null && Owner.Powers.Contains(this)) await PowerCmd.ModifyAmount(context, this, KonoeAttributeRules.BurnRemaining(stacks) - Amount, null, null);
    }
}

[RegisterPower]
public sealed class KonoeKnockdownPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerAssetProfile AssetProfile => KonoePowerArt.For("knockdown");
    public override async Task AfterPowerAmountChanged(PlayerChoiceContext context, PowerModel power, decimal amount, Creature? applier, CardModel? source)
    {
        if (ReferenceEquals(power, this) && amount > 0) await CreatureCmd.LoseBlock(context, Owner, Owner.Block, applier);
    }
}

[RegisterPower]
public sealed class KonoeAirbornePower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerAssetProfile AssetProfile => KonoePowerArt.For("airborne");
}

internal static class KonoePowerArt
{
    // Element statuses use dedicated art; everything else still borrows a native icon.
    private static PowerAssetProfile Mod(string file) => new(
        IconPath: "res://OtherworldTreasures/images/powers/" + file + ".png",
        BigIconPath: "res://OtherworldTreasures/images/powers/" + file + "_big.png");

    internal static PowerAssetProfile For(string name)
    {
        if (name == "wet") return Mod("konoe_wet");
        if (name == "burn") return Mod("konoe_burn");
        if (name == "frozen") return Mod("konoe_frozen");
        if (name == "knockdown") return Mod("konoe_knockdown");
        if (name == "airborne") return Mod("konoe_airborne");
        if (name == "counter") return Mod("konoe_counter");
        if (name == "fairy") return Mod("konoe_fairy");
        PowerModel power = name switch
        {
            "guard" => ModelDb.Power<RagePower>(),
            _ => ModelDb.Power<TheBombPower>()
        };
        return new(IconPath: power.IconPath, BigIconPath: power.ResolvedBigIconPath);
    }
}

[RegisterPower]
public sealed class KonoeAttackGuardPower : ModPowerTemplate
{
    private CardPlay? _grantingPlay;
    [SavedProperty] public int BlockPerAttack { get; set; } = 3;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerAssetProfile AssetProfile => KonoePowerArt.For("guard");
    public override MegaCrit.Sts2.Core.Localization.LocString Description => new MegaCrit.Sts2.Core.Localization.LocString("powers", Id.Entry + ".description").AddValue("BlockPerAttack", BlockPerAttack).AddValue("Count", Amount);
    internal void SetGrantingPlay(CardPlay play) => _grantingPlay = play;
    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
    {
        if (ReferenceEquals(play, _grantingPlay)) { _grantingPlay = null; return; }
        if (play.Player == Owner.Player && play.Card.Type == CardType.Attack)
            await PowerCmd.ModifyAmount(context, this, BlockPerAttack, Owner, play.Card);
    }
}

[RegisterPower]
public sealed class KonoeCounterPower : ModPowerTemplate
{
    [SavedProperty] public int DamageElementTypes { get; set; }
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override PowerAssetProfile AssetProfile => KonoePowerArt.For("counter");
}

internal static class KonoeLocExtensions
{
    internal static MegaCrit.Sts2.Core.Localization.LocString AddValue(this MegaCrit.Sts2.Core.Localization.LocString text, string key, int value) { text.Add(key, (decimal)value); return text; }
}
