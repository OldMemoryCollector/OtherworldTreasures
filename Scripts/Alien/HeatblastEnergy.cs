using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace OtherworldTreasures.Scripts.Alien;

/// <summary>实际能量增长由热爆的 EnergyChanged 事件观察，含其他遗物/能力；这里不重复计数。</summary>
internal static class HeatblastEnergy
{
    internal static Task GainExtra(PlayerChoiceContext context, Player player, decimal amount)
        => amount > 0 ? PlayerCmd.GainEnergy(amount, player) : Task.CompletedTask;
}
