using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace OtherworldTreasures.Scripts.Alien;

/// <summary>
/// 手牌 UI 同步工具。
///
/// 背景：用 <c>skipVisuals: true</c> 静默把牌移出手牌时，**手牌 UI 不会自己撤掉那张牌的节点**，
/// 结果是"看得见、点不动、也不会再被抓取"的幽灵牌（牌其实已经在别的牌堆里了）。
/// 回合管线里不能开动画（会 await 飞行动画导致卡死），所以只能静默移动 + 手动撤节点。
/// </summary>
internal static class HandUiSync
{
    /// <summary>把这几张牌的卡牌节点从手牌 UI 上撤掉（调用前请确保它们确实原本在手牌里）。</summary>
    public static void RemoveFromHandUi(IEnumerable<CardModel> cards)
    {
        var hand = NCombatRoom.Instance?.Ui?.Hand;
        if (hand == null)
        {
            return;
        }
        foreach (var card in cards)
        {
            try
            {
                hand.Remove(card);
            }
            catch (Exception e)
            {
                Entry.Logger.Warn($"[Alien] 撤手牌节点失败（已忽略）：{e.Message}");
            }
        }
    }
}
