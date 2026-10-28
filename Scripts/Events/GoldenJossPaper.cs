using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models.Relics;

namespace OtherworldTreasures.Scripts.Events;

// Native Joss Paper calculates the number of threshold activations directly
// instead of reading Cards. Apply its golden yield at that specific draw site,
// retaining the native exhaust counter, ethereal deferral and remainder logic.
[HarmonyPatch]
internal static class GoldenJossPaper
{
    private static MethodBase TargetMethod()
    {
        var method = AccessTools.Method(typeof(JossPaper), "DrawIfThresholdMet");
        var stateMachine = method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType
            ?? throw new InvalidOperationException("Joss Paper's native draw method changed.");
        return AccessTools.Method(stateMachine, "MoveNext");
    }

    internal static int CardsForActivations(int activations, JossPaper relic) =>
        GoldenRelics.IsGolden(relic) ? checked(activations * relic.DynamicVars.Cards.IntValue) : activations;

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        var code = instructions.ToList();
        var owner = __originalMethod.DeclaringType!.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(f => f.FieldType == typeof(JossPaper));
        int changed = 0;
        for (int i = 0; i < code.Count; i++)
        {
            yield return code[i];
            if (code[i].operand is not MethodInfo conversion || conversion.DeclaringType != typeof(decimal) ||
                conversion.Name != "op_Explicit" || conversion.ReturnType != typeof(int)) continue;
            // The integer quotient immediately supplies this draw's count.
            // The later conversion for the modulo counter must remain untouched.
            if (!code.Skip(i + 1).Take(6).Any(c => c.operand is MethodInfo call &&
                    call.DeclaringType == typeof(CardPileCmd) && call.Name == nameof(CardPileCmd.Draw))) continue;
            yield return new CodeInstruction(OpCodes.Ldarg_0);
            yield return new CodeInstruction(OpCodes.Ldfld, owner);
            yield return CodeInstruction.Call(typeof(GoldenJossPaper), nameof(CardsForActivations));
            changed++;
        }
        if (changed != 1)
            throw new InvalidOperationException($"Expected one native Joss Paper draw count, found {changed}.");
    }
}
