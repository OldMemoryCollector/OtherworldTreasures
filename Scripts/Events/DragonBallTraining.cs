using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using OtherworldTreasures.Scripts.Cards;
using OtherworldTreasures.Scripts.Relics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Events;

[RegisterSharedEvent]
public sealed class DragonBallTraining : OtherworldEvent
{
    public override EventAssetProfile AssetProfile => new(
        InitialPortraitPath: "res://OtherworldTreasures/images/events/dragon_ball_training.png");
    private IReadOnlyList<EventOption>? _options;
    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        if (_options != null) return _options;
        var skills = new List<(string Mentor, CardModel Skill)>
        {
            ("KRILLIN", ModelDb.Card<SolarFlare>()),
            ("PICCOLO", ModelDb.Card<SpecialBeamCannon>()),
            ("KING_KAI", Owner.RunState.Players.Count > 1 && Rng.NextInt(2) == 0 ? ModelDb.Card<SpiritBomb>() : ModelDb.Card<Kaioken>())
        };
        // Fisher-Yates uses only this event's seeded stream.
        for (int i = skills.Count - 1; i > 0; i--) { int j = Rng.NextInt(i + 1); (skills[i], skills[j]) = (skills[j], skills[i]); }
        var options = new List<EventOption>();
        foreach (var (mentor, canonical) in skills)
        {
            var key = InitialOptionKey(mentor);
            var description = GetOptionDescription(key)!;
            description.Add("Skill", canonical.Title);
            options.Add(new EventOption(this, () => Resolve(async () =>
            {
                var learned = Owner.RunState.CreateCard(canonical, Owner);
                await CardPileCmd.Add(learned, PileType.Deck);
                if (!Owner.Relics.OfType<CombatScouter>().Any()) await RelicCmd.Obtain<CombatScouter>(Owner);
            }, "LEARNED"), GetOptionTitle(key)!, description, key,
                [HoverTipFactory.FromCard(canonical)]));
        }
        _options = options;
        return _options;
    }
}
