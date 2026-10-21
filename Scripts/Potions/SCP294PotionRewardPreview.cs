using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Potions;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace OtherworldTreasures.Scripts.Potions;

internal static class Scp294PotionRewardPreview
{
    private static readonly MethodInfo SetPotion = AccessTools.PropertySetter(typeof(PotionReward), nameof(PotionReward.Potion));
    private static readonly FieldInfo OverrideRng = AccessTools.Field(typeof(Reward), "_rngOverride");
    private static readonly FieldInfo Icon = AccessTools.Field(typeof(PotionReward), "_icon");

    internal static void Prepare(PotionReward reward)
    {
        if (reward.Potion is not { } original || !Scp294Settings.ShouldReplace(original, reward.Player)) return;
        var rng = (Rng?)OverrideRng.GetValue(reward) ?? reward.Player.PlayerRng.Rewards;
        Replace(reward, SCP294Potion.BrewRandomWithRng(rng));
    }

    private static void Replace(PotionReward reward, PotionModel potion)
    {
        SetPotion.Invoke(reward, [potion]);
        if (Icon.GetValue(reward) is NPotion icon) icon.Model = potion;
    }

    internal static void Save(PotionReward reward, SerializableReward save)
    {
        if (reward.Potion is { } potion && SCP294Potion.TryRecipe(potion, out _))
            save.PredeterminedModelId = potion.ToSerializable(-1).Id!;
    }

    internal static bool HasRecipe(SerializableReward save) =>
        save.PredeterminedModelId?.Entry.StartsWith("BREW_", StringComparison.Ordinal) == true;

    internal static void Restore(PotionReward reward, SerializableReward save)
    {
        if (HasRecipe(save)) Replace(reward, PotionModel.FromSerializable(new SerializablePotion { Id = save.PredeterminedModelId }));
    }
}

[HarmonyPatch(typeof(PotionReward), nameof(PotionReward.Populate))]
internal static class Scp294PopulatePotionReward
{
    private static void Postfix(PotionReward __instance) => Scp294PotionRewardPreview.Prepare(__instance);
}

// Reward hooks can append an already-populated potion after the initial Populate pass.
// Finish preparing those before the native reward screen and synchronization begin.
[HarmonyPatch(typeof(RewardsSet), nameof(RewardsSet.GenerateWithoutOffering))]
internal static class Scp294PrepareModifiedPotionRewards
{
    private static void Postfix(RewardsSet __instance, ref Task __result) => __result = Finish(__result, __instance);
    private static async Task Finish(Task original, RewardsSet rewards)
    {
        await original;
        foreach (var reward in rewards.Rewards.OfType<PotionReward>()) Scp294PotionRewardPreview.Prepare(reward);
    }
}

// Reuse the native predetermined-model field; no new network or save schema is needed.
[HarmonyPatch(typeof(Reward), nameof(Reward.ToSerializable))]
internal static class Scp294SavePotionReward
{
    private static void Postfix(Reward __instance, SerializableReward __result)
    {
        if (__instance is PotionReward potion) Scp294PotionRewardPreview.Save(potion, __result);
    }
}

[HarmonyPatch(typeof(Reward), nameof(Reward.FromSerializable))]
internal static class Scp294RestorePotionReward
{
    private static bool Prefix(SerializableReward save, Player player, ref Reward __result)
    {
        if (save.RewardType != RewardType.Potion || !Scp294PotionRewardPreview.HasRecipe(save)) return true;
        var reward = new PotionReward(player);
        Scp294PotionRewardPreview.Restore(reward, save);
        __result = reward;
        return false;
    }
    private static void Postfix(SerializableReward save, Reward __result)
    {
        // Also restore native PotionReward subclasses reconstructed by RitsuLib's reward registry.
        if (__result is PotionReward potion && Scp294PotionRewardPreview.HasRecipe(save) &&
            (potion.Potion == null || !SCP294Potion.TryRecipe(potion.Potion, out _)))
            Scp294PotionRewardPreview.Restore(potion, save);
    }
}
