using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Players;

namespace OtherworldTreasures.Scripts.Relics;

// 按玩家隔离的可变状态容器。
//
// 为什么需要它：我们的遗物有不少"本场战斗只能用一次/需要跨战斗保留"的状态，
// 原先放在 static 字段里（因为战斗中遗物会被克隆，实例字段不可靠）。
// 但 static 是**进程级**的——两名玩家都持有同一件遗物时，两份状态会变成同一份，
// 表现为"一个人用了，另一个人也用不了"（库拉的骰子就是这么发现的）。
//
// 用法：为每件遗物定义一个状态类，然后用 PerPlayerState<状态类>.Of(owner) 取它。
// 每个 T 各自一张表，互不干扰。新的一局由各遗物在自己的 AfterObtained 里 ClearAll()。
internal static class PerPlayerState<T> where T : class, new()
{
    private static readonly Dictionary<ulong, T> s_map = new();

    internal static T Of(Player? owner)
    {
        var netId = owner?.NetId ?? 0UL;
        if (!s_map.TryGetValue(netId, out var state))
        {
            state = new T();
            s_map[netId] = state;
        }
        return state;
    }

    internal static void ClearAll() => s_map.Clear();
}
