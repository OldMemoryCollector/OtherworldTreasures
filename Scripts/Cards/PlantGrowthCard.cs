using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using OtherworldTreasures.Scripts.Monsters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Cards;

// 施肥（衍生卡）：【戴夫的种子】在召唤植物后，再次右键遗物时三选一给到的那张卡。
// 1 费：给场上的植物 +5 最大生命并回复等量生命（效果名"植培"，对应骨手那边的"召唤"）。
// 本张是不带词条的基础版；另外两张分别是带【除外】和带【湮灭】的版本。
[RegisterCard(typeof(TokenCardPool))]
public class PlantGrowthCard : ModCardTemplate, IDerivedCard
{
    // 费用（遗物文本栏里的 {Cost} 也读这个常量）
    public const int CardEnergyCost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Token;
    private const bool shouldShowInCardLibrary = true;

    public PlantGrowthCard() : base(CardEnergyCost, type, rarity, TargetType.Self, shouldShowInCardLibrary)
    {
    }

    // 卡图（244x184 横版，三张施肥卡共用）
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://OtherworldTreasures/images/cards/Plant_Growth.png"
    );

    // 卡面描述里的数值用 {VarName} 占位，直接读植物上的常量 → 改数值不用同步文本
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("Hp", PlantSummonBase.GrowthAmount),
    ];

    // 悬浮说明：照原版【召唤】的做法，卡面只写「植培5」，含义由悬浮提示给出
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => base.AdditionalHoverTips
        .Append(PlantGrowthTips.Build());

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner != null)
        {
            await PlantSummonBase.GrowPlant(Owner, PlantSummonBase.GrowthAmount);
        }
    }
}

// 「植培」的悬浮说明（照原版【召唤】的做法：标题带数值，说明里解释效果）
internal static class PlantGrowthTips
{
    public static IHoverTip Build()
    {
        var title = new LocString("static_hover_tips", "OTHERWORLD_TREASURES_PLANT_GROWTH.title");
        var description = new LocString("static_hover_tips", "OTHERWORLD_TREASURES_PLANT_GROWTH.description");
        var amount = new DynamicVar("Hp", PlantSummonBase.GrowthAmount);
        title.Add(amount);
        description.Add(amount);
        return new HoverTip(title, description);
    }
}
