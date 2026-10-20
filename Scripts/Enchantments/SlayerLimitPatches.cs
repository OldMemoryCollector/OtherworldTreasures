using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using OtherworldTreasures.Scripts.Enchantments;
using MegaCrit.Sts2.Core.ValueProps;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace OtherworldTreasures.Scripts;

[HarmonyPatch]
internal static class SlayerLimitPatches
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var methods = new List<MethodBase>();
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try { types = assembly.GetTypes(); } catch (ReflectionTypeLoadException e) { types = e.Types.OfType<Type>().ToArray(); } catch { continue; }
            foreach (var type in types)
            {
                if (!typeof(AbstractModel).IsAssignableFrom(type)) continue;
                const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
                var cap = type.GetMethod(nameof(AbstractModel.ModifyDamageCap), flags);
                if (cap != null) methods.Add(cap);
                var hpLoss = type.GetMethod(nameof(AbstractModel.ModifyHpLostBeforeOstyLate), flags);
                if (hpLoss != null) methods.Add(hpLoss);
            }
        }
        return methods.Distinct();
    }

    private static bool Prefix(AbstractModel __instance, object[] __args, MethodBase __originalMethod, ref decimal __result)
    {
        try
        {
            var target = __args.OfType<Creature>().FirstOrDefault();
            var card = __args.OfType<CardModel>().FirstOrDefault();
            bool overridesLimitHook = __instance.GetType().GetMethod(__originalMethod.Name)?.DeclaringType != typeof(AbstractModel);
            if (__instance is PowerModel power && target?.Powers.Contains(power) == true
                && overridesLimitHook && SlayerLimitBypass.Applies(card, target))
            {
                if (__originalMethod.Name == nameof(AbstractModel.ModifyDamageCap))
                    __result = decimal.MaxValue;
                else
                    __result = __args.OfType<decimal>().FirstOrDefault();
                return false;
            }
        }
        catch { }
        return true;
    }
}

[HarmonyPatch(typeof(CardCmd), nameof(CardCmd.ClearEnchantment))]
internal static class EfficiencyClearPatch
{
    private static void Prefix(CardModel card)
    {
        try
        {
            if (card.Enchantment is Efficiency { ReducedCost: true } efficiency)
            {
                card.EnergyCost.UpgradeBy(1);
                efficiency.ReducedCost = false;
            }
        }
        catch { }
    }
}
