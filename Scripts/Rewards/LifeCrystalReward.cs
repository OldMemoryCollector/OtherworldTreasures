using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Rewards;
using OtherworldTreasures.Scripts.Relics;
using STS2RitsuLib.Combat.Rewards;

namespace OtherworldTreasures.Scripts.Rewards;

public class LifeCrystalReward : ModCustomReward
{
    public const decimal MaxHpGain = 10m;
    public LifeCrystalReward(Player player) : base(player) { }
    public override RewardType ModRewardType => Entry.LifeCrystalRewardType;
    protected override string? RewardIconPath => "res://OtherworldTreasures/images/relics/LifeCrystal_85.png";
    protected override string DescriptionLocKey => "OTHERWORLD_TREASURES_REWARD_LIFE_CRYSTAL";
    public override void MarkContentAsSeen() { }
    protected override async Task<bool> OnSelect()
    {
        await RelicCmd.Obtain<LifeCrystal>(Player);
        return true;
    }
}

public class LifeFruitReward : ModCustomReward
{
    public LifeFruitReward(Player player) : base(player) { }
    public override RewardType ModRewardType => Entry.LifeFruitRewardType;
    protected override string? RewardIconPath => "res://OtherworldTreasures/images/relics/LifeFruit_85.png";
    protected override string DescriptionLocKey => "OTHERWORLD_TREASURES_REWARD_LIFE_FRUIT";
    public override void MarkContentAsSeen() { }
    protected override async Task<bool> OnSelect()
    {
        await RelicCmd.Obtain<LifeFruit>(Player);
        return true;
    }
}
