using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using OtherworldTreasures.Scripts.Spider;
using STS2RitsuLib.Combat.AttackHits;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Powers;

[RegisterPower]
public sealed class DodgePower : ModPowerTemplate, IAttackHitHookListener
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerAssetProfile AssetProfile => new(IconPath: "res://images/powers/missing_power.png", BigIconPath: "res://images/powers/missing_power.png");
    public async Task BeforeAttackHit(AttackHitContext context)
    {
        var owner = Owner;
        if (owner.CombatState is not { } combat || Amount <= 0 || context.Dealer?.Side != CombatSide.Enemy || owner.Side == CombatSide.Enemy || !context.Targets.Contains(owner)) return;
        // One synchronized roll per segment, even if Block would absorb its entire damage.
        bool evaded = SpiderRules.Roll(Amount, combat.RunState.Rng.CombatTargets.NextFloat());
        SpiderMoveScope.Record(context.Dealer, owner, evaded ? context.Attack : null);
        if (evaded) { Flash(); context.Targets = context.Targets.Where(target => target != owner).ToArray(); }
        await PowerCmd.Decrement(this);
    }
    public override Task AfterSideTurnEnd(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants) =>
        side == CombatSide.Enemy ? PowerCmd.Remove(this) : Task.CompletedTask;
}
