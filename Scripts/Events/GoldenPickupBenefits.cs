using System.Text.Json;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using STS2RitsuLib.Utils;

namespace OtherworldTreasures.Scripts.Events;

// Only the confirmation path for an owned relic calls this. Native acquisition
// still grants the full enhanced values; previews, clones and save loading do
// not trigger a second pickup. Saved progress is copied with the coating mark.
internal static class GoldenPickupBenefits
{
    internal readonly record struct Counts(int Slots, int Potions);
    private sealed record Progress(int FirstExtraSlot, int SlotsGranted, int PotionAttempts);
    private static readonly SavedAttachedState<RelicModel, string> Granted =
        new("OtherworldGoldenPickupDeltaV1", () => "");

    internal static Counts Capture(RelicModel relic) => relic switch
    {
        PotionBelt => new(relic.DynamicVars["PotionSlots"].IntValue, 0),
        PhialHolster => new(relic.DynamicVars["PotionSlots"].IntValue, relic.DynamicVars["Potions"].IntValue),
        AlchemicalCoffer => new(relic.DynamicVars["PotionSlots"].IntValue, relic.DynamicVars["PotionSlots"].IntValue),
        _ => default,
    };

    internal static async Task GrantDifference(RelicModel relic, Counts before)
    {
        if (relic is not (PotionBelt or PhialHolster or AlchemicalCoffer) ||
            !GoldenRelics.IsGolden(relic) || relic.Owner is not { } owner ||
            !owner.Relics.Any(owned => ReferenceEquals(owned, relic)) ||
            Granted.TryGetValue(relic, out var granted) && granted.Length > 0) return;

        var after = Capture(relic);
        int slots = Math.Max(0, after.Slots - before.Slots);
        int potions = Math.Max(0, after.Potions - before.Potions);
        int firstExtraSlot = owner.MaxPotionCount;
        await PlayerCmd.GainMaxPotionCount(slots, owner);
        var progress = new Progress(firstExtraSlot, slots, 0);
        Granted[relic] = JsonSerializer.Serialize(progress);

        // Match native pickup rules: Coffer fills its newly created slots;
        // Holster tries ordinary free slots, and full/forbidden attempts finish
        // normally rather than being retried after a reload or another click.
        foreach (var potion in PotionFactory.CreateRandomPotionsOutOfCombat(owner, potions,
            owner.RunState.Rng.CombatPotionGeneration))
        {
            int index = progress.PotionAttempts;
            progress = progress with { PotionAttempts = index + 1 };
            // TryToProcure adds the potion before awaiting other mods' hooks.
            // Settle this attempt first so a throwing hook cannot duplicate it.
            Granted[relic] = JsonSerializer.Serialize(progress);
            try
            {
                await PotionCmd.TryToProcure(potion.ToMutable(), owner,
                    relic is AlchemicalCoffer ? firstExtraSlot + index : -1);
            }
            catch (Exception error)
            {
                Entry.Logger.Warn($"[GoldenRelics] Pickup delta potion attempt failed for {relic.Id}: {error.Message}");
            }
        }
    }

    internal static void CopyMark(RelicModel original, RelicModel clone)
    {
        if (Granted.TryGetValue(original, out var value) && value.Length > 0) Granted[clone] = value;
    }
}
