using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;

namespace OtherworldTreasures.Scripts.Events;

// === SCP-914 等级规则层 ===
// 2026-10-17 位移制重做：这是玩家确认过的主动改造，不是故障、不是回归。
// 旧实现按"稀有度数字 +1"直接产卡/产遗物，导致诅咒卡一步变蓝卡、产出池混入诅咒与基础牌。
// 现在统一成"档位位移"：所有加工都只决定往上一档/下一档/不动，越界由本类判定。
//
// 档位阶梯（-1 → 0 → 1 → 2 → 3 → 4）：
//   -1 诅咒 / 状态
//    0 基础卡 / 普通卡；遗物：起始、无稀有度、普通
//    1 罕见
//    2 稀有；遗物：稀有、商店
//    3 "双份稀有"：产出 2 件稀有（不是真实稀有度，只能作为结果出现）
//    4 先古
//
// 遗物特例：事件遗物可以投入（按 1 档参与位移），但永远不会作为产出出现。
internal static class Scp914Ranks
{
    internal const int MinRank = -1;

    internal const int MaxRank = 4;

    // 档位 3 不是稀有度，而是"两份稀有"这一结果档；卡与遗物对称。
    internal const int DoubleRank = 3;

    // Move() 的两个哨兵结果：低于 0 档 = 销毁；高于先古 = 模组专属先古的溢出分支。
    internal const int Destroyed = -1000;

    internal const int Overflow = 1000;

    private static readonly int[] Ladder = [MinRank, 0, 1, 2, DoubleRank, MaxRank];

    internal static int RankOf(CardRarity rarity) => rarity switch
    {
        CardRarity.Curse or CardRarity.Status => MinRank,
        CardRarity.Basic or CardRarity.Common => 0,
        CardRarity.Uncommon => 1,
        CardRarity.Rare => 2,
        CardRarity.Ancient => MaxRank,
        _ => Destroyed
    };

    internal static int RankOf(RelicRarity rarity) => rarity switch
    {
        RelicRarity.Starter or RelicRarity.None or RelicRarity.Common => 0,
        // 事件遗物只作为投入物存在，按 1 档参与位移（用户 2026-10-17 明确：事件遗物等级为 1）
        RelicRarity.Uncommon or RelicRarity.Event => 1,
        // 商店遗物可以投入也可以产出，与稀有同档
        RelicRarity.Rare or RelicRarity.Shop => 2,
        RelicRarity.Ancient => MaxRank,
        _ => Destroyed
    };

    /// <summary>
    /// 沿阶梯位移。delta 为正表示升档、为负表示降档、0 表示原样。
    /// 返回销毁 / 溢出哨兵，或新的档位。
    /// </summary>
    internal static int Move(int rank, int delta)
    {
        int index = Array.IndexOf(Ladder, rank);
        if (index < 0)
        {
            return Destroyed;
        }
        int target = index + delta;
        // 阶梯上的 -1 档（诅咒）只用于"投入物本身的档位"，从不作为产出档位：
        // 降级只要落到 0 档以下（含 -1）就损毁；+0 不经过这里，所以 -1 + 0 仍然是诅咒（用户 2026-10-17）。
        if (target < 0 || Ladder[target] < 0)
        {
            return Destroyed;
        }
        if (target >= Ladder.Length)
        {
            return Overflow;
        }
        return Ladder[target];
    }

    internal static bool IsDestroyed(int rank) => rank == Destroyed;

    internal static bool IsOverflow(int rank) => rank == Overflow;

    /// <summary>结果档位是否是"两份稀有"。</summary>
    internal static bool IsDouble(int rank) => rank == DoubleRank;

    /// <summary>极精加工成功率（2026-10-18 调整）：普通/诅咒 50%、罕见 30%、稀有 10%、先古 5%，其余为失败。</summary>
    internal static int VeryFineSuccessPercent(int rank) => rank switch
    {
        MinRank or 0 => 50,
        1 => 30,
        2 => 10,
        MaxRank => 5,
        _ => 0
    };
}
