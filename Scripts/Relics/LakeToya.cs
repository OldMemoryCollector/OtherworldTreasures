using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Runs;
using OtherworldTreasures.Scripts.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Relics;

[RegisterRelic(typeof(SharedRelicPool))]
public sealed class LakeToya : ModRelicTemplate
{
    private const string PlaceholderIcon = "res://images/powers/missing_power.png";
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        base.AdditionalHoverTips.Concat(HoverTipFactory.FromCardWithCardHoverTips<LakeToyaCard>());
    public override RelicRarity Rarity => RelicRarity.Event;
    #if STS2_113
    protected override bool IsAllowedInternal(IRunState runState)
    #else
    public override bool IsAllowed(IRunState runState)
    #endif
        => DevelopmentContent.Gintama;
    public override bool IsAllowedInShops => DevelopmentContent.Gintama;
    public override RelicAssetProfile AssetProfile => new(IconPath: PlaceholderIcon, IconOutlinePath: PlaceholderIcon, BigIconPath: PlaceholderIcon);

    public override async Task BeforeCombatStartLate()
    {
        Flash();
        await CardPileCmd.AddToCombatAndPreview<LakeToyaCard>(Owner.Creature, PileType.Hand, 1, Owner);
    }
}
