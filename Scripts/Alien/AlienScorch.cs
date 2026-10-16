using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using OtherworldTreasures.Scripts.Powers;

namespace OtherworldTreasures.Scripts.Alien;

internal static class AlienScorch
{
    /// <summary>每次给予均获得太阳耀斑加成；热爆明确传入施法者，不借用空卡牌。</summary>
    internal static Task Apply(PlayerChoiceContext context, Creature target, int stacks, CardModel? source, Creature? applier = null)
    {
        var caster = applier ?? source?.Owner.Creature;
        if (stacks <= 0 || !target.IsAlive) return Task.CompletedTask;
        stacks += caster?.GetPower<HeatblastSolarFlarePower>()?.Amount ?? 0;
        return PowerCmd.Apply<AlienScorchPower>(context, target, stacks, caster, source);
    }

    internal static async Task<int> Clear(PlayerChoiceContext context, Creature target)
    {
        var power = target.GetPower<AlienScorchPower>();
        if (power == null) return 0;
        int stacks = power.Amount;
        await PowerCmd.Remove(power);
        return stacks;
    }
}
