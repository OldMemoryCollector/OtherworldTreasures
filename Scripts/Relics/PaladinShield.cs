using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Runs;
using OtherworldTreasures.Scripts.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Relics;

[RegisterRelic(typeof(SharedRelicPool))]
public sealed class PaladinShield : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(6m, ValueProp.Unpowered), new DynamicVar("BlockPerTeammate", 3m)];
    public override bool IsAllowed(IRunState runState) => runState.Players.Count > 1;
    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://OtherworldTreasures/images/relics/PaladinShield_85.png",
        IconOutlinePath: "res://OtherworldTreasures/images/relics/PaladinShield_85.png",
        BigIconPath: "res://OtherworldTreasures/images/relics/PaladinShield.png");
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        base.AdditionalHoverTips.Append(HoverTipFactory.FromPower<PaladinShieldPower>(25))
            .Append(HoverTipFactory.Static(StaticHoverTip.Block));

    public override async Task BeforeSideTurnEnd(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player || Owner.Creature.IsDead || !participants.Contains(Owner.Creature)) return;
        int teammates = Owner.RunState.Players.Count(player => player != Owner);
        decimal block = DynamicVars.Block.BaseValue + DynamicVars["BlockPerTeammate"].BaseValue * teammates;
        Flash();
        await CreatureCmd.GainBlock(Owner.Creature, block, ValueProp.Unpowered, null);
    }

    public override Task BeforeCombatStartLate() => PaladinShieldProtection.Refresh(Owner.Creature.CombatState);
    public override Task AfterObtained() => PaladinShieldProtection.Refresh(Owner.Creature.CombatState);
    public override Task AfterRemoved() => PaladinShieldProtection.Refresh(Owner.Creature.CombatState);
    public override Task AfterCurrentHpChanged(Creature creature, decimal delta) =>
        creature.Player != null ? PaladinShieldProtection.Refresh(Owner.Creature.CombatState) : Task.CompletedTask;
    public override Task AfterPlayerTurnStartEarly(PlayerChoiceContext context, Player player) =>
        player == Owner ? PaladinShieldProtection.Refresh(Owner.Creature.CombatState) : Task.CompletedTask;
    public override Task AfterDeath(PlayerChoiceContext context, Creature creature, bool wasRemovalPrevented, float deathAnimLength) =>
        PaladinShieldProtection.Refresh(Owner.Creature.CombatState);
}
