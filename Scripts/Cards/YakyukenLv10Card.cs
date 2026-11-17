using System.Collections.Generic;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Cards;

// 野球拳lv10（来自《金庸群侠传》）：野球拳修炼到顶之后的独立形态，不是 LV1～LV9 那张牌的第 10 级。
// 1 费攻击，使目标失去 2000 点生命值：无视格挡，也不受力量、易伤这类修正影响。
// 卡面只写台词，数值不写出来。
// 使用主人提供的野球拳10级卡图。
[RegisterCard(typeof(TokenCardPool))]
public class YakyukenLv10Card : ModCardTemplate
{
    private const string PortraitImagePath = "res://OtherworldTreasures/images/cards/Yakyuken_Lv10.png";

    private const int energyCost = 1;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Token;
    private const bool shouldShowInCardLibrary = DevelopmentContent.Extras;

    // 让目标失去的生命值
    private const decimal HpLoss = 2000m;

    public YakyukenLv10Card() : base(energyCost, type, rarity, TargetType.AnyEnemy, shouldShowInCardLibrary)
    {
    }

    public override bool CanBeGeneratedInCombat => false;
    public override bool CanBeGeneratedByModifiers => false;
    public override CardAssetProfile AssetProfile => new(PortraitPath: PortraitImagePath);

    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new HpLossVar(HpLoss)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var target = cardPlay.Target;
        if (target == null)
        {
            return;
        }

        // 失去生命值走 Unblockable | Unpowered，和原版的失去生命一致。
        await CreatureCmd.Damage(choiceContext, [target], DynamicVars["HpLoss"].BaseValue,
            ValueProp.Unblockable | ValueProp.Unpowered, cardPlay.Card.Owner.Creature);
    }
}
