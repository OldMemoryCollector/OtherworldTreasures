using System;
using System.Threading;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using OtherworldTreasures.Scripts.Powers;

namespace OtherworldTreasures.Scripts.Alien;

internal static class GhostFear
{
    private static readonly AsyncLocal<Creature?> BlockOnly = new();
    internal static bool InBlockOnlyMove => BlockOnly.Value != null;
    internal static bool IsBlockOnlyMove(Creature enemy) => BlockOnly.Value == enemy;
    internal static IDisposable EnterBlockOnlyMove(Creature enemy) => new BlockScope(enemy);
    private sealed class BlockScope : IDisposable
    {
        private readonly Creature? _previous = BlockOnly.Value;
        internal BlockScope(Creature enemy) => BlockOnly.Value = enemy;
        public void Dispose() => BlockOnly.Value = _previous;
    }
    internal static int Stage(Creature target) => target.GetPower<AlienTerrorPower>()?.Stage ?? 0;
    internal static bool Panicked(Creature? target) => target != null && Stage(target) >= 1;
    internal static bool Escaping(Creature target) => Stage(target) >= 2;
    internal static async Task<bool> Apply(PlayerChoiceContext c, Creature target, int amount, Creature caster, CardModel? source)
    {
        if (!target.IsAlive || amount <= 0) return false;
        int before = Stage(target);
        int bonus = caster.GetPower<GhostfreakTrueFormPower>()?.Amount ?? 0;
        var applied = await PowerCmd.Apply<AlienTerrorPower>(c, target, amount + bonus, caster, source);
        // 进入魂飞的伤害可能使敌人当场死亡，仍按本次实际施加后的阶段判定联动。
        return applied != null && applied.Stage > before;
    }
    // 用户修订：鬼影造成的逃跑按击杀结算，走原版死亡/奖励钩子，不加入 EscapedCreatures。
    internal static Task DefeatByFear(Creature enemy) => CreatureCmd.Kill(enemy, force: true);
}
