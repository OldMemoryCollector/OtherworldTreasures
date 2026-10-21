using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using OtherworldTreasures.Scripts.Potions;
using STS2RitsuLib;
using STS2RitsuLib.Combat.Rewards;
using STS2RitsuLib.RunData;

namespace OtherworldTreasures.Scripts.Rewards;

public sealed class AppleChestSave
{
    // 0: no drop, 1: available, 2: claimed/skipped. Persist misses as well.
    public Dictionary<string, int> Chests { get; set; } = new();
}

internal static class AppleChestDrops
{
    internal const float DropChance = .05f;
    private static PlayerRunSavedData<AppleChestSave> _slot = null!;
    internal static void Initialize()
    {
        using (RitsuLibFramework.BeginModDataRegistration(Entry.ModId))
            _slot = RitsuLibFramework.GetRunSavedDataStore(Entry.ModId).RegisterPerPlayer(
                "enchanted_apple_chests_v1", () => new AppleChestSave(),
                options: new RunSavedDataOptions { WritePolicy = RunSavedDataWritePolicy.WhenSet });
    }
    internal static string Key(Player player)
    {
        var run = player.RunState;
        var coord = run.CurrentMapCoord;
        return $"{run.CurrentActIndex}:{coord?.col}:{coord?.row}";
    }
    internal static bool IsAvailable(Player player, string key) => _slot.Get(player).Chests.GetValueOrDefault(key) == 1;
    internal static void Close(Player player, string key) => _slot.Modify(player, s => s.Chests[key] = 2);
    internal static bool Roll(Player player, string key)
    {
        var entries = _slot.Get(player).Chests;
        if (!entries.ContainsKey(key))
        {
            bool dropped = player.PlayerRng.Rewards.NextFloat() < DropChance;
            _slot.Modify(player, s => s.Chests[key] = dropped ? 1 : 0);
        }
        return IsAvailable(player, key);
    }
}

// Native potion UI, full-belt handling, history and synchronized claiming.
// Ritsu's public serializer preserves the fixed potion and chest key.
public sealed class EnchantedAppleChestReward : PotionReward, IModSerializableReward
{
    public string ChestKey { get; }
    public EnchantedAppleChestReward(Player player, string key)
        : base(ModelDb.Potion<EnchantedGoldenApple>().ToMutable(), player) => ChestKey = key;
    public RewardType ModRewardType => Entry.EnchantedAppleRewardType;
    protected override RewardType RewardType => ModRewardType;
    public string? ToModRewardJson() => JsonSerializer.Serialize(ChestKey);
    public override SerializableReward ToSerializable()
    {
        var save = ModRewardSerialization.CreateSerializable(this);
        Scp294PotionRewardPreview.Save(this, save);
        return save;
    }
    protected override async Task<bool> OnSelect()
    {
        if (!AppleChestDrops.IsAvailable(Player, ChestKey)) return true;
        bool taken = await base.OnSelect();
        if (taken) AppleChestDrops.Close(Player, ChestKey);
        return taken;
    }
    public override void OnSkipped()
    {
        base.OnSkipped();
        AppleChestDrops.Close(Player, ChestKey);
    }
}

[HarmonyPatch(typeof(RewardsSet), nameof(RewardsSet.WithRewardsFromRoom))]
internal static class EnchantedAppleTreasureBonus
{
    private static void Postfix(RewardsSet __instance, AbstractRoom room)
    {
        if (room is not TreasureRoom || __instance.Rewards.OfType<EnchantedAppleChestReward>().Any()) return;
        string key = AppleChestDrops.Key(__instance.Player);
        if (AppleChestDrops.Roll(__instance.Player, key))
            __instance.Rewards.Add(new EnchantedAppleChestReward(__instance.Player, key));
    }
}
