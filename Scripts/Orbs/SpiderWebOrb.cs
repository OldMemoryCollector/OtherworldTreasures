using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using OtherworldTreasures.Scripts.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Spider;

[RegisterOrb]
public sealed class SpiderWebOrb : ModOrbTemplate
{
    public override decimal PassiveVal => ModifyOrbValue(3m);
    public override decimal EvokeVal => ModifyOrbValue(6m);
    public override bool AllowInRandomOrbPool => false;
    public override OrbAssetProfile AssetProfile => new(
        IconPath: "res://images/orbs/frost_orb.png",
        VisualsScenePath: "res://scenes/orbs/orb_visuals/frost_orb.tscn");
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<WebbedPower>()];
    public override Task BeforeTurnEndOrbTrigger(PlayerChoiceContext context) => TriggerPassive(context, null);
    public override async Task Passive(PlayerChoiceContext context, Creature? target)
    {
        var enemies = CombatState.HittableEnemies;
        if (enemies.Count == 0) return;
        ActivatePassive();
        target ??= Owner.RunState.Rng.CombatTargets.NextItem(enemies);
        if (target == null) return;
        await PowerCmd.Apply<WebbedPower>(context, target, PassiveVal, Owner.Creature, null);
    }
    public override async Task<IEnumerable<Creature>> Evoke(PlayerChoiceContext context)
    {
        var enemies = CombatState.HittableEnemies;
        if (enemies.Count == 0) return [];
        var target = Owner.RunState.Rng.CombatTargets.NextItem(enemies);
        if (target == null) return [];
        ActivateEvoke([target]);
        await PowerCmd.Apply<WebbedPower>(context, target, EvokeVal, Owner.Creature, null);
        return [target];
    }
}
