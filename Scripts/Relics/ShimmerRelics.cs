using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Saves.Runs;
using OtherworldTreasures.Scripts.Events;
using OtherworldTreasures.Scripts.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Relics;

public abstract class CountedShimmerRelic : ModRelicTemplate
{
    private int _copies = 1;
    [SavedProperty]
    public int Copies
    {
        get => _copies;
        set { AssertMutable(); _copies = Math.Max(1, value); RefreshTotals(); InvokeDisplayAmountChanged(); }
    }
    public override RelicRarity Rarity => RelicRarity.Event;
    public override bool IsStackable => true;
    public override bool ShowCounter => IsMutable && Copies > 1;
    public override int DisplayAmount => Copies;
    public override RelicAssetProfile AssetProfile => new(
        IconPath: $"res://OtherworldTreasures/images/relics/{GetType().Name}_85.png",
        IconOutlinePath: $"res://OtherworldTreasures/images/relics/{GetType().Name}_85.png",
        BigIconPath: $"res://OtherworldTreasures/images/relics/{GetType().Name}.png");

    public override async Task AfterObtained()
    {
        await ApplyAcquisitionEffect();
        var existing = Owner.Relics.OfType<CountedShimmerRelic>().FirstOrDefault(r => r != this && r.GetType() == GetType());
        if (existing != null)
        {
            existing.Copies += Copies;
            await RelicCmd.Remove(this);
        }
        else { RefreshTotals(); InvokeDisplayAmountChanged(); }
    }
    protected virtual void RefreshTotals() { }
    protected virtual Task ApplyAcquisitionEffect() => Task.CompletedTask;
    public async Task ConsumeOne()
    {
        if (Copies > 1) Copies--;
        else await RelicCmd.Remove(this);
    }
}

[RegisterRelic(typeof(EventRelicPool))]
public sealed class LifeCrystal : CountedShimmerRelic
{
    protected override Task ApplyAcquisitionEffect() => CreatureCmd.GainMaxHp(Owner.Creature, 10m * Copies);
    public override EventModel ModifyNextEvent(EventModel currentEvent) => ShimmerLake.BoostEncounter(Owner, currentEvent);
}

[RegisterRelic(typeof(EventRelicPool))]
public sealed class LifeFruit : CountedShimmerRelic
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Absorption", 5m)];
    protected override void RefreshTotals() => DynamicVars["Absorption"].BaseValue = 5m * Copies;
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => base.AdditionalHoverTips.Append(HoverTipFactory.FromPower<TemporaryHealthPower>(5 * (IsMutable ? Copies : 1)));
    public override async Task BeforeCombatStart()
    {
        Flash();
        await PowerCmd.Apply<TemporaryHealthPower>(new ThrowingPlayerChoiceContext(), Owner.Creature, 5m * Copies, Owner.Creature, null);
    }
    public override EventModel ModifyNextEvent(EventModel currentEvent) => ShimmerLake.BoostEncounter(Owner, currentEvent);
}

[RegisterRelic(typeof(EventRelicPool))]
public sealed class AegisFruit : CountedShimmerRelic
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Metallicize", 4m)];
    protected override void RefreshTotals() => DynamicVars["Metallicize"].BaseValue = 4m * Copies;
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => base.AdditionalHoverTips.Append(HoverTipFactory.FromPower<ShimmerMetallicizePower>(4 * (IsMutable ? Copies : 1)));
    public override async Task BeforeCombatStart()
    {
        Flash();
        await PowerCmd.Apply<ShimmerMetallicizePower>(new ThrowingPlayerChoiceContext(), Owner.Creature, 4m * Copies, Owner.Creature, null);
    }
}

[RegisterRelic(typeof(EventRelicPool))]
public sealed class VitalCrystal : CountedShimmerRelic
{
    protected override async Task ApplyAcquisitionEffect()
    {
        for (int i = 0; i < Copies; i++)
            await CreatureCmd.GainMaxHp(Owner.Creature, Math.Max(1m, decimal.Floor(Owner.Creature.MaxHp * .2m)));
    }
}
