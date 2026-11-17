using System.Text.Json.Nodes;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models.Capabilities;

namespace OtherworldTreasures.Scripts.Konoe;

// Independent of the card's native enchantment slot; only the combat copy is modified.
[RegisterModelCapability]
public sealed class KonoeElementInfusionCapability : UntilCombatEndCapability<CardModel>, ICardHoverTipContributor
{
    private List<KonoeElement> _elements = [];
    private HashSet<int> _untilPlayedIndices = [];
    public bool HasTemporaryElements => _untilPlayedIndices.Count > 0;
    public IReadOnlyList<KonoeElement> Elements => _elements.AsReadOnly();
    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _elements = [.. _elements];
        _untilPlayedIndices = [.. _untilPlayedIndices];
    }
    public void Add(KonoeElement element)
    {
        AssertMutable();
        if (element is < KonoeElement.Water or > KonoeElement.Fire) throw new ArgumentOutOfRangeException(nameof(element));
        _elements.Add(element);
        MarkDirty();
    }
    public void AddUntilPlayed(KonoeElement element)
    {
        Add(element);
        _untilPlayedIndices.Add(_elements.Count - 1);
        MarkDirty();
    }
    internal void ConsumeTemporaryElements()
    {
        AssertMutable();
        if (_untilPlayedIndices.Count == 0) return;
        foreach (int index in _untilPlayedIndices.OrderDescending()) _elements.RemoveAt(index);
        _untilPlayedIndices.Clear();
        MarkDirty();
    }
    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
    {
        if (Owner == null || play.Card != Owner || play.Card.Type != CardType.Attack) return;
        bool combination = play.Card is KonoeBlackStone { LastPlayWasCombination: true } or KonoeAbyssalFormula { LastPlayWasCombination: true };
        if (!combination)
            foreach (var element in _elements.ToArray()) await KonoeElements.Gain(context, play.Player, element, play.Card);
        // Replays are one card-use series. Keep the type through every hit/replay.
        if (play.IsLastInSeries && HasTemporaryElements)
        {
            ConsumeTemporaryElements();
            KonoeElements.Refresh(play.Player);
        }
    }
    public IEnumerable<IHoverTip> GetHoverTips(CardModel card)
    {
        if (_elements.Count == 0) yield break;
        foreach (var tip in KonoeSpellTips.Elements(KonoeDamage.FromElements(_elements))) yield return tip;
        if (HasTemporaryElements) yield return new HoverTip(new LocString("static_hover_tips", "KONOE_TEMPORARY_INFUSION.title"), new LocString("static_hover_tips", "KONOE_TEMPORARY_INFUSION.description"));
    }
    protected override JsonNode? SaveAdditionalState() => new JsonObject
    {
        ["elements"] = new JsonArray(_elements.Select(e => (JsonNode?)JsonValue.Create((int)e)).ToArray()),
        ["untilPlayed"] = new JsonArray(_untilPlayedIndices.Order().Select(i => (JsonNode?)JsonValue.Create(i)).ToArray())
    };
    protected override void LoadAdditionalState(JsonNode? state, int schemaVersion)
    {
        // 0.1.85 stored only a permanent-element array.
        var array = state as JsonArray ?? (state as JsonObject)?["elements"] as JsonArray;
        _elements = array?.Select(e => (KonoeElement)e!.GetValue<int>()).ToList() ?? [];
        var temporary = (state as JsonObject)?["untilPlayed"] as JsonArray;
        _untilPlayedIndices = temporary?.Select(e => e!.GetValue<int>()).Where(i => i >= 0 && i < _elements.Count).ToHashSet() ?? [];
    }
}
