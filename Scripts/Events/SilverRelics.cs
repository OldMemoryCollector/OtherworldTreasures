using System.Runtime.CompilerServices;
using System.Text.Json;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using STS2RitsuLib.Utils;

namespace OtherworldTreasures.Scripts.Events;

// Opt in only turn requirements whose reduction is beneficial. A value named
// "Turns" could equally be a buff duration or a cooldown protecting the player.
public interface ISilverRelicAdapter
{
    IReadOnlyCollection<string> SilverTurnVariables { get; }
}

public static class SilverRelics
{
    private static readonly SavedAttachedState<RelicModel, string> Turns = new("OtherworldSilverTurnsV1", () => "");
    private sealed record ParsedTurns(string Json, Dictionary<string, decimal> Values);
    private static readonly ConditionalWeakTable<RelicModel, ParsedTurns> Parsed = new();

    public static bool IsSilver(RelicModel relic) => relic.IsMutable && Turns.TryGetValue(relic, out var value) && value.Length > 0;
    public static bool CanEnhance(RelicModel relic) => GoldenRelics.IsEligibleUncoated(relic) &&
        (Variables(relic).Count > 0 || SilverNativeTurns.OriginalTurn(relic) > 1);

    private static IReadOnlyCollection<string> Variables(RelicModel relic)
    {
        IReadOnlyCollection<string> keys = relic is ISilverRelicAdapter adapter ? adapter.SilverTurnVariables : relic switch
        {
            HappyFlower or FakeHappyFlower or Pendulum or PollinousCore => ["Turns"],
            StoneCalendar => ["DamageTurn"],
            _ => [],
        };
        return keys.Count > 0 && keys.All(k => relic.DynamicVars.TryGetValue(k, out var v) && v.BaseValue > 1 && decimal.Truncate(v.BaseValue) == v.BaseValue)
            ? keys : Array.Empty<string>();
    }

    public static bool Enhance(RelicModel relic)
    {
        if (!CanEnhance(relic)) return false;
        var originals = Variables(relic).ToDictionary(k => k, k => relic.DynamicVars[k].BaseValue);
        if (SilverNativeTurns.OriginalTurn(relic) > 1) originals["$nativeTurn"] = SilverNativeTurns.OriginalTurn(relic);
        Turns[relic] = JsonSerializer.Serialize(originals);
        Apply(relic, relic.DynamicVars);
        // Native modulo counters must not skip the next activation when a saved
        // counter already meets the newly shortened interval.
        switch (relic)
        {
            case HappyFlower flower: flower.TurnsSeen = Math.Min(flower.TurnsSeen, flower.DynamicVars["Turns"].IntValue - 1); break;
            case FakeHappyFlower flower: flower.TurnsSeen = Math.Min(flower.TurnsSeen, flower.DynamicVars["Turns"].IntValue - 1); break;
            case Pendulum pendulum: pendulum.TurnsSeen = Math.Min(pendulum.TurnsSeen, pendulum.DynamicVars["Turns"].IntValue - 1); break;
        }
        return true;
    }

    internal static void CopyMark(RelicModel original, RelicModel clone)
    {
        if (IsSilver(original)) Turns[clone] = Turns[original];
    }

    internal static void Apply(RelicModel relic, DynamicVarSet vars)
    {
        if (!IsSilver(relic)) return;
        string json = Turns[relic];
        if (!Parsed.TryGetValue(relic, out var cached) || cached.Json != json)
        {
            cached = new ParsedTurns(json, JsonSerializer.Deserialize<Dictionary<string, decimal>>(json)!);
            Parsed.Remove(relic);
            Parsed.Add(relic, cached);
        }
        foreach (var (key, original) in cached.Values)
            if (vars.TryGetValue(key, out var value)) value.BaseValue = Math.Max(1, original - 1);
    }
}
