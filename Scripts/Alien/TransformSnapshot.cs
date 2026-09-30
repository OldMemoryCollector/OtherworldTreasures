using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using OtherworldTreasures.Scripts.Cards.Alien;
using STS2RitsuLib.CardPiles;

namespace OtherworldTreasures.Scripts.Alien;

/// <summary>
/// 变身前状态快照：记下玩家战斗内的牌堆与自身能力，变身结束时原样还原。
///
/// 要点：**不碰 run 的主牌组（Player.Deck）** —— 原始卡牌只是整体挪进一个永不显示的
/// "暂存堆"（Entry.OmnitrixStashPile，Headless），变身结束再整体挪回抽牌堆。
/// 所以变身结束、战斗提前结束、读档都不会污染玩家的真实牌组。
/// </summary>
internal sealed class TransformSnapshot
{
    // 原始牌堆内容（变身结束时整体塞回抽牌堆，不逐堆还原）
    private readonly List<CardModel> _originalCards = new();

    // 自身能力快照：能力实例 + 记录时的层数
    private readonly List<(PowerModel Power, int Amount)> _selfPowers = new();

    // 玩家战斗内的牌堆（主牌组不在其中）。
    // 注意：变身时"挪进暂存堆"会跳过消耗堆（见 Capture），但还原时仍要连消耗堆一起扫，才能把英雄牌清干净。
    private static IEnumerable<CardPile> CombatPiles(PlayerCombatState combatState)
    {
        yield return combatState.Hand;
        yield return combatState.DrawPile;
        yield return combatState.DiscardPile;
        yield return combatState.ExhaustPile;
        yield return combatState.PlayPile;
    }

    /// <summary>变身瞬间调用：把原始牌堆搬出战斗（暂存）并记录自身能力。</summary>
    public static async Task<TransformSnapshot> Capture(Player player)
    {
        var snapshot = new TransformSnapshot();
        var combatState = player.PlayerCombatState;
        if (combatState == null)
        {
            return snapshot;
        }

        // 1) 牌堆：手牌 / 抽牌 / 弃牌 / 出牌。
        //    **消耗堆不参与**：变身之前就已消耗掉的牌应当一直留在消耗堆里，
        //    不能被带进暂存堆、再在变身结束时塞回抽牌堆（那等于把消耗掉的牌复活）。
        foreach (var pile in CombatPiles(combatState))
        {
            if (pile == combatState.ExhaustPile)
            {
                continue;
            }
            foreach (var card in pile.Cards.ToList())
            {
                snapshot._originalCards.Add(card);
            }
        }
        if (snapshot._originalCards.Count > 0)
        {
            // 原始牌堆整体挪进"暂存堆"（Headless，永不显示；这是独立的战斗牌堆，不是主牌组）
            await CardPileCmd.Add(snapshot._originalCards.ToList(), Entry.OmnitrixStashPile);
        }

        // 2) 自身能力（正面负面都记；变身后新增的会被移回/移除）
        foreach (var power in player.Creature.Powers.ToList())
        {
            snapshot._selfPowers.Add((power, power.Amount));
        }
        Entry.Logger.Info($"[Omnitrix] 快照：牌堆 {snapshot._originalCards.Count} 张，自身能力 {snapshot._selfPowers.Count} 个");
        return snapshot;
    }

