using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using OtherworldTreasures.Scripts.Cards;

namespace OtherworldTreasures.Scripts.UI;

// Library placement is independent of gameplay rarity and the Ancient card frame.
// Keep the existing predicates so vanilla and other mods retain their categories.
[HarmonyPatch(typeof(NCardLibrary), nameof(NCardLibrary._Ready))]
internal static class TrainingCardLibrary
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(NCardLibrary __instance,
        Dictionary<NCardPoolFilter, Func<CardModel, bool>> ____poolFilters,
        NCardPoolFilter ____miscPoolFilter, NCardPoolFilter ____ancientsFilter)
    {
        var misc = ____poolFilters[____miscPoolFilter];
        var ancients = ____poolFilters[____ancientsFilter];
        ____poolFilters[____miscPoolFilter] = card => IsDisplayedAsDerived(card) || misc(card);
        ____poolFilters[____ancientsFilter] = card => !IsDisplayedAsDerived(card) && ancients(card);
    }

    private static bool IsDisplayedAsDerived(CardModel card) =>
        card is MartialTrainingCard or DonateEnergy or MustDieCardBase;
}
