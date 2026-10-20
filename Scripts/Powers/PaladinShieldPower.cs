using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using OtherworldTreasures.Scripts.Relics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Powers;

[RegisterPower]
public sealed class PaladinShieldPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override LocString Title => new("powers", "OTHERWORLD_TREASURES_POWER_PALADIN_SHIELD.title");
    public override LocString Description => new("powers", "OTHERWORLD_TREASURES_POWER_PALADIN_SHIELD.description");
    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://OtherworldTreasures/images/powers/paladin_shield_64.png",
        BigIconPath: "res://OtherworldTreasures/images/powers/paladin_shield.png");

    internal void FlashProtection() => Flash();

    // Previews use the normal pure hook. The committed Damage adapter runs
    // after every native HP guard so it can transfer exactly what was saved.
    public override decimal ModifyHpLostAfterOstyLate(Creature target, decimal amount,
        ValueProp props, Creature? dealer, MegaCrit.Sts2.Core.Models.CardModel? cardSource) =>
        target == Owner && !PaladinShieldProtection.IsCommittingNativeHpModifiers
            ? amount - PaladinShieldProtection.SharedAmount(target, amount, props)
            : amount;
    public override Task AfterCurrentHpChanged(Creature creature, decimal delta) =>
        creature.Player != null ? PaladinShieldProtection.Refresh(Owner.CombatState) : Task.CompletedTask;
    public override Task AfterDeath(PlayerChoiceContext context, Creature creature, bool wasRemovalPrevented, float deathAnimLength) =>
        PaladinShieldProtection.Refresh(Owner.CombatState);
    public override Task AfterCombatEnd(CombatRoom room) => PowerCmd.Remove(this);
}
