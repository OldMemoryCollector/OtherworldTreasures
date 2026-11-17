using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;

namespace OtherworldTreasures.Scripts.Relics;

[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.GainMaxHp), [typeof(Creature), typeof(decimal)])]
internal static class VitalCrystalGainMaxHp
{
    [HarmonyPrefix, HarmonyPriority(Priority.Last)]
    private static bool Prefix(Creature creature, decimal amount, ref Task __result)
    {
        if (amount < 0m || VitalCrystalHp.Get(creature) is not { } data) return true;
        __result = Gain(creature, data.BaseMaxHp + amount);
        return false;
    }

    private static async Task Gain(Creature creature, decimal baseMaxHp)
    {
        // Native GainMaxHp flow: set the maximum, record actual growth, then heal it.
        decimal gained = await CreatureCmd.SetMaxHp(creature, baseMaxHp);
        var player = creature.Player!;
        var history = player.RunState.CurrentMapPointHistoryEntry;
        if (history != null) history.GetEntry(player.NetId).MaxHpGained += (int)gained;
        await CreatureCmd.Heal(creature, gained);
    }
}

#if STS2_113
[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.LoseMaxHp),
    [typeof(PlayerChoiceContext), typeof(Creature), typeof(decimal), typeof(MegaCrit.Sts2.Core.Models.CardModel)])]
#else
[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.LoseMaxHp),
    [typeof(PlayerChoiceContext), typeof(Creature), typeof(decimal), typeof(bool)])]
#endif
internal static class VitalCrystalLoseMaxHp
{
    [HarmonyPrefix, HarmonyPriority(Priority.Last)]
#if STS2_113
    private static bool Prefix(PlayerChoiceContext choiceContext, Creature creature, decimal amount, MegaCrit.Sts2.Core.Models.CardModel? cardSource, ref Task __result)
    {
        bool isFromCard = cardSource != null;
#else
    private static bool Prefix(PlayerChoiceContext choiceContext, Creature creature, decimal amount, bool isFromCard, ref Task __result)
    {
        MegaCrit.Sts2.Core.Models.CardModel? cardSource = null;
#endif
        if (amount < 0m || VitalCrystalHp.Get(creature) is not { } data) return true;
        decimal baseMaxHp = VitalCrystalHpMath.AfterLoss(data.BaseMaxHp, amount);
        int finalMaxHp = VitalCrystalHpMath.Scale(baseMaxHp, data.Copies);
        __result = Lose(choiceContext, creature, baseMaxHp, finalMaxHp, isFromCard, cardSource);
        return false;
    }

    private static async Task Lose(PlayerChoiceContext choiceContext, Creature creature, decimal baseMaxHp, int finalMaxHp, bool isFromCard, MegaCrit.Sts2.Core.Models.CardModel? cardSource)
    {
        var player = creature.Player!;
        var history = player.RunState.CurrentMapPointHistoryEntry;
        if (history != null) history.GetEntry(player.NetId).MaxHpLost += Math.Max(0, creature.MaxHp - finalMaxHp);
        if (finalMaxHp < creature.CurrentHp)
            await CreatureCmd.Damage(choiceContext, creature, creature.CurrentHp - finalMaxHp,
                isFromCard ? ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move : ValueProp.Unblockable | ValueProp.Unpowered,
                cardSource, null);
        await CreatureCmd.SetMaxHp(creature, baseMaxHp);
    }
}

[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.SetMaxHp), [typeof(Creature), typeof(decimal)])]
internal static class VitalCrystalSetMaxHp
{
    private sealed record Pending(decimal BaseMaxHp, int FinalMaxHp);

    [HarmonyPrefix, HarmonyPriority(Priority.Last)]
    private static void Prefix(Creature creature, ref decimal amount, out Pending? __state)
    {
        __state = null;
        if (VitalCrystalHp.Get(creature) is not { } data) return;
        decimal baseMaxHp = Math.Clamp(amount, 0m, VitalCrystalHpMath.MaxHp);
        int finalMaxHp = VitalCrystalHpMath.Scale(baseMaxHp, data.Copies);
        __state = new Pending(baseMaxHp, finalMaxHp);
        amount = finalMaxHp;
    }

    private static void Postfix(Creature creature, Pending? __state, ref Task<decimal> __result)
    {
        if (__state != null) __result = Complete(__result, creature, __state);
    }

    private static async Task<decimal> Complete(Task<decimal> original, Creature creature, Pending pending)
    {
        decimal delta = await original;
        VitalCrystalHp.Commit(creature, pending.BaseMaxHp, pending.FinalMaxHp);
        return delta;
    }
}
