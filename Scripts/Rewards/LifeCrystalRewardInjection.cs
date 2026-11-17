using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models;

namespace OtherworldTreasures.Scripts.Rewards;

// Registration and the 111/113 hook lifecycle belong to RitsuLib.
// ExtraRewards persists the drops; the native hook receives a list already copied from the room.
[RegisterSingleton]
public sealed class LifeCrystalRewardInjection : HookedSingletonModel
{
    public const float AppearChance = 0.02f;

    public LifeCrystalRewardInjection() : base(HookType.Run) { }

    public override bool TryModifyRewards(Player player, List<Reward> rewards, AbstractRoom? room)
    {
        if (room is not CombatRoom combatRoom || !combatRoom.Encounter.ShouldGiveRewards || rewards.Count == 0)
            return false;

        bool modified = false;
        try
        {
            if (!ShimmerDropData.TryBegin(player, combatRoom)) return false;
            combatRoom.ExtraRewards.TryGetValue(player, out var existing);
            if (existing?.Any(r => r is LifeCrystalReward or LifeFruitReward) == true
                || rewards.Any(r => r is LifeCrystalReward or LifeFruitReward))
                return false;

            if (player.PlayerRng.Rewards.NextFloat() < AppearChance)
                Append(new LifeCrystalReward(player));
            if (player.PlayerRng.Rewards.NextFloat() < AppearChance)
                Append(new LifeFruitReward(player));

            void Append(Reward reward)
            {
                combatRoom.AddExtraReward(player, reward);
                rewards.Add(reward);
                modified = true;
            }
        }
        catch (Exception e)
        {
            Entry.Logger.Warn($"[LifeCrystal] 战后奖励追加失败（原版奖励照常生成）：{e}");
        }
        return modified;
    }
}
