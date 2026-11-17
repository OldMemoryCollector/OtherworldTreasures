using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Konoe;

[RegisterCard(typeof(KonoeCardPool))]
public sealed class KonoeStrike : ModCardTemplate
{
    public KonoeStrike() : base(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy, DevelopmentContent.Nine) { }
    protected override HashSet<CardTag> CanonicalTags => [CardTag.Strike];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6, ValueProp.Move)];
    public override bool CanBeGeneratedByModifiers => false;
    public override CardAssetProfile AssetProfile => KonoeCardArt.Profile;
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, play).Targeting(play.Target!)
            .WithHitFx("vfx/vfx_attack_slash").Execute(context);
    }
}

[RegisterCard(typeof(KonoeCardPool))]
public sealed class KonoeDefend : ModCardTemplate
{
    public KonoeDefend() : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self, DevelopmentContent.Nine) { }
    public override bool GainsBlock => true;
    protected override HashSet<CardTag> CanonicalTags => [CardTag.Defend];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(5, ValueProp.Move)];
    public override bool CanBeGeneratedByModifiers => false;
    public override CardAssetProfile AssetProfile => KonoeCardArt.Profile;
    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3);
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play) => await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);
}
