using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using OtherworldTreasures.Scripts.Relics;

namespace OtherworldTreasures.Scripts.Balance;

[HarmonyPatch(typeof(RelicModel), "Description", MethodType.Getter)]
internal static class RelicBalanceDescriptions
{
	[HarmonyPriority(Priority.Last)]
	private static void Postfix(RelicModel __instance, ref LocString __result)
	{
		if (LocManager.Instance?.Language != "zhs") return;
		var owner = __instance.IsMutable ? __instance.Owner : null;
		(string key, bool enabled) = __instance switch
		{
			WhatIfPhoneBooth => ("OTHERWORLD_TREASURES_RELIC_WHAT_IF_PHONE_BOOTH.description.balance", RelicBalanceRules.WhatIfPhoneBooth(owner)),
			ShrinkRay => ("OTHERWORLD_TREASURES_RELIC_SHRINK_RAY.description.balance", RelicBalanceRules.ShrinkRay(owner)),
			YorozuyaScooter => ("OTHERWORLD_TREASURES_RELIC_YOROZUYA_SCOOTER.description.balance", RelicBalanceRules.YorozuyaScooter(owner)),
			_ => (string.Empty, false)
		};
		if (!enabled) return;
		if (LocString.Exists("relics", key)) __result = new LocString("relics", key);
	}
}
