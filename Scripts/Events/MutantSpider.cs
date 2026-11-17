using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Runs;
using OtherworldTreasures.Scripts.Cards;
using OtherworldTreasures.Scripts.Relics;
using STS2RitsuLib.Interop.AutoRegistration;

namespace OtherworldTreasures.Scripts.Events;

[RegisterSharedEvent]
public sealed class MutantSpider : OtherworldEvent
{
    protected override bool AncientExempt => true;
    private static bool Supports(Player player) => player.Character is Ironclad or Silent or Defect or Regent or Necrobinder;
    public override bool IsAllowed(IRunState run) => DevelopmentContent.Spider && base.IsAllowed(run) && run.Players.Any(Supports);

    internal static IReadOnlyList<CardModel> SpecialtyCards(Player player) => player.Character switch
    {
        Ironclad => [ModelDb.Card<SpiderWeb>(), ModelDb.Card<HangingByASilk>(), ModelDb.Card<SpiderEcho>()],
        Silent => [ModelDb.Card<SpiderFlyingBlades>(), ModelDb.Card<SpiderVenomInjection>(), ModelDb.Card<SpiderHunt>()],
        Defect => [ModelDb.Card<SpiderChannel>(), ModelDb.Card<SpiderStatusConversion>(), ModelDb.Card<SteelSpiderWeb>()],
        Necrobinder => [ModelDb.Card<SpiderEtherealMark>()],
        _ => []
    };
    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        new(this, Supports(Owner) && !Owner.Relics.OfType<SpiderMutation>().Any() ? Accept : null,
            InitialOptionKey("ACCEPT"), RewardTips()),
        Leave()
    ];
    private IEnumerable<IHoverTip> RewardTips() =>
        ModelDb.Relic<SpiderMutation>().HoverTips.Concat(SpecialtyCards(Owner).SelectMany(card => card.HoverTips));
    private Task Accept() => Resolve(async () =>
    {
        if (Owner.Relics.OfType<SpiderMutation>().Any()) return;
        await RelicCmd.Obtain<SpiderMutation>(Owner);
        await CreatureCmd.GainMaxHp(Owner.Creature, 20);
        SpiderMutation.MutateBasics(Owner);
        foreach (var canonical in SpecialtyCards(Owner))
        {
            var card = Owner.RunState.CreateCard(canonical, Owner);
            await CardPileCmd.Add(card, PileType.Deck);
        }
    }, "ACCEPTED");
}
