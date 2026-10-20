using System;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Rooms;

namespace OtherworldTreasures.Scripts.Rewards;

// === Patch: RewardsCmd.GenerateForRoomEnd（战后奖励生成前注入生命水晶和生命果）===
// 理由：生命水晶是全模组机制，不属于任何遗物，原版没有"全局追加战后奖励"的钩子；
// 生命水晶本身已实现为 ModCustomReward（扩展点 1/2），这里只是在原版奖励生成前把它加入房间。
// 形式为不 return false 的非阻断前缀（优先级 4）：不替换、不拦截原方法，
// 与原版 RoyaltiesPower / SwipePower 调用的 combatRoom.AddExtraReward 完全同一 API；
// ExtraReward 与跑局内滚点标记共同保存，重新打开奖励界面不会重复滚点。
[HarmonyPatch(typeof(RewardsCmd), nameof(RewardsCmd.GenerateForRoomEnd))]
public static class LifeCrystalRewardInjection
{
    // 每种奖励各自的战后出现概率
    public const float AppearChance = 0.02f;

    static bool Prefix(Player player, AbstractRoom room)
    {
        try
        {
            if (room is not CombatRoom combatRoom)
            {
                return true;
            }

            if (!ShimmerDropData.TryBegin(player, combatRoom)) return true;
            combatRoom.ExtraRewards.TryGetValue(player, out var existing);
            if (existing?.Any(r => r is LifeCrystalReward or LifeFruitReward) == true) return true;
            if (existing?.Any(r => r is LifeCrystalReward) != true && player.PlayerRng.Rewards.NextFloat() < AppearChance)
                combatRoom.AddExtraReward(player, new LifeCrystalReward(player));
            if (existing?.Any(r => r is LifeFruitReward) != true && player.PlayerRng.Rewards.NextFloat() < AppearChance)
                combatRoom.AddExtraReward(player, new LifeFruitReward(player));
        }
        catch (Exception e)
        {
            // 注入失败绝不能阻断原版奖励生成：返回 true 放行
            Entry.Logger.Warn($"[LifeCrystal] 战后奖励注入失败（已忽略，原版奖励照常生成）：{e}");
        }
        return true;
    }
}
