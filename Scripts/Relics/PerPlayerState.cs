using System.Runtime.CompilerServices;
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
// 以玩家实例为键：同一 NetId 在新局/读档后会复用，但玩家对象不会。
// 弱引用表不会把已经结束的跑局及其卡牌、Godot 节点永久留在内存中。
internal static class PerPlayerState<T> where T : class, new()
{
    private static readonly ConditionalWeakTable<Player, T> s_map = new();

    internal static T Of(Player? owner)
    {
        // 图鉴里的 canonical 模型没有玩家，不能和 NetId=0 的实际玩家共享状态。
        return owner == null ? new T() : s_map.GetValue(owner, static _ => new T());
    }

    internal static void ClearAll() => s_map.Clear();
}
