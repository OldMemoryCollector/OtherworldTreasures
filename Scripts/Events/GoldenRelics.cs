using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using STS2RitsuLib.Utils;

namespace OtherworldTreasures.Scripts.Events;

// Third-party authors may opt a model into this event without new model IDs or
// registration-order dependencies. Each adapter names only actual positive yields.
public interface IGoldenRelicAdapter
{
    IReadOnlyCollection<string> GoldenBenefitVariables { get; }
}

public static class GoldenRelics
{
    private static readonly SavedAttachedState<RelicModel, string> Benefits = new("OtherworldGoldenBenefitsV1", () => "");
    private static readonly ConditionalWeakTable<Type, string[]> Automatic = new();
    private sealed record ParsedBenefits(string Json, Dictionary<string, decimal> Values);
    private static readonly ConditionalWeakTable<RelicModel, ParsedBenefits> Parsed = new();
    public static bool IsGolden(RelicModel relic) => relic.IsMutable && Benefits.TryGetValue(relic, out var value) && value.Length > 0;

    internal static bool IsEligibleUncoated(RelicModel relic) => relic.IsMutable && !IsGolden(relic) && !SilverRelics.IsSilver(relic) && !relic.IsMelted &&
        !relic.IsWax && relic.Rarity != RelicRarity.None &&
        !relic.IsUsedUp;

    public static bool CanEnhance(RelicModel relic) => IsEligibleUncoated(relic) &&
        (Variables(relic).Count > 0 || GoldenNativeModifiers.Supports(relic));

    private static IReadOnlyCollection<string> Variables(RelicModel relic)
    {
        var keys = relic is IGoldenRelicAdapter adapter ? adapter.GoldenBenefitVariables :
            Automatic.GetValue(relic.GetType(), _ => NativeGoldenBenefits.Variables(relic) ?? DetectSafeShape(relic));
        return keys.All(k => relic.DynamicVars.TryGetValue(k, out var v) && v.BaseValue > 0) ? keys : Array.Empty<string>();
    }

    private enum Benefit { Block, Strength, Dexterity, Gold, Energy, Heal, Draw, MaxHp, Stars, OrbSlots, Summon, Power }

