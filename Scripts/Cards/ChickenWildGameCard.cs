using System.Collections.Generic;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Cards;

// 鸡肉味野味（来自《惊悚乐园》）：【贝尔的日常小刀】击杀敌人时获得的卡。
// 0 费技能：回复生命、本场战斗获得力量，使用后从本局牌组中永久移除。
// 图标暂用原版占位图。
[RegisterCard(typeof(TokenCardPool))]
public class ChickenWildGameCard : ModCardTemplate
{
    private const string PlaceholderPortrait = "res://images/powers/missing_power.png";

    private const int energyCost = 0;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Token;
    private const bool shouldShowInCardLibrary = DevelopmentContent.Extras;

    // 回复量与本场力量
    private const decimal HealAmount = 8m;
    private const decimal StrengthAmount = 2m;

    public ChickenWildGameCard() : base(energyCost, type, rarity, TargetType.Self, shouldShowInCardLibrary)
    {
    }

    public override bool CanBeGeneratedInCombat => false;
    public override bool CanBeGeneratedByModifiers => false;
    public override CardAssetProfile AssetProfile => new(PortraitPath: PlaceholderPortrait);

    private static readonly CardKeyword[] Keywords = [CardKeyword.Exhaust];

    public override IEnumerable<CardKeyword> CanonicalKeywords => Keywords;

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var me = Owner?.Creature;
        if (me == null)
        {
            return;
        }

        await CreatureCmd.Heal(me, HealAmount);
        await PowerCmd.Apply<StrengthPower>(choiceContext, me, StrengthAmount, me, this);

        // 用掉就从牌组里彻底消失，不留残壳
        await CardPileCmd.RemoveFromDeck(this);
    }
}
