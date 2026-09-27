using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Rooms;

namespace OtherworldTreasures.Scripts.Rewards;

// === Patch: RewardsCmd.GenerateForRoomEnd（战后奖励生成前注入生命水晶）===
// 理由：生命水晶是全模组机制，不属于任何遗物，原版没有"全局追加战后奖励"的钩子；
// 生命水晶本身已实现为 ModCustomReward（扩展点 1/2），这里只是在原版奖励生成前把它加入房间。
// 形式为不 return false 的非阻断前缀（优先级 4）：不替换、不拦截原方法，
// 与原版 RoyaltiesPower / SwipePower 调用的 combatRoom.AddExtraReward 完全同一 API；
// ExtraReward 会随房间存档序列化，读档重开奖励界面时由工厂重建，不会重复滚点。
[HarmonyPatch(typeof(RewardsCmd), nameof(RewardsCmd.GenerateForRoomEnd))]
public static class LifeCrystalRewardInjection
{
    // 每次战后奖励生成时出现生命水晶的概率
    public const float AppearChance = 0.02f;

    static void Prefix(Player player, AbstractRoom room)
    {
        if (room is not CombatRoom combatRoom)
        {
            return;
        }

        // 读档后该奖励已由 ModRewardRegistry 工厂重建进 ExtraRewards；同一玩家只注入一次
        if (combatRoom.ExtraRewards.TryGetValue(player, out var existing)
            && existing.Any(r => r is LifeCrystalReward))
        {
            return;
        }

        // 必须走所有客户端共享的奖励 RNG：原版奖励滚点也用它，保证联机各端结果一致
        if (player.PlayerRng.Rewards.NextFloat() < AppearChance)
        {
            combatRoom.AddExtraReward(player, new LifeCrystalReward(player));
            Entry.Logger.Info("[LifeCrystal] 生命水晶加入战后奖励");
        }
    }
}