    // Inspect each value separately. A relic can have both a yield and a trigger
    // counter; only values whose every read goes directly into a positive command
    // targeting the owner qualify. The same check also applies to third-party models.
    private static string[] DetectSafeShape(RelicModel relic)
    {
        if (relic.GetType().GetMethod(nameof(RelicModel.AfterObtained))!.DeclaringType != typeof(RelicModel) ||
            relic.GetType().GetMethod(nameof(RelicModel.AfterRemoved))!.DeclaringType != typeof(RelicModel)) return [];
        var bodies = new List<List<CodeInstruction>>();
        for (Type? type = relic.GetType(); type != null && type != typeof(RelicModel); type = type.BaseType)
        {
            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                var body = method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType
                    .GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic) ?? method;
                if (body.GetMethodBody() != null) bodies.Add(PatchProcessor.GetOriginalInstructions(body));
            }
        }
        return relic.DynamicVars.Values.Where(v => v.BaseValue > 0 && IsDirectBenefit(v, bodies)).Select(v => v.Name).ToArray();
    }

    private static bool IsDirectBenefit(DynamicVar value, List<List<CodeInstruction>> bodies)
    {
        Type? genericPower = BeneficialPowerType(value);
        (string Key, string? Accessor, Benefit Kind, Type? PowerType)? shape = value switch
        {
            BlockVar => ("Block", "get_Block", Benefit.Block, null),
            PowerVar<StrengthPower> => (nameof(StrengthPower), "get_Strength", Benefit.Strength, typeof(StrengthPower)),
            PowerVar<DexterityPower> => (nameof(DexterityPower), "get_Dexterity", Benefit.Dexterity, typeof(DexterityPower)),
            GoldVar => ("Gold", "get_Gold", Benefit.Gold, null),
            EnergyVar => ("Energy", "get_Energy", Benefit.Energy, null),
            HealVar => ("Heal", "get_Heal", Benefit.Heal, null),
            CardsVar => ("Cards", "get_Cards", Benefit.Draw, null),
            MaxHpVar => ("MaxHp", "get_MaxHp", Benefit.MaxHp, null),
            StarsVar => ("Stars", "get_Stars", Benefit.Stars, null),
            RepeatVar => ("Repeat", "get_Repeat", Benefit.OrbSlots, null),
            SummonVar => (value.Name, value.Name == "Summon" ? "get_Summon" : null, Benefit.Summon, null),
            _ when genericPower != null => (value.Name, null, Benefit.Power, genericPower),
            _ => null,
        };
        if (shape == null || value.Name != shape.Value.Key) return false;
        var (_, accessor, kind, powerType) = shape.Value;
        int reads = 0;
        foreach (var il in bodies)
        {
            for (int i = 0; i < il.Count; i++)
            {
                if (il[i].operand is not MethodInfo getter || getter.Name != "get_DynamicVars") continue;
                int end = i + 1;
                if (end >= il.Count) return false;
                bool readsValue = accessor != null && il[end].operand is MethodInfo next && next.Name == accessor;
                if (il[end].opcode == OpCodes.Ldstr)
                {
                    if ((string)il[end].operand != value.Name) continue;
                    end++;
                    readsValue = end < il.Count && il[end].operand is MethodInfo indexer && indexer.Name == "get_Item";
                }
                else if (!readsValue && il[end].operand is MethodInfo other && other.Name.StartsWith("get_")) continue;
                // An unknown use of the whole set could also read this value.
                if (!readsValue) return false;
                reads++;
                var tail = il.Skip(end + 1).Take(24).ToList();
                int consumerIndex = tail.FindIndex(x => x.operand is MethodInfo m && m.DeclaringType?.Namespace == typeof(CreatureCmd).Namespace);
                if (consumerIndex < 0 || tail[consumerIndex].operand is not MethodInfo consumer || !MatchesConsumer(consumer, kind, powerType)) return false;
                var between = tail.Take(consumerIndex).ToArray();
                if (between.Any(x => !IsArgumentLoad(x))) return false;
                bool playerTarget = kind is Benefit.Gold or Benefit.Energy or Benefit.Draw or Benefit.Stars or Benefit.OrbSlots or Benefit.Summon;
                var getters = (playerTarget ? between : il.Take(i)).Select(x => x.operand).OfType<MethodInfo>().ToArray();
                if (kind is Benefit.OrbSlots or Benefit.Summon)
                {
                    var targetGetters = il.Take(i).Select(x => x.operand).OfType<MethodInfo>().ToArray();
                    if (targetGetters.LastOrDefault()?.Name != "get_Owner" || targetGetters[^1].DeclaringType != typeof(RelicModel)) return false;
                }
                else if (playerTarget)
                {
                    if (getters.LastOrDefault()?.Name != "get_Owner" || getters[^1].DeclaringType != typeof(RelicModel)) return false;
                }
                else if (getters.Length < 2 || getters[^2].Name != "get_Owner" || getters[^2].DeclaringType != typeof(RelicModel) || getters[^1].Name != "get_Creature") return false;
            }
        }
        return reads > 0;
    }

    private static Type? BeneficialPowerType(DynamicVar value)
    {
        for (Type? type = value.GetType(); type != null; type = type.BaseType)
        {
            if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(PowerVar<>)) continue;
            Type powerType = type.GetGenericArguments()[0];
            if (!ModelDb.Contains(powerType)) return null;
            return ModelDb.GetByIdOrNull<PowerModel>(ModelDb.GetId(powerType))?.Type == PowerType.Buff ? powerType : null;
        }
        return null;
    }

    private static bool IsArgumentLoad(CodeInstruction instruction)
    {
        if (instruction.operand is MethodInfo method)
            return method.Name is "get_BaseValue" or "get_IntValue" or "get_Owner" or "get_Creature" ||
                method.DeclaringType == typeof(decimal) && method.Name == "op_Implicit";
        string name = instruction.opcode.Name ?? "";
        return name.StartsWith("ldarg") || name.StartsWith("ldloc") || name.StartsWith("ldc.i4") ||
            instruction.opcode == OpCodes.Ldfld || instruction.opcode == OpCodes.Ldnull || instruction.opcode == OpCodes.Nop;
    }

    private static bool MatchesConsumer(MethodInfo method, Benefit kind, Type? powerType) => kind switch
    {
        Benefit.Gold => method.DeclaringType == typeof(PlayerCmd) && method.Name == nameof(PlayerCmd.GainGold),
        Benefit.Energy => method.DeclaringType == typeof(PlayerCmd) && method.Name == nameof(PlayerCmd.GainEnergy),
        Benefit.Draw => method.DeclaringType == typeof(CardPileCmd) && method.Name == nameof(CardPileCmd.Draw),
        Benefit.Block => method.DeclaringType == typeof(CreatureCmd) && method.Name == nameof(CreatureCmd.GainBlock),
        Benefit.Heal => method.DeclaringType == typeof(CreatureCmd) && method.Name == nameof(CreatureCmd.Heal),
        Benefit.MaxHp => method.DeclaringType == typeof(CreatureCmd) && method.Name == nameof(CreatureCmd.GainMaxHp),
        Benefit.Stars => method.DeclaringType == typeof(PlayerCmd) && method.Name == nameof(PlayerCmd.GainStars),
        Benefit.OrbSlots => method.DeclaringType == typeof(OrbCmd) && method.Name == nameof(OrbCmd.AddSlots),
        Benefit.Summon => method.DeclaringType == typeof(OstyCmd) && method.Name == nameof(OstyCmd.Summon),
        Benefit.Power => method.DeclaringType == typeof(PowerCmd) && method.Name == nameof(PowerCmd.Apply) &&
            method.IsGenericMethod && method.GetGenericArguments()[0] == powerType,
        _ => method.DeclaringType == typeof(PowerCmd) && method.Name == nameof(PowerCmd.Apply) && method.IsGenericMethod &&
            method.GetGenericArguments()[0] == (kind == Benefit.Strength ? typeof(StrengthPower) : typeof(DexterityPower)),
    };

    public static bool Enhance(RelicModel relic)
    {
        if (!CanEnhance(relic)) return false;
        var originals = Variables(relic).ToDictionary(k => k, k => relic.DynamicVars[k].BaseValue);
        // A native fixed contribution has no DynamicVar. Persist a reserved marker
        // in the existing dictionary format so old saved marks remain readable.
        if (GoldenNativeModifiers.Supports(relic)) originals["$native"] = 1m;
        Benefits[relic] = JsonSerializer.Serialize(originals);
        Apply(relic, relic.DynamicVars);
        return true;
    }

    internal static void CopyMark(RelicModel original, RelicModel clone)
    {
        if (IsGolden(original)) Benefits[clone] = Benefits[original];
    }

    internal static void Apply(RelicModel relic, DynamicVarSet vars)
    {
        if (!IsGolden(relic)) return;
        string json = Benefits[relic];
        if (!Parsed.TryGetValue(relic, out var cached) || cached.Json != json)
        {
            cached = new ParsedBenefits(json, JsonSerializer.Deserialize<Dictionary<string, decimal>>(json)!);
            Parsed.Remove(relic);
            Parsed.Add(relic, cached);
        }
        foreach (var (key, original) in cached.Values)
            if (vars.TryGetValue(key, out var value))
            {
                decimal enhanced = NativeGoldenBenefits.Transform(relic, key, original);
                if (value.BaseValue != enhanced) value.BaseValue = enhanced;
            }
    }
}

