using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using OtherworldTreasures.Scripts.Cards;

namespace OtherworldTreasures.Scripts.Events;

[RegisterSharedEvent]
public sealed class GoldenAxe : OtherworldEvent
{
    public override EventAssetProfile AssetProfile => new(
        InitialPortraitPath: "res://OtherworldTreasures/images/events/golden_axe.png");
    private CardModel? _card;
    private RelicModel? _relic;
    private IReadOnlyList<EventOption>? _options;
    private bool _dropPrepared;
    private bool _silver;
    private string _droppedName = "";
    private static bool CanEnhanceRelic(RelicModel relic) => GoldenRelics.CanEnhance(relic) || SilverRelics.CanEnhance(relic);
    internal static IReadOnlyList<RelicModel> EligibleRelics(IEnumerable<RelicModel> owned)
    {
        var eligible = owned.Where(CanEnhanceRelic).ToList();
        var regular = eligible.Where(r => r.Rarity != RelicRarity.Starter).ToList();
        return regular.Count > 0 ? regular : eligible;
    }
    private static IEnumerable<CardModel> Cards(Player player) => player.Deck.Cards.Where(c =>
        c.IsRemovable && c.Rarity is CardRarity.Basic or CardRarity.Common &&
        c.Type is CardType.Attack or CardType.Skill or CardType.Power);
    public override bool IsAllowed(IRunState run) => base.IsAllowed(run) &&
        run.Players.Any(p => Cards(p).Any() || EligibleRelics(p.Relics).Count > 0);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        if (_options != null) return _options;
        PrepareDrop();
        if (_relic == null && _card == null) return _options = [Leave()];
        string page = _relic != null ? _silver ? "INITIAL_SILVER" : "INITIAL_RELIC" : "INITIAL_CARD";
        RelicModel? goldenPreview = null;
        if (_relic != null)
        {
            goldenPreview = (RelicModel)_relic.ClonePreservingMutability();
            if (!(_silver ? SilverRelics.Enhance(goldenPreview) : GoldenRelics.Enhance(goldenPreview))) goldenPreview = null;
        }
        _options = new[] { "HONEST", "DISHONEST", "LEAVE" }.Select(choice =>
        {
            string key = ModOptionKey(page, choice);
            var description = new LocString(LocTable, "OTHERWORLD_TREASURES_GOLDEN_DROPPED_OPTION.description");
            description.Add("Description", GetOptionDescription(key)!);
            description.Add("Item", _droppedName);
            var preview = choice == "HONEST" ? goldenPreview ?? _relic : _relic;
            var tips = preview?.HoverTips ?? new[] { HoverTipFactory.FromCard(_card!) };
            var option = new EventOption(this, () => Resolve(() => Choose(choice), choice),
                GetOptionTitle(key)!, description, key, tips);
            if (preview != null) option.WithRelic(preview);
            return option;
        }).ToArray();
        return _options;
    }

    // Some event launchers/UI request the description before creating buttons.
    // Select once from the event's seeded RNG, independent of that read order.
    private void PrepareDrop()
    {
        if (_dropPrepared) return;
        var relics = EligibleRelics(Owner.Relics);
        var cards = Cards(Owner).ToList();
        if (relics.Count > 0 && (cards.Count == 0 || Rng.NextInt(2) == 0))
            _relic = relics[Rng.NextInt(relics.Count)];
        else if (cards.Count > 0) _card = cards[Rng.NextInt(cards.Count)];
        if (_relic != null)
            _silver = SilverRelics.CanEnhance(_relic) && (!GoldenRelics.CanEnhance(_relic) || Rng.NextInt(2) == 0);
        _droppedName = _relic?.Title.GetFormattedText() ?? _card?.Title ?? "";
        _dropPrepared = true;
        Entry.Logger.Info($"[GoldenAxe] Drop={(_relic?.Id ?? _card?.Id)?.ToString() ?? "none"}; eligibleRelics={relics.Count}, eligibleCards={cards.Count}");
    }

    public override LocString InitialDescription
    {
        get
        {
            if (!IsMutable || ((EventModel)this).Owner == null || Rng == null) return PageDescription("PREVIEW");
            PrepareDrop();
            var text = PageDescription(_relic != null ? _silver ? "INITIAL_SILVER" : "INITIAL_RELIC" : _card != null ? "INITIAL_CARD" : "UNAVAILABLE");
            text.Add("Item", _droppedName);
            return text;
        }
    }

    private async Task Choose(string choice)
    {
        if (_relic != null && Owner.Relics.Contains(_relic))
        {
            if (choice == "HONEST" && (_silver ? SilverRelics.Enhance(_relic) : GoldenRelics.Enhance(_relic)))
            {
                _relic.Flash();
                Entry.Logger.Info($"[GoldenAxe] Enhanced owned relic {_relic.Id}");
            }
            else if (choice == "LEAVE") await RelicCmd.Remove(_relic);
        }
        if (_card != null && Owner.Deck.Cards.Contains(_card) && choice != "DISHONEST")
        {
            await CardPileCmd.RemoveFromDeck(_card);
            if (choice == "HONEST")
                foreach (var rarity in new[] { CardRarity.Rare, CardRarity.Uncommon })
                {
                    var options = CardCreationOptions.ForNonCombatWithUniformOdds([Owner.Character.CardPool],
                        c => c.Rarity == rarity && c is not IDerivedCard).WithRngOverride(Rng);
                    foreach (var result in CardFactory.CreateForReward(Owner, 1, options))
                        await CardPileCmd.Add(result.Card, PileType.Deck);
                }
        }
        if (choice == "DISHONEST") await CardPileCmd.AddCurseToDeck<Guilty>(Owner);
    }
}
