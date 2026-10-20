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

    internal static PaladinShield? ProtectorFor(Creature target, ValueProp props)
    {
        // Unblockable non-attack effects are HP loss/payments, not damage to
        // share. This also makes transferred HP loss incapable of bouncing.
        if (target.Player == null || target.IsDead || target.CombatState is not { } combat ||
            combat.Players.Count < 2 || props.HasFlag(ValueProp.Unblockable) && !props.HasFlag(ValueProp.Move)) return null;
        return combat.Players.OrderBy(player => player.NetId)
            .Where(player => player.Creature != target && player.Creature.IsAlive &&
                player.Creature.CombatState == combat && player.Creature.CurrentHp * 4L > player.Creature.MaxHp)
            .SelectMany(player => player.Relics.OfType<PaladinShield>()).FirstOrDefault(relic => !relic.IsMelted);
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
            bool protectedNow = ProtectorFor(target, ValueProp.Move) != null;
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
            await CreatureCmd.Damage(context, transfer.Protector, transfer.Amount,
                ValueProp.Unblockable | ValueProp.Unpowered, null, null, null);
        }
        return results;
    }

    internal static MethodInfo NativeDamageMethod => typeof(CreatureCmd).GetMethods().Single(method =>
        method.Name == nameof(CreatureCmd.Damage) && method.GetParameters().Length == 7 &&
        method.GetParameters()[1].ParameterType == typeof(IEnumerable<Creature>) &&
        method.GetParameters()[2].ParameterType == typeof(decimal));
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
        if (hookCount != 3)
            throw new InvalidOperationException($"PaladinShield expected 3 HP guard calls; found {hookCount}.");
    }
}

[HarmonyPatch]
internal static class PaladinShieldDamageCompletion
{
    private static MethodBase TargetMethod() => PaladinShieldProtection.NativeDamageMethod;
    private static void Postfix(PlayerChoiceContext choiceContext, ref Task<IEnumerable<DamageResult>> __result) =>
        __result = PaladinShieldProtection.CompleteDamage(__result, choiceContext);
}
