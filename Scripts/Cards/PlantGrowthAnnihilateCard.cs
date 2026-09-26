using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using OtherworldTreasures.Scripts.Monsters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Cards;

// 施肥（衍生卡·湮灭版）：效果与基础版完全一致，
// 但回合结束时若还留在手牌里，就会被送进除外池。
[RegisterCard(typeof(TokenCardPool))]
public class PlantGrowthAnnihilateCard : ModCardTemplate, IDerivedCard
{
    private const int energyCost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Token;
    private const bool shouldShowInCardLibrary = true;

    public PlantGrowthAnnihilateCard() : base(energyCost, type, rarity, TargetType.Self, shouldShowInCardLibrary)
    {
    }

    // 卡图（244x184 横版，三张施肥卡共用）
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://OtherworldTreasures/images/cards/Plant_Growth.png"
    );

    // 关键词：湮灭（回合结束时若仍在手牌中，则将其除外）
    public override IEnumerable<CardKeyword> CanonicalKeywords => [MyKeywords.Annihilate];

    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("Hp", PlantSummonBase.GrowthAmount),
    ];

    // 悬浮说明：照原版【召唤】的做法，卡面只写「植培5」，含义由悬浮提示给出
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => base.AdditionalHoverTips
        .Append(PlantGrowthTips.Build());

    // 湮灭：回合结束时若此牌仍在手牌中，则将其除外（与【缩小灯】同一套写法）
    // 注意：本 hook 跑在回合结束管线中间，抛异常会打断整条管线（表现为卡死），因此全程兜异常。
    public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        try
        {
            if (!participants.Contains(Owner.Creature))
            {
                return;
            }
            if (Pile?.Type != PileType.Hand)
            {
                return;
            }
            // skipVisuals 必须为 true：原版回合结束清手牌同样跳过动画，否则有卡死风险
            var results = await CardPileCmd.Add(
                new[] { this },
                Entry.ExclusionPile,
                CardPilePosition.Top,
                null!,
                true);
            // 静默移动只搬模型，手牌里的节点会留下"尸体"；补一次飞向除外池的动画（只 Play 不 await）
            var (tween, _) = CardPileCmd.GetTweenForCardsChangingPiles(results, fromSilentAdd: true);
            tween?.Play();
        }
        catch (Exception e)
        {
            Entry.Logger.Info($"[PlantGrowthAnnihilateCard] 湮灭 EXCEPTION（已忽略，避免打断回合结束）：{e}");
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner != null)
        {
            await PlantSummonBase.GrowPlant(Owner, PlantSummonBase.GrowthAmount);
        }
    }
}
