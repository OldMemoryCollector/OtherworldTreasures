using System.Reflection;
using System.Reflection.Emit;
using STS2RitsuLib.Scaffolding.Characters;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Runs;
using OtherworldTreasures.Scripts.Cards;
using OtherworldTreasures.Scripts.Konoe;
using MegaCrit.Sts2.Core.Nodes.Screens.RelicCollection;
using OtherworldTreasures.Scripts.Relics;

namespace OtherworldTreasures.Scripts;

// Keep in-development models registered for saves and explicit development grants.
// Normal acquisition is closed on the models; collection filtering is confined to the library.
internal static class ReleaseContent
{
    private static readonly HashSet<Type> HiddenRelics =
    [
        typeof(SubstituteDoll),
        typeof(IchaIchaParadise),
        typeof(NewEightGlasses),
        typeof(KaguraUmbrella),
        typeof(YorozuyaScooter),
        typeof(LakeToya),
        typeof(BellKnife),
        typeof(DeathPoker),
        typeof(SpiderMutation),
        typeof(KonoeSagesBook)
    ];

    private static readonly HashSet<Type> HiddenCards =
    [
        typeof(BellKnifeCard), typeof(BratSweepKickCard), typeof(ChickenWildGameCard),
        typeof(FlyingCards), typeof(LakeToyaCard), typeof(LingxiFingerCard),
        typeof(LingxiFingerGuard), typeof(LingxiFingerStrike), typeof(PokerPursuit),
        typeof(PokerShield), typeof(YakyukenCard), typeof(YakyukenLv10Card)
    ];

    // Namespace ownership avoids querying Pool while ModelDb is building its card/pool caches.
    internal static bool IsKonoeCard(CardModel card) =>
        card.GetType().Assembly == typeof(KonoeCardPool).Assembly &&
        card.GetType().Namespace == typeof(KonoeCardPool).Namespace;
    internal static bool IsHiddenCard(CardModel card) =>
        (IsKonoeCard(card) || card is SpiderCard || HiddenCards.Contains(card.GetType()))
        && !DevelopmentEnabled(card.GetType());

    internal static bool IsAvailableCard(CardModel card, IRunState runState) =>
        !IsHiddenCard(card) || (IsKonoeCard(card) &&
            runState.Players.Any(player => player.Character is Characters.Konoe));
    internal static IEnumerable<CardModel> AvailableCards(IEnumerable<CardModel> cards, IRunState runState) =>
        cards.Where(card => IsAvailableCard(card, runState));

    // Reuse the character's existing framework policy at callers the framework does not cover.
    internal static IEnumerable<CharacterModel> VisibleCharacters(IEnumerable<CharacterModel> characters) =>
        characters.Where(character => character.GetType().Assembly != typeof(ReleaseContent).Assembly
            || character is not IModCharacterVanillaSelectionPolicy policy || !policy.HideFromVanillaCharacterSelect);

    internal static bool DevelopmentEnabled(Type type)
    {
        if (type.Assembly != typeof(ReleaseContent).Assembly) return false;
        if (type.Namespace == typeof(KonoeCardPool).Namespace || type == typeof(Characters.Konoe)) return DevelopmentContent.Nine;
        if (typeof(SpiderCard).IsAssignableFrom(type) || type == typeof(SpiderMutation)) return DevelopmentContent.Spider;
        if (type == typeof(LakeToyaCard) || type == typeof(NewEightGlasses) || type == typeof(KaguraUmbrella)
            || type == typeof(YorozuyaScooter) || type == typeof(LakeToya)) return DevelopmentContent.Gintama;
        return DevelopmentContent.Extras;
    }

    internal static bool IsHiddenRelic(RelicModel relic) => HiddenRelics.Contains(relic.GetType()) && !DevelopmentEnabled(relic.GetType());
    internal static IEnumerable<RelicModel> VisibleRelics(IEnumerable<RelicModel> relics) => relics.Where(relic => !IsHiddenRelic(relic));
}

[HarmonyPatch(typeof(NRelicCollectionCategory), nameof(NRelicCollectionCategory.LoadRelics))]
internal static class UpcomingRelicCollectionFilter
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var getter = AccessTools.PropertyGetter(typeof(ModelDb), nameof(ModelDb.AllRelics));
        var filter = AccessTools.Method(typeof(ReleaseContent), nameof(ReleaseContent.VisibleRelics));
        int matches = 0;
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (!instruction.Calls(getter)) continue;
            yield return new CodeInstruction(OpCodes.Call, filter);
            matches++;
        }
        if (matches != 1) throw new InvalidOperationException("Relic collection filtering requires one ModelDb.AllRelics call.");
    }
}

// Native rewards, shops, combat generation and transformations share this filter.
// An existing Konoe run can still use its own pool; ordinary runs cannot roll it.
[HarmonyPatch(typeof(CardFactory),
#if STS2_113
    "FilterForRun"
#else
    "FilterForPlayerCount"
#endif
)]
internal static class UpcomingCardAcquisitionFilter
{
    private static void Postfix(IRunState runState, ref IEnumerable<CardModel> __result) =>
        __result = ReleaseContent.AvailableCards(__result, runState);
}

// RitsuLib handles standard selection and lobby random rolls. Custom selection (111/113)
// and the new 113 mode callers need the same list filter without changing registration.
[HarmonyPatch]
internal static class UpcomingAlternativeCharacterSelectionFilter
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(MegaCrit.Sts2.Core.Nodes.Screens.CustomRun.NCustomRunScreen), "InitCharacterButtons");
        yield return AccessTools.Method(typeof(MegaCrit.Sts2.Core.Nodes.Screens.DailyRun.NDailyRunScreen), "SetupLobbyParams");
#if STS2_113
        yield return AccessTools.Method(typeof(MegaCrit.Sts2.Core.Nodes.Screens.Blitz.NBlitzScreen), "InitCharacterButtons");
        yield return AccessTools.Method(typeof(MegaCrit.Sts2.Core.Nodes.Screens.Blitz.NBlitzScreen), "UpdateRandomCharacterVisibility");
        yield return AccessTools.Method(typeof(MegaCrit.Sts2.Core.Nodes.Screens.Dread.NDreadScreen), "InitCharacterButtons");
        yield return AccessTools.Method(typeof(MegaCrit.Sts2.Core.Helpers.AscensionHelper), "GetRotatingCharacterIdForStreak");
#endif
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        var getter = AccessTools.PropertyGetter(typeof(ModelDb), nameof(ModelDb.AllCharacters));
        var filter = AccessTools.Method(typeof(ReleaseContent), nameof(ReleaseContent.VisibleCharacters));
        int matches = 0;
        foreach (var instruction in instructions)
        {
            yield return instruction;
            if (!instruction.Calls(getter)) continue;
            yield return new CodeInstruction(OpCodes.Call, filter);
            matches++;
        }
        if (matches != 1) throw new InvalidOperationException($"Character selection filtering expected one character-list call in {__originalMethod.Name}, found {matches}.");
    }
}
