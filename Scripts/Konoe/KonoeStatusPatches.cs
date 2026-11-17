using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.ValueProps;

namespace OtherworldTreasures.Scripts.Konoe;

// Frozen no longer prevents actions. Preserve the independent rare-card interrupt.
// Electric orbs resolve before the original move delegate, exactly once.
[HarmonyPatch(typeof(MoveState), nameof(MoveState.PerformMove))]
internal static class KonoeFrozenMoveGate
{
    private static bool Prefix(MoveState __instance, IEnumerable<Creature> targets,
        Func<IReadOnlyList<Creature>, Task> ____onPerform, ref bool ____performedAtLeastOnce, ref Task __result)
    {
        var list = targets.ToArray();
        var actor = list.FirstOrDefault()?.CombatState?.Enemies.FirstOrDefault(e => e.IsAlive && ReferenceEquals(e.Monster?.NextMove, __instance));
        if (actor == null) return true;
        bool interrupted = actor.Monster?.IntendsToAttack == true && actor.GetPower<KonoeAttackInterruptedPower>() is { Amount: > 0 };
        var orbs = actor.Powers.OfType<KonoeAmethystEchoPower>().ToArray();
        if (!interrupted && orbs.Length == 0) return true;
        ____performedAtLeastOnce = true;
        __result = Resolve();
        return false;
        async Task Resolve()
        {
            foreach (var orb in orbs) if (actor.IsAlive) await orb.ResolveBeforeMove(new BlockingPlayerChoiceContext());
            if (actor.IsAlive && !interrupted) await ____onPerform(list);
        }
    }
}

[HarmonyPatch(typeof(Creature), nameof(Creature.DamageBlockInternal))]
internal static class KonoeKnockdownBlockBypass
{
    private static void Prefix(Creature __instance, ref ValueProp props)
    {
        if (props.IsCardOrMonsterMove() && KonoeElementEffects.CardAttackHitting(__instance) && __instance.GetPower<KonoeKnockdownPower>() is { Amount: > 0 }) props |= ValueProp.Unblockable;
    }
}

// Safety net only: a creature that is no longer attached to a combat has nothing to clear.
// The turn loop snapshots creaturesStartingTurn before turn-start hooks run, so anything that dies
// during BeforeSideTurnStart stays in that snapshot with CombatState == null; without this guard
// Hook.ShouldClearBlock(null, ...) throws and the entire turn loop dies (combat soft-locks).
[HarmonyPatch(typeof(Creature), "ClearBlock")]
internal static class KonoeClearBlockGuard
{
    private static bool Prefix(Creature __instance, ref Task __result)
    {
        if (__instance.CombatState != null) return true;
        __result = Task.CompletedTask;
        return false;
    }
}
