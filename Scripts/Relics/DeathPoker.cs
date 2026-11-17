using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using OtherworldTreasures.Scripts.Cards;
using OtherworldTreasures.Scripts.Poker;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Combat.HandSize;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Relics;

[RegisterRelic(typeof(SharedRelicPool))]
public sealed class DeathPoker : ModRelicTemplate, IMaxHandSizeModifier
{
    public const string PileImage = "res://OtherworldTreasures/images/piles/death_poker.png";
    private string _savedCards = "";
    // Native string SavedProperty preserves order, duplicate faces and per-owner state.
    [SavedProperty] public string SavedCards {
        get => _savedCards;
        set { AssertMutable(); _savedCards = string.Join(",", Decode(value).Select(c => c.Value)); InvokeDisplayAmountChanged(); }
    }
    public IReadOnlyList<PokerFace> Cards => Decode(_savedCards);
    public int ModifyMaxHandSize(Player player, int currentMaxHandSize) => player == Owner ? currentMaxHandSize + 1 : currentMaxHandSize;
    public override RelicRarity Rarity => RelicRarity.Event;
    #if STS2_113
    protected override bool IsAllowedInternal(IRunState runState)
    #else
    public override bool IsAllowed(IRunState runState)
    #endif
        => DevelopmentContent.Extras;
    public override bool IsAllowedInShops => DevelopmentContent.Extras;
    public override bool ShowCounter => true;
    public override int DisplayAmount => Cards.Count;
    public override RelicAssetProfile AssetProfile => new(IconPath: PileImage, IconOutlinePath: PileImage, BigIconPath: PileImage);

    private static PokerFace[] Decode(string? value) => (value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
        .Select(s => int.TryParse(s, out int n) ? n : -1).Where(n => n is >= 0 and < 54).Take(54).Select(n => new PokerFace(n)).ToArray();
    public void Generate(int amount)
    {
        AssertMutable();
        var cards = Cards.ToList();
        for (int i = 0; i < amount && cards.Count < 54; i++) {
            int roll = Owner.RunState.Rng.CombatCardGeneration.NextInt(100);
            cards.Add(new PokerFace(roll == 0 ? 52 : roll == 1 ? 53 : Owner.RunState.Rng.CombatCardGeneration.NextInt(52)));
        }
        SavedCards = string.Join(",", cards.Select(c => c.Value));
    }
    public PokerFace[] Consume(IEnumerable<int> indexes)
    {
        AssertMutable();
        var cards = Cards.ToList(); var selected = indexes.Order().ToArray();
        if (selected.Length != selected.Distinct().Count() || selected.Any(i => i < 0 || i >= cards.Count)) throw new InvalidOperationException("Invalid poker inventory selection");
        var result = selected.Select(i => cards[i]).ToArray();
        foreach (int index in selected.Reverse()) cards.RemoveAt(index);
        SavedCards = string.Join(",", cards.Select(c => c.Value));
        return result;
    }
    public int[] RandomIndexes(int count)
    {
        var available = Enumerable.Range(0, Cards.Count).ToList(); var selected = new List<int>();
        if (available.Count < count) return [];
        for (int i = 0; i < count; i++) { int slot = Owner.RunState.Rng.CombatCardSelection.NextInt(available.Count); selected.Add(available[slot]); available.RemoveAt(slot); }
        return selected.Order().ToArray();
    }
    public override async Task BeforeCombatStartLate()
    {
        Generate(5); Flash();
        await CardPileCmd.AddToCombatAndPreview<FlyingCards>(Owner.Creature, PileType.Hand, 1, Owner);
    }
    public override Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Combat.ICombatState combatState)
    {
        if (player == Owner && player.PlayerCombatState?.TurnNumber >= 2) { Generate(2); Flash(); }
        return Task.CompletedTask;
    }
}
