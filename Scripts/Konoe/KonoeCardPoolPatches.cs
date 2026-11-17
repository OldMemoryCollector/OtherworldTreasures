using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace OtherworldTreasures.Scripts.Konoe;

// Borrowed higher-rarity cards retain their canonical Ironclad membership. Only an
// instance owned by Konoe uses her current pool, including native transformation paths.
[HarmonyPatch(typeof(CardModel), nameof(CardModel.Pool), MethodType.Getter)]
internal static class KonoeOwnedTemporaryCardPool
{
    private static void Postfix(CardModel __instance, ref CardPoolModel __result)
    {
        if (__result is IroncladCardPool && __instance.IsMutable && __instance.Owner?.Character is Characters.Konoe)
            __result = ModelDb.CardPool<KonoeCardPool>();
    }
}
