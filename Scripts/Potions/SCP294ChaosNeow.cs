using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;

namespace OtherworldTreasures.Scripts.Potions;

[HarmonyPatch(typeof(Neow), "GenerateInitialOptions")]
internal static class Scp294ChaosNeowChoices
{
    private static bool Prefix(Neow __instance, ref IReadOnlyList<EventOption> __result)
    {
        if (__instance.Owner is not { } owner || !Scp294ChaosRules.For(owner.RunState).NeowRelics) return true;
        RelicModel[] relics = [ModelDb.Relic<WhiteBeastStatue>().ToMutable(), ModelDb.Relic<PotionBelt>().ToMutable(), ModelDb.Relic<PhialHolster>().ToMutable()];
        foreach (var relic in relics) relic.Owner = owner;
        __result = [new EventOption(__instance, async () =>
        {
            foreach (var relic in relics) await RelicCmd.Obtain(relic, owner);
            AccessTools.Method(typeof(AncientEventModel), "Done").Invoke(__instance, null);
        }, new LocString("events", "OTHERWORLD_TREASURES_CRAZY_NEOW.title"),
           new LocString("events", "OTHERWORLD_TREASURES_CRAZY_NEOW.description"),
           "NEOW.pages.INITIAL.options.OTHERWORLD_TREASURES_CRAZY_POTIONS", relics.SelectMany(r => r.HoverTipsExcludingRelic))];
        return false;
    }
}
