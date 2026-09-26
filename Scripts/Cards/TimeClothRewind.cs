using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models.CardPools;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Cards;

// 时光布 · 抉择选项【回溯】：回到上一回合的状态。
// 仅用于"选择一张牌"界面，不可打出、不可被战斗内随机生成。
[RegisterCard(typeof(TokenCardPool))]
public class TimeClothRewind : ModCardTemplate
{
    private const int energyCost = -1;
    private const CardType type = CardType.Status;
    private const CardRarity rarity = CardRarity.Status;
    private const TargetType targetType = TargetType.None;
    private const bool shouldShowInCardLibrary = true;

    public TimeClothRewind() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 词条：回溯（词条带完整效果说明，卡面可直接看/悬停看）
    public override IEnumerable<CardKeyword> CanonicalKeywords => [MyKeywords.Rewind];

    public override bool CanBeGeneratedInCombat => false;

    public override int MaxUpgradeLevel => 0;

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://OtherworldTreasures/images/cards/Time_Cloth_Rewind.png"
    );
}
