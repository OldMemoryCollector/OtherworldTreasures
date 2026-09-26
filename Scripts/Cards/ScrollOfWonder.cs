using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Audio;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Keywords;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Cards;

// 惊奇卡牌：抽到时自动打出；抽一张牌；打出一张随机卡牌（排除自身）并除外；除外自身。
[RegisterCard(typeof(TokenCardPool))]
public class ScrollOfWonder : ModCardTemplate
{
    private const int energyCost = 3;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Ancient;
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    // 实例级标志：防止同一实例重复自动打出
    private bool _autoPlayed;

    public ScrollOfWonder() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    // 先古卡面风格
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://OtherworldTreasures/images/cards/Scroll_of_Wonder.png",
        VisualStyle: CardVisualStyle.Ancient
    );

    // 关键词：除外（视觉标记）
    public override IEnumerable<CardKeyword> CanonicalKeywords => [
        MyKeywords.Exclude
    ];

    // 悬浮提示：补充随机卡池的范围说明
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => base.AdditionalHoverTips.Append(
        new HoverTip(
            new LocString("cards", "OTHERWORLD_TREASURES_CARD_SCROLL_OF_WONDER.tip.title"),
            new LocString("cards", "OTHERWORLD_TREASURES_CARD_SCROLL_OF_WONDER.tip.description")));

    // 打出后送入除外池
    protected override CardLocation GetResultLocationForCardPlay()
    {
        return new CardLocation(Owner, Entry.ExclusionPile, CardPilePosition.Top);
    }

    // 抽到时自动打出
    public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (card != this) return;
        if (_autoPlayed) return; // 同一实例只自动打一次
        _autoPlayed = true;

        await CardCmd.AutoPlay(choiceContext, this, null!, AutoPlayType.Default, false, false);
    }

    // 打出时的效果：先打随机卡（除外），再抽一张牌
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 从所有卡牌中随机选一张（排除诅咒/状态/任务/事件/先古，排除衍生牌与自身防止递归/缺目标）
        var allPlayableCards = ModelDb.AllCards
            .Where(c => c.Type != CardType.Curse && c.Type != CardType.Status && c.Type != CardType.Quest)
            .Where(c => c.Rarity != CardRarity.Ancient && c.Rarity != CardRarity.Event)
            .Where(c => c is not IDerivedCard)
            .Where(c => !Equals(c.GetType(), typeof(ScrollOfWonder)))
            .ToList();

        if (allPlayableCards.Count > 0)
        {
            var randomCanonical = allPlayableCards[System.Random.Shared.Next(allPlayableCards.Count)];
            var combatState = CombatManager.Instance.DebugOnlyGetState();
            var randomCard = combatState.CreateCard(randomCanonical, Owner);

            // 转化音效（随机卡出现的惊喜感）
            SfxCmd.Play(FmodSfx.transform);

            // 放入手牌打出（必须先进手牌才能 AutoPlay）
            await CardPileCmd.AddGeneratedCardToCombat(randomCard, PileType.Hand, Owner, CardPilePosition.Random);
            await CardCmd.AutoPlay(choiceContext, randomCard, null!, AutoPlayType.Default, false, false);

            // 除外：用完整参数调用 CardPileCmd.Add
            // Add(IEnumerable<CardModel>, PileType, CardPilePosition, AbstractModel, bool)
            await CardPileCmd.Add(
                new[] { randomCard },
                Entry.ExclusionPile,
                CardPilePosition.Top,
                null!,
                true);
        }

        // 抽一张牌
        await CardPileCmd.Draw(choiceContext, Owner);
    }
}
