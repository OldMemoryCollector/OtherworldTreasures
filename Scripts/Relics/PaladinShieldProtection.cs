using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using OtherworldTreasures.Scripts.Powers;

namespace OtherworldTreasures.Scripts.Relics;

internal static class PaladinShieldProtection
{
    [ThreadStatic] private static int _committedHpModifiers;
    internal static bool IsCommittingNativeHpModifiers => _committedHpModifiers > 0;
    private sealed record Transfer(Creature Protector, int Amount);
    private static readonly ConditionalWeakTable<DamageResult, Transfer> PendingTransfers = new();
    private static readonly AsyncLocal<int> TransferDepth = new();
    private sealed class EnemyPhase
    {
        internal int Round;
        internal bool Active;
        internal readonly HashSet<PaladinShield> Qualified = new();
    }
    private static readonly ConditionalWeakTable<ICombatState, EnemyPhase> EnemyPhases = new();

    internal static Task Reset(ICombatState? combat)
    {
        if (combat != null) EnemyPhases.Remove(combat);
        return Refresh(combat);
    }

    internal static Task BeginSideTurn(ICombatState combat, CombatSide side)
    {
        var phase = EnemyPhases.GetOrCreateValue(combat);
        if (side == CombatSide.Player)
        {
            phase.Active = false;
            phase.Qualified.Clear();
        }
        else if (!phase.Active || phase.Round != combat.RoundNumber)
        {
            // Snapshot once before any enemy starts acting. Losing block later
            // does not switch protection off halfway through this phase.
            phase.Round = combat.RoundNumber;
            phase.Active = true;
            phase.Qualified.Clear();
            foreach (var player in combat.Players.OrderBy(player => player.NetId))
            {
                var creature = player.Creature;
                if (creature.IsDead || creature.Block * 4L < creature.MaxHp) continue;
                foreach (var relic in player.Relics.OfType<PaladinShield>().Where(relic => !relic.IsMelted))
                    phase.Qualified.Add(relic);
            }
        }
        return Refresh(combat);
    }

    internal static PaladinShield? ProtectorFor(Creature target, ValueProp props, bool ignoreTransferGuard = false)
    {
        // HP payments/Poison are not shared. A transfer can consume the
        // protector's block, but must never bounce to another shield owner.
        if ((!ignoreTransferGuard && TransferDepth.Value > 0) || target.Player == null || target.IsDead ||
            target.CombatState is not { } combat || combat.Players.Count < 2 ||
            combat.CurrentSide != CombatSide.Enemy ||
            !EnemyPhases.TryGetValue(combat, out var phase) || !phase.Active ||
            phase.Round != combat.RoundNumber ||
            props.HasFlag(ValueProp.Unblockable) && !props.HasFlag(ValueProp.Move)) return null;
        return combat.Players.OrderBy(player => player.NetId)
            .Where(player => player.Creature != target && player.Creature.IsAlive &&
                player.Creature.CombatState == combat)
            .SelectMany(player => player.Relics.OfType<PaladinShield>())
            .FirstOrDefault(relic => !relic.IsMelted && phase.Qualified.Contains(relic));
    }

    internal static int SharedAmount(Creature target, decimal amount, ValueProp props) =>
        ProtectorFor(target, props) == null ? 0 : (int)(Math.Clamp(decimal.Truncate(amount), 0m, 999999999m) / 4m);

    internal static async Task Refresh(ICombatState? combat)
    {
        if (combat == null || !CombatManager.Instance.IsInProgress || CombatManager.Instance.IsEnding) return;
        foreach (var player in combat.Players.ToArray())
        {
            var target = player.Creature;
            var current = target.GetPower<PaladinShieldPower>();
            bool protectedNow = ProtectorFor(target, ValueProp.Move, ignoreTransferGuard: true) != null;
            if (protectedNow && current == null)
                await PowerCmd.Apply<PaladinShieldPower>(new ThrowingPlayerChoiceContext(), target, 25m, null, null);
            else if (!protectedNow && current != null) await PowerCmd.Remove(current);
        }
    }

    // This wrapper is called only by CreatureCmd.Damage's committed pipeline.
    // It leaves the global Hook implementation and all native HP guards intact.
    internal static decimal ModifyCommittedHpLoss(IRunState run, ICombatState? combat, Creature target,
        decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, HpLossHookPhase phases,
        out IEnumerable<AbstractModel> modifiers)
    {
        _committedHpModifiers++;
        try { return Hook.ModifyHpLost(run, combat, target, amount, props, dealer, cardSource, phases, out modifiers); }
        finally { _committedHpModifiers--; }
    }

    internal static DamageResult ApplySharedHpLoss(Creature target, decimal amount, ValueProp props, int absorbed = 0)
    {
        var protector = ProtectorFor(target, props);
        int shared = protector == null ? 0 : SharedAmount(target, amount, props);
        var result = target.LoseHpInternal(amount - shared, props);
        if (absorbed > 0) result = new DamageResult(target, props)
        {
            UnblockedDamage = result.UnblockedDamage + absorbed,
            WasTargetKilled = result.WasTargetKilled,
            OverkillDamage = result.OverkillDamage
        };
        if (shared > 0) PendingTransfers.Add(result, new Transfer(protector!.Owner.Creature, shared));
        return result;
    }

    internal static async Task<IEnumerable<DamageResult>> CompleteDamage(Task<IEnumerable<DamageResult>> task,
        PlayerChoiceContext context)
    {
        var results = (await task).ToList();
        foreach (var result in results)
        {
            if (!PendingTransfers.TryGetValue(result, out var transfer)) continue;
            PendingTransfers.Remove(result);
            if (transfer.Protector.IsDead) continue;
            transfer.Protector.Player?.Relics.OfType<PaladinShield>().FirstOrDefault()?.Flash();
            result.Receiver.GetPower<PaladinShieldPower>()?.FlashProtection();
            TransferDepth.Value++;
            try
            {
                await CreatureCmd.Damage(context, transfer.Protector, transfer.Amount,
                    ValueProp.Unpowered, null, null, null);
            }
            finally { TransferDepth.Value--; }
        }
        return results;
    }

    internal static MethodInfo NativeDamageMethod =>
        OtherworldTreasures.Scripts.Compatibility.NativeDamagePipeline.Method;
}

[HarmonyPatch]
internal static class PaladinShieldDamageAdapter
{
    private static MethodBase TargetMethod() => AccessTools.Method(
        PaladinShieldProtection.NativeDamageMethod.GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType, "MoveNext");
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var hpHook = AccessTools.Method(typeof(Hook), nameof(Hook.ModifyHpLost));
        int hookCount = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(hpHook))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = AccessTools.Method(typeof(PaladinShieldProtection), nameof(PaladinShieldProtection.ModifyCommittedHpLoss));
                hookCount++;
            }
            yield return instruction;
        }
        if (hookCount != OtherworldTreasures.Scripts.Compatibility.NativeDamagePipeline.HpGuardCallSites)
            throw new InvalidOperationException($"PaladinShield expected {OtherworldTreasures.Scripts.Compatibility.NativeDamagePipeline.HpGuardCallSites} HP guard calls; found {hookCount}.");
    }
}

[HarmonyPatch]
internal static class PaladinShieldDamageCompletion
{
    private static MethodBase TargetMethod() => PaladinShieldProtection.NativeDamageMethod;
    private static void Postfix(PlayerChoiceContext choiceContext, ref Task<IEnumerable<DamageResult>> __result) =>
        __result = PaladinShieldProtection.CompleteDamage(__result, choiceContext);
}
