using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using OtherworldTreasures.Scripts.Relics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Events;

[RegisterSharedEvent]
public sealed class ShimmerLake : OtherworldEvent
{
    internal const float ItemEncounterBonus = .05f;
    private bool _busy;
    private bool _finished;
    protected override bool AncientExempt => true;
    public override EventAssetProfile AssetProfile => new(
        InitialPortraitPath: "res://OtherworldTreasures/images/events/shimmer_lake.png");
    public override bool IsAllowed(IRunState run) => run.CurrentActIndex is 1 or 2 && !Visited(run, Id) &&
        (run is not RunState state || !state.VisitedEventIds.Contains(Id));

    internal static EventModel BoostEncounter(Player owner, EventModel current)
    {
        var lake = ModelDb.Event<ShimmerLake>();
        if (current is ShimmerLake or AncientEventModel || !lake.IsAllowed(owner.RunState)) return current;
        return owner.RunState.Rng.Niche.NextFloat() < ItemEncounterBonus ? lake : current;
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        new EventOption(this, HasIngredient ? ShowTransmutation : null, InitialOptionKey("TRANSMUTE")),
        new EventOption(this, Owner.Deck.Cards.Any(c => ShimmerCardActions.TransformCandidates(c).Count > 0) ? TransformCard : null,
            InitialOptionKey("TRANSFORM")),
        new EventOption(this, Donors().Any() ? DismantleCard : null, InitialOptionKey("DISMANTLE")),
        Leave()
    ];

    private bool HasIngredient => Owner.Relics.Any(r => r is LifeFruit) ||
        Owner.Creature.MaxHp > 10 && Owner.Relics.Any(r => r is LifeCrystal);
    private IEnumerable<CardModel> Donors() => Owner.Deck.Cards.Where(source =>
        Owner.Deck.Cards.Any(target => ShimmerCardActions.CanTransfer(source, target)));

    private Task ShowTransmutation()
    {
        if (_busy || _finished) return Task.CompletedTask;
        var options = new List<EventOption>
        {
            new(this, Owner.Relics.OfType<LifeCrystal>().Any() && Owner.Creature.MaxHp > 10 ? TransmuteCrystal : null,
                ModOptionKey("TRANSMUTATION", "CRYSTAL"), ModelDb.Relic<VitalCrystal>().HoverTips),
            new(this, Owner.Relics.OfType<LifeFruit>().Any() ? TransmuteFruit : null,
                ModOptionKey("TRANSMUTATION", "FRUIT"), ModelDb.Relic<AegisFruit>().HoverTips),
            new(this, ReturnToLake, ModOptionKey("TRANSMUTATION", "BACK"))
        };
        SetEventState(PageDescription("TRANSMUTATION"), options);
        return Task.CompletedTask;
    }

    private Task ReturnToLake()
    {
        SetEventState(PageDescription("INITIAL"), GenerateInitialOptions());
        return Task.CompletedTask;
    }

    internal async Task<bool> ConvertCrystal()
    {
        var crystal = Owner.Relics.OfType<LifeCrystal>().FirstOrDefault();
        if (crystal == null || Owner.Creature.MaxHp <= 10) return false;
        await crystal.ConsumeOne();
        await CreatureCmd.LoseMaxHp(new ThrowingPlayerChoiceContext(), Owner.Creature, 10m, false);
        await RelicCmd.Obtain<VitalCrystal>(Owner);
        return true;
    }
    internal async Task<bool> ConvertFruit()
    {
        var fruit = Owner.Relics.OfType<LifeFruit>().FirstOrDefault();
        if (fruit == null) return false;
        await fruit.ConsumeOne();
        await RelicCmd.Obtain<AegisFruit>(Owner);
        return true;
    }
    private Task TransmuteCrystal() => CompleteConversion(ConvertCrystal, "CRYSTAL");
    private Task TransmuteFruit() => CompleteConversion(ConvertFruit, "FRUIT");
    private async Task CompleteConversion(Func<Task<bool>> convert, string page)
    {
        if (_busy || _finished) return;
        _busy = true;
        try
        {
            if (!await convert()) { await ReturnToLake(); return; }
            _finished = true;
            SetEventFinished(PageDescription(page));
        }
        finally { _busy = false; }
    }

    private async Task TransformCard()
    {
        if (_busy || _finished) return;
        _busy = true;
        try
        {
            var selected = (await CardSelectCmd.FromDeckGeneric(Owner,
                new CardSelectorPrefs(CardSelectorPrefs.TransformSelectionPrompt, 1) { Cancelable = true },
                card => ShimmerCardActions.TransformCandidates(card).Count > 0)).FirstOrDefault();
            if (selected == null) { await ReturnToLake(); return; }
            if (await ShimmerCardActions.Transform(selected, Rng) == null) { await ReturnToLake(); return; }
            _finished = true;
            SetEventFinished(PageDescription("TRANSFORMED"));
        }
        finally { _busy = false; }
    }

    private async Task DismantleCard()
    {
        if (_busy || _finished) return;
        _busy = true;
        try
        {
            var donors = Donors().ToHashSet();
            var source = (await CardSelectCmd.FromDeckGeneric(Owner,
                new CardSelectorPrefs(new LocString("events", Id.Entry + ".selection.SOURCE"), 1) { Cancelable = true },
                donors.Contains)).FirstOrDefault();
            if (source == null) { await ReturnToLake(); return; }
            var target = (await CardSelectCmd.FromDeckGeneric(Owner,
                new CardSelectorPrefs(new LocString("events", Id.Entry + ".selection.TARGET"), 1) { Cancelable = true },
                card => ShimmerCardActions.CanTransfer(source, card))).FirstOrDefault();
            if (target == null || !ShimmerCardActions.Transfer(source, target)) { await ReturnToLake(); return; }
            CardCmd.Preview(target, 1.5f);
            _finished = true;
            SetEventFinished(PageDescription("DISMANTLED"));
        }
        finally { _busy = false; }
    }
}