    /// <summary>
    /// 变身结束调用：清掉英雄专属牌、把原始牌堆整体塞回抽牌堆、自身能力回滚到快照。
    /// 时点选在"玩家回合结束、手牌已空"时执行（见 Omnitrix.StartTransformationEnd）。
    /// </summary>
    public async Task Restore(Player player)
    {
        try
        {
            var combatState = player.PlayerCombatState;
            if (combatState == null)
            {
                return;
            }

            // 1) 清掉英雄专属牌。调用时点特意选在"玩家回合已结束、手牌是空的"时候（见 Omnitrix.StartTransformationEnd），
            //    所以这里只涉及不渲染的牌堆，不会像以前那样在手牌里留下卡牌节点"尸体"。
            int removedAlienCards = await RemoveAlienCards(player);
            // 1b) 清掉手里**我们自己塞进来**的状态牌（【灼伤】）：熔岩之躯一撤掉，
            //     这些牌既不会被燃尽、还会在回合末反过来伤害玩家。敌人塞进来的保持原样。
            int removedStatusCards = await RemoveStatusCardsInHand(player);

            // 2) 从暂存堆里把原始牌堆整体挪回抽牌堆（下回合正常抽回来）
            var originals = Entry.OmnitrixStashPile.GetPile(player).Cards.ToList();
            if (originals.Count > 0)
            {
                await CardPileCmd.Add(originals, PileType.Draw, CardPilePosition.Bottom, null!, false);
            }

            // 3) 自身能力回滚：快照里有的恢复层数，快照里没有的（alien 造成的）移除
            var snapshotAmounts = _selfPowers.ToDictionary(p => p.Power, p => p.Amount, ReferenceEqualityComparer.Instance);
            foreach (var power in player.Creature.Powers.ToList())
            {
                if (snapshotAmounts.TryGetValue(power, out int amount))
                {
                    if (power.Amount != amount)
                    {
                        power.SetAmount(amount, silent: true);
                    }
                }
                else
                {
                    await PowerCmd.Remove(power);
                }
            }

            Entry.Logger.Info(
                $"[Omnitrix] 还原：移出英雄牌 {removedAlienCards} 张、残留状态牌 {removedStatusCards} 张，原始牌堆 {originals.Count} 张塞回抽牌堆");
        }
        catch (Exception e)
        {
            // 还原跑在回合结束的钩子里：抛异常会打断回合管线（表现为卡死），必须吞掉
            Entry.Logger.Warn($"[Omnitrix] 还原失败（已忽略，避免打断回合管线）：{e}");
        }
        finally
        {
            _originalCards.Clear();
            _selfPowers.Clear();
        }
    }

    /// <summary>
    /// 把战斗内所有英雄专属牌移出战斗，返回移除张数。
    /// 扫 PlayerCombatState.AllPiles（手牌/抽牌/弃牌/消耗/出牌 5 个堆）。
    /// 注意：带【保留】的英雄牌（炎狱天降）在回合交替期间可能还没回到手牌，一次扫不干净，
    /// 所以调用方会在之后几回合的回合开始再补扫（见 Omnitrix.AfterPlayerTurnStart）。
    /// </summary>
    public static async Task<int> RemoveAlienCards(Player player)
    {
        var combatState = player.PlayerCombatState;
        if (combatState == null)
        {
            return 0;
        }
        var alienCards = combatState.AllPiles
            .SelectMany(pile => pile.Cards)
            .Where(card => card is IAlienCard)
            .ToList();
        if (alienCards.Count == 0)
        {
            return 0;
        }
        // 静默移除不会撤掉手牌 UI 上的卡牌节点（会留下"看得见却点不动"的幽灵牌），
        // 所以先记下哪些在手牌里，移完之后手动撤节点
        var inHand = alienCards.Where(card => combatState.Hand.Cards.Contains(card)).ToList();
        await CardPileCmd.RemoveFromCombat(alienCards, skipVisuals: true);
        HandUiSync.RemoveFromHandUi(inHand);
        return alienCards.Count;
    }

    /// <summary>
    /// 把玩家手牌里**由我们自己塞进来的**状态牌移出战斗，返回移除张数。
    /// 变身结束时用：这些牌是变身期间我们的牌/被动生成的（【灼伤】），
    /// 熔岩之躯一撤掉它们既不会被燃尽、还会在回合末反过来伤害玩家，所以一并清掉。
    /// 敌人塞进来的状态牌**保持原样**（原版【灼伤】是同一个卡牌模型，靠 HeatblastBurns 的标记区分来源）。
    /// </summary>
    public static async Task<int> RemoveStatusCardsInHand(Player player)
    {
        var combatState = player.PlayerCombatState;
        if (combatState == null)
        {
            return 0;
        }
        var statusCards = combatState.Hand.Cards.Where(HeatblastBurns.IsOurs).ToList();
        if (statusCards.Count == 0)
        {
            return 0;
        }
        await CardPileCmd.RemoveFromCombat(statusCards, skipVisuals: true);
        // 同样要手动撤掉手牌 UI 上的节点，否则会留下幽灵牌
        HandUiSync.RemoveFromHandUi(statusCards);
        return statusCards.Count;
    }
}
