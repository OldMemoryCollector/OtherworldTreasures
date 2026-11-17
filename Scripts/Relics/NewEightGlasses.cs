using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Relics;

// 新八的眼镜（来自《银魂》）：【吐槽达人】
// 每个玩家回合抽牌结束后判定一次：
//   手牌全为技能牌 → 抽 3 张攻击牌
//   手牌全为攻击牌 → 抽 3 张带格挡效果的技能牌
//   手牌全由诅咒/状态组成 → 场上只要有敌人准备攻击，抽 3 张带格挡的技能牌，否则抽 3 张攻击牌
// 隐藏效果与普通效果共用同一次判定，不重复触发。多敌人时任一名准备攻击就按攻击优先处理。
// 图标暂用原版占位图。
[RegisterRelic(typeof(SharedRelicPool))]
public class NewEightGlasses : ModRelicTemplate
{
    private const string PlaceholderIcon = "res://images/powers/missing_power.png";

    // 每次判定最多补充的张数
    private const int DrawCount = 3;

    public override RelicRarity Rarity => RelicRarity.Event;
    #if STS2_113
    protected override bool IsAllowedInternal(IRunState runState)
    #else
    public override bool IsAllowed(IRunState runState)
    #endif
        => DevelopmentContent.Gintama;
    public override bool IsAllowedInShops => DevelopmentContent.Gintama;
    public override RelicAssetProfile AssetProfile => new(
        IconPath: PlaceholderIcon,
        IconOutlinePath: PlaceholderIcon,
        BigIconPath: PlaceholderIcon
    );

    // 原版流程是「先抽手牌，再发 AfterPlayerTurnStart」，所以这里就是抽牌结束后的时点
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner)
        {
            await Judge(choiceContext, player);
        }

        await base.AfterPlayerTurnStart(choiceContext, player);
    }

    private async Task Judge(PlayerChoiceContext choiceContext, Player player)
    {
        var hand = CardPile.GetCards(player, PileType.Hand).ToList();
        if (hand.Count == 0)
        {
            return;
        }

        // 判定都基于这一次抽牌后的手牌快照，两条各自独立，可以同时触发
        if (!hand.Any(IsAttack))
        {
            await DrawMatching(choiceContext, player, IsAttack);
        }

        if (!hand.Any(IsBlockSkill))
        {
            await DrawMatching(choiceContext, player, IsBlockSkill);
        }
    }

    // 从抽牌堆里挑出符合条件的牌放进手牌
    private async Task DrawMatching(
        PlayerChoiceContext choiceContext, Player player, System.Func<CardModel, bool> predicate)
    {
        var picked = CardPile.GetCards(player, PileType.Draw)
            .Where(predicate)
            .Take(DrawCount)
            .ToList();

        if (picked.Count == 0)
        {
            return;
        }

        Flash();
        foreach (var card in picked)
        {
            await CardPileCmd.Add(card, PileType.Hand, CardPilePosition.Random, this);
        }

        Entry.Logger.Info($"[NewEightGlasses] 吐槽达人补充 {picked.Count} 张牌");
    }

    private static bool IsAttack(CardModel card) => card.Type == CardType.Attack;

    // 技能牌且带格挡数值，才算「有格挡效果的技能牌」
    private static bool IsBlockSkill(CardModel card)
        => card.Type == CardType.Skill && card.DynamicVars.ContainsKey("Block");
}
