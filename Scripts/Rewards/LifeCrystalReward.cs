using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Rewards;
using STS2RitsuLib.Combat.Rewards;

namespace OtherworldTreasures.Scripts.Rewards;

// 生命水晶（来自《泰拉瑞亚》Life Crystal）：
// 战斗胜利后以固定概率出现在战后奖励中（由 LifeCrystalRewardInjection 注入）。
// 领取即使用：生命上限 +10（CreatureCmd.GainMaxHp 会同时把当前生命也补 10）。
// 无动态状态，无需 Payload；读档时由 Entry 中注册的工厂直接重建。
public class LifeCrystalReward : ModCustomReward
{
    public const decimal MaxHpGain = 10m;

    public LifeCrystalReward(Player player) : base(player)
    {
    }

    // 必须使用注册得到的 RewardType
    public override RewardType ModRewardType => Entry.LifeCrystalRewardType;

    protected override string? RewardIconPath
        => "res://OtherworldTreasures/images/rewards/life_crystal.png";

    // 与本模组其他 loc key 命名保持一致（默认规则会拼成 OTHERWORLDTREASURES_...）
    protected override string? DescriptionLocKey => "OTHERWORLD_TREASURES_REWARD_LIFE_CRYSTAL";

    // 没有需要标记为已查看的卡牌/药水内容
    public override void MarkContentAsSeen()
    {
    }

    // 玩家点击领取：加上限（同时回复等量生命）。true 表示领取成功，奖励被消除
    protected override async Task<bool> OnSelect()
    {
        await CreatureCmd.GainMaxHp(Player.Creature, MaxHpGain);
        Entry.Logger.Info("[LifeCrystal] 领取生命水晶，生命上限 +10");
        return true;
    }
}
