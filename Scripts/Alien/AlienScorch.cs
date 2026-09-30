using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using OtherworldTreasures.Scripts.Powers;

namespace OtherworldTreasures.Scripts.Alien;

/// <summary>
/// 灼伤施加入口：所有"给敌人加灼伤"的卡都必须走这里。
/// 统一处理：
/// - 【燃烧反应】：施法者身上若有该标记（熔岩之躯上的 CombustionReactionBonus），
///   本次施加额外 +N 层，并消耗标记。
/// - 【太阳耀斑】：本回合首次给予灼伤时额外 +2 层（标记在 HeatblastSolarFlarePower 实例上）。
/// 叠加顺序（按设计第十二节）：先燃烧反应 +N，再太阳耀斑 +2。
/// </summary>
internal static class AlienScorch
{
    internal static async Task Apply(PlayerChoiceContext choiceContext, Creature target, int stacks, CardModel? source)
    {
        // 施法者 = 出这张牌的玩家所在生物（所有权在玩家身上）
        var caster = source?.Owner?.Creature;

        if (caster != null && stacks > 0)
        {
            // ① 燃烧反应：先结算 +N
            var passive = caster.GetPower<HeatblastPassivePower>();
            if (passive != null && passive.CombustionReactionBonus > 0)
            {
                stacks += passive.CombustionReactionBonus;
                passive.CombustionReactionBonus = 0;
            }

            // ② 太阳耀斑：本回合首次给予灼伤时额外 +2 层
            var flare = caster.GetPower<HeatblastSolarFlarePower>();
            if (flare != null && flare.TryTriggerFirstScorch())
            {
                stacks += HeatblastSolarFlarePower.BonusStacks;
            }
        }

        if (stacks > 0)
        {
            await PowerCmd.Apply<AlienScorchPower>(choiceContext, target, stacks, caster!, source!);
        }
    }

    /// <summary>
    /// 引爆：清除目标身上全部灼伤，返回被清除的层数（用于计算引爆伤害）。
    /// 引爆后目标灼伤归零，不可二次引爆。
    /// </summary>
    internal static async Task<int> Detonate(PlayerChoiceContext choiceContext, Creature target)
    {
        var power = target.GetPower<AlienScorchPower>();
        if (power == null)
        {
            return 0;
        }
        int stacks = power.Amount;
        await PowerCmd.Remove(power);
        return stacks;
    }

    /// <summary>
    /// 清除：移除目标身上的灼伤，不产生引爆伤害。返回被清除的层数。
    /// </summary>
    internal static async Task<int> Clear(PlayerChoiceContext choiceContext, Creature target)
    {
        var power = target.GetPower<AlienScorchPower>();
        if (power == null)
        {
            return 0;
        }
        int stacks = power.Amount;
        await PowerCmd.Remove(power);
        return stacks;
    }
}
