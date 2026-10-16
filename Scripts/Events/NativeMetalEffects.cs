using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;

namespace OtherworldTreasures.Scripts.Events;

// These methods return only this relic's contribution. Do not intercept the
// global damage/power commands or multiply the combined result of other relics.
[HarmonyPatch]
internal static class GoldenNativeModifiers
{
    internal static bool Supports(RelicModel relic) => relic is
        PaperPhrog or PaperKrane or PenNib or Vambrace or VitruvianMinion or UnsettlingLamp;

    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(PaperPhrog), nameof(PaperPhrog.ModifyVulnerableMultiplier));
        yield return AccessTools.Method(typeof(PaperKrane), nameof(PaperKrane.ModifyWeakMultiplier));
        yield return AccessTools.Method(typeof(PenNib), nameof(PenNib.ModifyDamageMultiplicative));
        yield return AccessTools.Method(typeof(Vambrace), nameof(Vambrace.ModifyBlockMultiplicative));
        yield return AccessTools.Method(typeof(VitruvianMinion), nameof(VitruvianMinion.ModifyDamageMultiplicative));
        yield return AccessTools.Method(typeof(VitruvianMinion), nameof(VitruvianMinion.ModifyBlockMultiplicative));
        yield return AccessTools.Method(typeof(UnsettlingLamp), nameof(UnsettlingLamp.ModifyPowerAmountGivenMultiplicative));
    }

    private static void Prefix(object[] __args, out decimal __state) =>
        __state = __args.Length > 1 && __args[1] is decimal amount ? amount : 0m;

    private static void Postfix(RelicModel __instance, decimal __state, ref decimal __result)
    {
        if (!GoldenRelics.IsGolden(__instance)) return;
        decimal neutral = __instance is PaperPhrog or PaperKrane ? __state : 1m;
        __result = Math.Max(0m, neutral + 2m * (__result - neutral));
    }
}

// Patch only the literal next to TurnNumber in five reviewed native methods.
// Their normal pipeline (conditions, flash, reward and display events) stays intact.
[HarmonyPatch]
internal static class SilverNativeTurns
{
    internal static int OriginalTurn(RelicModel relic) => relic switch
    {
        Candelabra or HornCleat => 2,
        CaptainsWheel or Chandelier or SparklingRouge => 3,
        _ => 0,
    };

    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var (type, name) in new[]
        {
            (typeof(Candelabra), nameof(Candelabra.AfterSideTurnStart)),
            (typeof(Chandelier), nameof(Chandelier.AfterSideTurnStart)),
            (typeof(CaptainsWheel), nameof(CaptainsWheel.AfterBlockCleared)),
            (typeof(HornCleat), nameof(HornCleat.AfterBlockCleared)),
            (typeof(SparklingRouge), nameof(SparklingRouge.AfterBlockCleared)),
        })
        {
            var method = AccessTools.Method(type, name);
            var stateMachine = method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType
                ?? throw new InvalidOperationException($"The native turn method changed: {type.Name}.{name}");
            yield return AccessTools.Method(stateMachine, "MoveNext");
        }
    }

    private static int TriggerTurn(int original, RelicModel relic) =>
        SilverRelics.IsSilver(relic) ? Math.Max(1, original - 1) : original;

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        var code = instructions.ToList();
        var owner = __originalMethod.DeclaringType!.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(f => typeof(RelicModel).IsAssignableFrom(f.FieldType));
        bool changed = false;
        for (int i = 0; i < code.Count; i++)
        {
            yield return code[i];
            if (i == 0 || code[i - 1].operand is not MethodInfo getter || getter.Name != "get_TurnNumber" ||
                !(code[i].LoadsConstant(2) || code[i].LoadsConstant(3))) continue;
            yield return new CodeInstruction(OpCodes.Ldarg_0);
            yield return new CodeInstruction(OpCodes.Ldfld, owner);
            yield return CodeInstruction.Call(typeof(SilverNativeTurns), nameof(TriggerTurn));
            changed = true;
        }
        if (!changed) throw new InvalidOperationException($"Could not locate native turn requirement: {__originalMethod}");
    }
}

[HarmonyPatch(typeof(RelicModel), "Description", MethodType.Getter)]
internal static class GoldenNativeDescriptions
{
    private static void Postfix(RelicModel __instance, ref LocString __result)
    {
        if (GoldenRelics.IsGolden(__instance))
        {
            string? key = __instance switch
            {
                PaperPhrog => "PAPER_PHROG", PaperKrane => "PAPER_KRANE", PenNib => "PEN_NIB",
                Vambrace => "VAMBRACE", VitruvianMinion => "VITRUVIAN_MINION", UnsettlingLamp => "UNSETTLING_LAMP",
                MembershipCard => "MEMBERSHIP_CARD", UndyingSigil => "UNDYING_SIGIL",
                _ => null,
            };
            if (key != null)
                __result = new LocString("relics", $"OTHERWORLD_TREASURES_GOLDEN_{key}.description");
        }
        else if (SilverRelics.IsSilver(__instance) && SilverNativeTurns.OriginalTurn(__instance) > 1)
        {
            __result = new LocString("relics", __instance is SparklingRouge
                ? "OTHERWORLD_TREASURES_SILVER_FIXED_STATS.description" : __instance is CaptainsWheel or HornCleat
                ? "OTHERWORLD_TREASURES_SILVER_FIXED_BLOCK.description"
                : "OTHERWORLD_TREASURES_SILVER_FIXED_ENERGY.description");
            __result.Add("Turn", SilverNativeTurns.OriginalTurn(__instance) - 1);
        }
    }
}
