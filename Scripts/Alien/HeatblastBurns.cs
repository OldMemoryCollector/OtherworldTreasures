using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;

namespace OtherworldTreasures.Scripts.Alien;

/// <summary>
/// 火焰人自己往手牌里塞【灼伤】状态牌的统一入口。
/// 塞进去的每一张都会打上"我方"标记：变身结束清理时**只清这些**，
/// 敌人塞进来的状态牌保持原样（原版【灼伤】模型同一个类，只能靠标记区分来源）。
/// </summary>
internal static class HeatblastBurns
{
    // 手牌上限（与太阳耀斑/熔岩之躯一致：手牌满就不再塞）
    private const int MaxHandSize = 10;

    private static readonly object Marker = new();

    // 我方生成的【灼伤】。用弱引用表，不阻止卡牌被回收
    private static readonly ConditionalWeakTable<CardModel, object> Ours = new();

    /// <summary>这张状态牌是不是我们自己塞进来的。</summary>
    public static bool IsOurs(CardModel card) => Ours.TryGetValue(card, out _);

    /// <summary>往手牌里塞 count 张【灼伤】（手牌满时提前停下）。</summary>
    public static async Task AddToHand(ICombatState combatState, Player player, int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (player.PlayerCombatState is not { } playerCombatState
                || playerCombatState.Hand.Cards.Count >= MaxHandSize)
            {
                return;
            }
            var burn = combatState.CreateCard(ModelDb.Card<Burn>(), player);
            Ours.Remove(burn);
            Ours.Add(burn, Marker);
            await CardPileCmd.AddGeneratedCardToCombat(burn, PileType.Hand, player, CardPilePosition.Random);
            Entry.Logger.Info(
                $"[Heatblast] 生成【灼伤】：Type={burn.Type}，Pile={burn.Pile?.Type.ToString() ?? "null"}，手牌 {playerCombatState.Hand.Cards.Count} 张");
        }
    }
}
