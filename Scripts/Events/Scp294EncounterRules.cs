using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace OtherworldTreasures.Scripts.Events;

internal static class Scp294EncounterRules
{
    internal static int PriorVisits(IRunState run, ModelId id) => run.MapPointHistory
        .Take(Math.Max(0, run.CurrentActIndex))
        .Count(act => act.Any(point => point.Rooms.Any(room => room.ModelId == id)));

    internal static bool VisitedThisAct(IRunState run, ModelId id) =>
        run.CurrentActIndex >= 0 && run.CurrentActIndex < run.MapPointHistory.Count &&
        run.MapPointHistory[run.CurrentActIndex].Any(point => point.Rooms.Any(room => room.ModelId == id));

    internal static double RetentionChance(int previousVisits) => Math.Pow(.5, Math.Max(0, previousVisits));

    internal static bool IsAllowed(IRunState run, ModelId id)
    {
        if (VisitedThisAct(run, id)) return false;
        int previous = PriorVisits(run, id);
        if (previous == 0) return true;
        // A dedicated seeded roll is fixed for this act. Eligibility checks do not advance shared RNG,
        // cannot reroll on each question-mark room, and agree across peers and save/reload.
        var roll = new Rng(run.Rng.Seed, $"{id.Entry}:act:{run.CurrentActIndex}:visits:{previous}");
        return roll.NextFloat() < RetentionChance(previous);
    }
}

// Native RoomSet rejects every globally visited event, even after IsAllowed approves a later act.
// Recover only an eligible coffee machine that the native scan skipped before its chosen event.
// The visited set and native handling of every other event stay intact.
[HarmonyPatch(typeof(RoomSet), nameof(RoomSet.EnsureNextEventIsValid))]
internal static class Scp294RepeatEncounterPatch
{
    private static void Prefix(RoomSet __instance, out int __state) => __state = __instance.eventsVisited;

    private static void Postfix(RoomSet __instance, RunState runState, int __state)
    {
        int count = __instance.events.Count;
        if (count == 0) return;
        int skipped = Math.Min(count, Math.Max(0, __instance.eventsVisited - __state));
        for (int step = 0; step < skipped; step++)
        {
            int cursor = __state + step;
            if (__instance.events[cursor % count] is Scp294 coffee && coffee.IsAllowed(runState))
            {
                __instance.eventsVisited = cursor;
                return;
            }
        }
        // Native exhaustion fallback permits repetition. Never let that bypass the per-act coffee cap.
        for (int step = 0; step < count && __instance.NextEvent is Scp294 coffee && !coffee.IsAllowed(runState); step++)
            __instance.eventsVisited++;
    }
}
