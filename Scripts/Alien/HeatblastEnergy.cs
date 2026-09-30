using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using OtherworldTreasures.Scripts.Powers;

namespace OtherworldTreasures.Scripts.Alien;

/// <summary>
/// 火焰人额外能量获得入口。
/// 所有"通过卡牌/能力获得额外能量"的效果都必须走这里，
/// 以便统一统计本回合额外能量（供喷射突进、流星冲击判定）
/// 并通知【超燃加速】（每次获得能量事件触发一次伤害充能）。
///
/// 不包括：回合开始的基础能量、熔岩之躯的能量上限 +1（那是 ModifyMaxEnergy，不经过这里）。
/// </summary>
internal static class HeatblastEnergy
{
    /// <summary>获得额外能量，并更新熔岩之躯计数器 + 通知超燃加速。</summary>
    internal static async Task GainExtra(PlayerChoiceContext choiceContext, Player player, decimal amount)
    {
        if (amount <= 0)
        {
            return;
        }
        await PlayerCmd.GainEnergy(amount, player);

        var creature = player.Creature;
        if (creature == null)
        {
            return;
        }

        var passive = creature.GetPower<HeatblastPassivePower>();
        if (passive != null)
        {
            passive.ExtraEnergyGainedThisTurn += (int)amount;
            passive.HasGainedExtraEnergyThisTurn = true;
        }

        // 通知超燃加速：按"获得能量事件"触发（一次调用 = 一次事件，不论 amount 多少）
        var sc = creature.GetPower<HeatblastSupercombustionPower>();
        sc?.OnExtraEnergyGained();
    }
}
