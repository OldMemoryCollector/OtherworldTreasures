using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using OtherworldTreasures.Scripts.Spider;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Powers;

[RegisterPower]
public sealed class WebbedPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerAssetProfile AssetProfile => new(IconPath: "res://images/powers/missing_power.png", BigIconPath: "res://images/powers/missing_power.png");
    // Invoked by the actual monster move, after SpiderVenom and before the native action pipeline.
    internal async Task CheckAction(PlayerChoiceContext context)
    {
        if (!Owner.IsAlive || Amount <= 0 || Owner.CombatState is not { } combat) return;
        bool failed = SpiderRules.Roll(Amount, combat.RunState.Rng.CombatTargets.NextFloat());
        Flash();
        await PowerCmd.Decrement(this);
        if (failed && Owner.IsAlive) await CreatureCmd.Stun(Owner);
    }
}