[HarmonyPatch(typeof(AbstractModel), nameof(AbstractModel.MutableClone))]
internal static class GoldenRelicClone
{
    private static void Postfix(AbstractModel __instance, AbstractModel __result)
    {
        if (__instance is RelicModel original && __result is RelicModel clone)
        {
            GoldenRelics.CopyMark(original, clone);
            SilverRelics.CopyMark(original, clone);
        }
    }
}

[HarmonyPatch(typeof(RelicModel), nameof(RelicModel.DynamicVars), MethodType.Getter)]
internal static class GoldenRelicValues
{
    private static void Postfix(RelicModel __instance, DynamicVarSet __result)
    {
        GoldenRelics.Apply(__instance, __result);
        SilverRelics.Apply(__instance, __result);
    }
}

[HarmonyPatch(typeof(RelicModel), nameof(RelicModel.Title), MethodType.Getter)]
internal static class GoldenRelicName
{
    private static void Postfix(RelicModel __instance, ref LocString __result)
    {
        bool gold = GoldenRelics.IsGolden(__instance);
        if (!gold && !SilverRelics.IsSilver(__instance)) return;
        var name = new LocString("relics", gold ? "OTHERWORLD_TREASURES_GOLDEN_PREFIX" : "OTHERWORLD_TREASURES_SILVER_PREFIX");
        name.Add("Title", __result);
        __result = name;
    }
}
