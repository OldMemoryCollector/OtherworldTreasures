using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Relics;

// 十字章护身符（来自《泰拉瑞亚》Cross Necklace）：
// 抽到状态牌或诅咒牌时将其消耗并抽一张牌；回合结束时，将手牌中的状态牌与诅咒牌消耗，获得 2 点格挡。
[RegisterRelic(typeof(SharedRelicPool))]
public class CrossNecklace : ModRelicTemplate
{
    private const int BlockAmount = 2;

    // 由先古之民给予，使用 Ancient 稀有度
    public override RelicRarity Rarity => RelicRarity.Ancient;

    protected override IEnumerable<DynamicVar> CanonicalVars => new[]
    {
        new BlockVar(BlockAmount, ValueProp.Unpowered)
    };

    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://OtherworldTreasures/images/relics/Cross_Necklace.png",
        IconOutlinePath: "res://OtherworldTreasures/images/relics/Cross_Necklace.png",
        BigIconPath: "res://OtherworldTreasures/images/relics/Cross_Necklace.png"
    );

    // 抽到状态牌/诅咒牌：消耗并抽一张牌（替换牌若也是状态/诅咒会继续触发，直至抽到正常牌或牌库抽空）
    public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (card.Owner == Owner && IsJunk(card))
        {
            Flash();
            await CardCmd.Exhaust(choiceContext, card);
            await CardPileCmd.Draw(choiceContext, Owner);
        }
        await base.AfterCardDrawn(choiceContext, card, fromHandDraw);
    }

    // 回合结束：消耗手牌中所有状态牌/诅咒牌，获得 2 点格挡
    public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner.Creature))
        {
            // 快照：消耗过程中牌堆会变化
            var junkInHand = CardPile.GetCards(Owner, PileType.Hand).Where(IsJunk).ToList();
            if (junkInHand.Count > 0)
            {
                Flash();
                foreach (var card in junkInHand)
                {
                    await CardCmd.Exhaust(choiceContext, card);
                }
                await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, null);
            }
        }
        await base.BeforeSideTurnEnd(choiceContext, side, participants);
    }

    private static bool IsJunk(CardModel card)
    {
        return card.Type == CardType.Status || card.Type == CardType.Curse;
    }
}
