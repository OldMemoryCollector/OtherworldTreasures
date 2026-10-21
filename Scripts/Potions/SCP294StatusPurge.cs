using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Random;

namespace OtherworldTreasures.Scripts.Potions;

public enum StatusPurgeTarget { Enemies, Allies, Everyone }
public enum StatusPurgeSelection { Single, All }
public enum StatusPurgeLayers { One, Two, Three, Half, All }
public enum StatusPurgePolarity { Positive, Negative, All }
public sealed record StatusPurgeRule
{
    public bool Enabled { get; init; }
    public int Interval { get; init; } = 1;
    public StatusPurgeTarget Target { get; init; } = StatusPurgeTarget.Everyone;
    public StatusPurgeSelection Selection { get; init; } = StatusPurgeSelection.All;
    public StatusPurgeLayers Layers { get; init; } = StatusPurgeLayers.One;
    public StatusPurgePolarity Polarity { get; init; } = StatusPurgePolarity.Negative;
}

internal static class Scp294StatusPurge
{
    private sealed class RoundStamp { internal readonly Dictionary<StatusPurgeTarget, int> LastRounds = new(); }
    private static readonly ConditionalWeakTable<CombatState, RoundStamp> Rounds = new();
    internal static bool ValidInterval(string? text) => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int n) && n > 0;
    internal static void Migrate(Scp294Config data)
    {
        if (data.StatusPurgeGroupsInitialized) return;
        data.StatusPurgeGroupsInitialized = true;
        if (!data.StatusPurgeEnabled) return;
        var target = Enum.IsDefined(data.StatusPurgeTarget) ? data.StatusPurgeTarget : StatusPurgeTarget.Everyone;
        var group = Options(data, target);
        group.Enabled = true;
        group.Interval = data.StatusPurgeInterval;
        group.Selection = data.StatusPurgeSelection;
        group.Layers = data.StatusPurgeLayers;
        group.Polarity = data.StatusPurgePolarity;
    }
    internal static StatusPurgeGroupConfig Options(Scp294Config data, StatusPurgeTarget target)
    {
        Migrate(data);
        return target switch
        {
            StatusPurgeTarget.Enemies => data.StatusPurgeEnemies ??= new(),
            StatusPurgeTarget.Allies => data.StatusPurgeAllies ??= new(),
            _ => data.StatusPurgeEveryone ??= new()
        };
    }
    internal static StatusPurgeRule[] CaptureAll(Scp294Config data) => Enum.GetValues<StatusPurgeTarget>()
        .Select(target => CaptureGroup(data, target)).ToArray();
    private static StatusPurgeRule CaptureGroup(Scp294Config data, StatusPurgeTarget target)
    {
        var group = Options(data, target);
        bool valid = ValidInterval(group.Interval);
        return new StatusPurgeRule
        {
            Enabled = data.EverywhereMode && group.Enabled && valid,
            Interval = valid ? int.Parse(group.Interval, CultureInfo.InvariantCulture) : 1,
            Target = target,
            Selection = Enum.IsDefined(group.Selection) ? group.Selection : StatusPurgeSelection.All,
            Layers = Enum.IsDefined(group.Layers) ? group.Layers : StatusPurgeLayers.One,
            Polarity = Enum.IsDefined(group.Polarity) ? group.Polarity : StatusPurgePolarity.Negative
        };
    }
    internal static IReadOnlyList<StatusPurgeRule> SavedRules(CrazyPotionRunSave saved) =>
        saved.StatusPurges ?? [saved.StatusPurge ?? new StatusPurgeRule()];
    internal static bool Includes(StatusPurgeTarget target, CombatSide side) => target switch
    {
        StatusPurgeTarget.Enemies => side == CombatSide.Enemy,
        StatusPurgeTarget.Allies => side == CombatSide.Player,
        _ => true
    };
    internal static long RemovedLayers(long layers, StatusPurgeLayers option) => Math.Min(layers, option switch
    {
        StatusPurgeLayers.Two => 2L,
        StatusPurgeLayers.Three => 3L,
        StatusPurgeLayers.Half => layers / 2 + layers % 2,
        StatusPurgeLayers.All => layers,
        _ => 1L
    });
    internal static bool Matches(PowerModel power, StatusPurgePolarity polarity) => polarity switch
    {
        StatusPurgePolarity.Positive => power.TypeForCurrentAmount == PowerType.Buff,
        StatusPurgePolarity.Negative => power.TypeForCurrentAmount == PowerType.Debuff,
        StatusPurgePolarity.All => power.TypeForCurrentAmount is PowerType.Buff or PowerType.Debuff,
        _ => false
    };

    internal static async Task AfterRound(CombatState combat)
    {
        var stamp = Rounds.GetValue(combat, _ => new RoundStamp());
        foreach (var rule in SavedRules(Scp294ChaosRules.For(combat.RunState)))
        {
            if (!rule.Enabled || rule.Interval < 1 || combat.RoundNumber < 1 || combat.RoundNumber % rule.Interval != 0) continue;
            if (stamp.LastRounds.TryGetValue(rule.Target, out int last) && last >= combat.RoundNumber) continue;
            stamp.LastRounds[rule.Target] = combat.RoundNumber;
            await Apply(combat, rule);
        }
    }

    internal static async Task Apply(CombatState combat, StatusPurgeRule rule)
    {
        // Select the same stable snapshot on every peer, before removal hooks can add more powers.
        var selected = new List<PowerModel>();
        foreach (var creature in combat.Creatures.Where(c => c.IsAlive && Includes(rule.Target, c.Side)).OrderBy(c => c.CombatId))
        {
            var powers = creature.Powers.Where(p => Matches(p, rule.Polarity))
                .OrderBy(p => p.Id.Category, StringComparer.Ordinal).ThenBy(p => p.Id.Entry, StringComparer.Ordinal).ToArray();
            if (rule.Selection == StatusPurgeSelection.Single && powers.Length > 0)
            {
                string key = $"status-purge:{combat.RunState.CurrentActIndex}:{combat.RunState.CurrentMapCoord}:{combat.RoundNumber}:{creature.CombatId}:{rule.Target}:{rule.Polarity}";
                selected.Add(powers[new Rng((ulong)combat.RunState.Rng.Seed, key).NextInt(powers.Length)]);
            }
            else selected.AddRange(powers);
        }
        foreach (var power in selected)
        {
            if (CombatManager.Instance.IsEnding) break;
            if (!power.Owner.IsAlive || !power.Owner.Powers.Contains(power) || !Matches(power, rule.Polarity)) continue;
            long layers = power.StackType == PowerStackType.Counter ? Math.Max(1L, Math.Abs((long)power.Amount)) : 1L;
            long removed = RemovedLayers(layers, rule.Layers);
            if (removed >= layers) await PowerCmd.Remove(power);
            else
            {
                // This is removal, not an applied debuff: Artifact must not cancel or consume it.
                int remaining = (int)(layers - removed);
                power.SetAmount(power.Amount < 0 ? -remaining : remaining);
            }
        }
        if (!CombatManager.Instance.IsEnding && NCombatRoom.Instance is { } room)
            foreach (var creature in selected.Select(p => p.Owner).Distinct().Where(c => c.IsMonster && c.IsAlive))
                if (room.GetCreatureNode(creature) is { } node) await node.UpdateIntent(combat.Allies);
    }
}

// Run once after the native enemy-side end hooks, using the owning turn state rather than a global live combat.
[HarmonyPatch(typeof(CombatManager), "EndEnemyTurnInternal")]
internal static class Scp294StatusPurgeAtRoundEnd
{
    private static readonly Type TurnState = AccessTools.TypeByName("MegaCrit.Sts2.Core.Combat.CombatTurnState");
    private static readonly PropertyInfo State = AccessTools.Property(TurnState, "State");
    private static readonly PropertyInfo IsLive = AccessTools.Property(TurnState, "IsLive");
    private static void Postfix(object turnState, ref Task __result) => __result = Finish(__result, turnState);
    private static async Task Finish(Task original, object turnState)
    {
        await original;
        if ((bool)IsLive.GetValue(turnState)!) await Scp294StatusPurge.AfterRound((CombatState)State.GetValue(turnState)!);
    }
}
