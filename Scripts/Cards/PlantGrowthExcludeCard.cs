using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using OtherworldTreasures.Scripts.Monsters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Cards;

// 施肥（衍生卡·除外版）：效果与基础版完全一致，只是打出后送入除外池（一次性）。
[RegisterCard(typeof(TokenCardPool))]
public class PlantGrowthExcludeCard : ModCardTemplate, IDerivedCard
{
    private const int energyCost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Token;
    private const bool shouldShowInCardLibrary = true;

    public PlantGrowthExcludeCard() : base(energyCost, type, rarity, TargetType.Self, shouldShowInCardLibrary)
    {
    }

    // 卡图（244x184 横版，三张施肥卡共用）
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://OtherworldTreasures/images/cards/Plant_Growth.png"
    );

    // 关键词：除外（打出后进除外池，无法常规取回）
    public override IEnumerable<CardKeyword> CanonicalKeywords => [MyKeywords.Exclude];

    // 打出后送入除外池（而不是弃牌堆）
    protected override CardLocation GetResultLocationForCardPlay()
    {
        return new CardLocation(Owner, Entry.ExclusionPile, CardPilePosition.Top);
    }

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
