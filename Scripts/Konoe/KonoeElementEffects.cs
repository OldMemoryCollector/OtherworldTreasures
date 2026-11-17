using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Combat.AttackHits;

namespace OtherworldTreasures.Scripts.Konoe;

public static class KonoeAttributeRules
{
    public static decimal WindDamage(decimal basis, int index) => Math.Max(0, basis * (4 - index) / 4m);
    // Water: 1 wet per 2 damage (blocked damage counts). Fire: 1 burn per 3 actual HP lost.
    public static int StatusLayers(int summedDamage, KonoeDamageType type)
        => Math.Max(0, summedDamage) / (type == KonoeDamageType.Fire ? 3 : 2);
    public static int WetFreezeBonus(int wet) => Math.Max(0, wet) / 8;
    public static int BurnRemaining(int stacks) => Math.Max(0, stacks) / 2;
    public static int Reflected(int total, int threshold) => total <= threshold ? total : total / 2;
}

// Ephemeral resolution bookkeeping is weak-keyed by the actual command/play.
// Persisted combat data lives on powers/capabilities, never on the hook singleton.
internal static class KonoeElementEffects
{
    private sealed class Totals { internal readonly Dictionary<(Creature, KonoeDamageType), int> Damage = []; }
    private sealed class PlayState { internal readonly HashSet<PowerModel> Powers = []; internal readonly HashSet<Creature> Shattered = []; }
    private sealed class Defense
    {
        internal KonoeAttackGuardPower? Guard;
        internal KonoeCounterPower? Mirror;
        internal bool FullReflection;
        internal int RawCommitted, Reflected;
    }
    private sealed class CommandState { internal readonly Dictionary<Creature, Defense> Defenses = []; }
    private sealed class Hit(AttackHitContext context, int windIndex, Hit? previous)
    {
        internal AttackHitContext Context = context;
        internal int WindIndex = windIndex;
        internal Hit? Previous = previous;
        internal PowerModel[] Statuses = [];
        internal Defense? Defense;
        internal int Raw, Reflect, Guard;
    }
    private static readonly ConditionalWeakTable<object, Totals> DamageTotals = new();
    private static readonly ConditionalWeakTable<CardPlay, PlayState> Plays = new();
    private static readonly ConditionalWeakTable<AttackCommand, CommandState> Commands = new();
    private static readonly ConditionalWeakTable<Creature, Hit> Hits = new();
    private static bool _planningDefense;
    private static int _hitLogs, _damageLogs;

    // Temporary diagnostics: confirm whether the per-hit hook chain actually runs in game.
    internal static void LogHit(AttackHitContext c, KonoeDamageType types)
    {
        if (_hitLogs++ >= 80) return;
        Entry.Logger.Info($"[KonoeHit] card={c.CardSource?.Id.Entry} types={types} props={(int)c.DamageProps} targets={c.Targets.Count} hit={c.HitIndex + 1}/{c.TotalHitCount}");
    }

    internal static Task BeforeHit(AttackHitContext c)
    {
        var types = KonoeDamage.ForAttack(c.Attack).DamageTypes;
        var command = Commands.GetValue(c.Attack, _ => new());
        for (int i = 0; i < c.Targets.Count; i++)
        {
            var target = c.Targets[i];
            Hits.TryGetValue(target, out var previous);
            var hit = new Hit(c, (types & KonoeDamageType.Wind) != 0 ? KonoeDamage.WindIndex(c.Attack, target) : 0, previous);
            if (c.CardSource?.Type == CardType.Attack && c.CardPlay != null)
                hit.Statuses = target.Powers.Where(p => p is KonoeFrozenPower or KonoeKnockdownPower or KonoeAirbornePower).ToArray();
            if (!command.Defenses.TryGetValue(target, out var defense))
            {
                defense = new() { Guard = target.GetPower<KonoeAttackGuardPower>(), Mirror = target.GetPower<KonoeCounterPower>() };
                if (defense.Mirror != null && c.Dealer?.Side != target.Side)
                {
                    _planningDefense = true;
                    try
                    {
                        decimal perHit = Hook.ModifyDamage(c.CombatState.RunState, c.CombatState, target, c.Dealer,
                            c.Damage, c.DamageProps, c.CardSource, c.CardPlay, ModifyDamageHookType.All, CardPreviewMode.None, out _);
                        defense.FullReflection = (int)perHit * c.TotalHitCount <= defense.Mirror.Amount;
                    }
                    finally { _planningDefense = false; }
                }
                command.Defenses[target] = defense;
            }
            if (c.Dealer?.Side != target.Side && (defense.Guard != null || defense.Mirror != null)) hit.Defense = defense;
            Hits.Remove(target); Hits.Add(target, hit);
        }
        return Task.CompletedTask;
    }
    private static Hit? Matching(Creature? target, Creature? dealer, CardModel? source, CardPlay? play = null, bool checkPlay = false)
        => target != null && Hits.TryGetValue(target, out var h) && h.Context.Dealer == dealer && h.Context.CardSource == source && (!checkPlay || h.Context.CardPlay == play) ? h : null;
    internal static decimal WindBase(decimal damage, Creature? target, Creature? dealer, CardModel? source, CardPlay? play)
    {
        var h = Matching(target, dealer, source, play, true);
        if (h != null) return KonoeAttributeRules.WindDamage(damage, h.WindIndex);
        if (source?.TargetType == TargetType.AllEnemies && target != null && dealer?.CombatState is { } combat)
            return KonoeAttributeRules.WindDamage(damage, Math.Max(0, combat.GetOpponentsOf(dealer).Where(e => e.IsAlive).ToList().IndexOf(target)));
        return damage;
    }
    internal static decimal Defend(decimal damage, Creature? target, Creature? dealer, CardModel? source, CardPlay? play, ValueProp props)
    {
        if (_planningDefense || !props.IsCardOrMonsterMove() || Matching(target, dealer, source, play, true) is not { Defense: { } d } h) return damage;
        int raw = Math.Max(0, (int)damage);
        int reflect = d.Mirror == null ? 0 : d.FullReflection ? raw : (d.RawCommitted + raw) / 2 - d.RawCommitted / 2;
        int guard = d.Guard != null && target!.Powers.Contains(d.Guard) ? Math.Min(d.Guard.Amount, raw - reflect) : 0;
        h.Raw = raw; h.Reflect = reflect; h.Guard = guard;
        return raw - reflect - guard;
    }
    internal static async Task DamageReceived(PlayerChoiceContext c, Creature target, DamageResult r, ValueProp props, Creature? dealer, CardModel? source)
    {
        var hit = Matching(target, dealer, source);
        if (hit?.Defense is { } defense && props.IsCardOrMonsterMove())
        {
            defense.RawCommitted += hit.Raw; defense.Reflected += hit.Reflect;
            if (hit.Guard > 0 && defense.Guard != null && target.Powers.Contains(defense.Guard))
                await PowerCmd.ModifyAmount(c, defense.Guard, -hit.Guard, dealer, source);
            hit.Raw = hit.Reflect = hit.Guard = 0;
        }
        var types = KonoeDamage.FromProps(props);
        // Fallback: when the per-hit tag never reached ValueProp, derive the element from the played attack card.
        if (types == KonoeDamageType.None && source is { Type: CardType.Attack }) types = KonoeDamage.ForCard(source);
        if (_damageLogs++ < 80)
            Entry.Logger.Info($"[KonoeDmg] target={target.CombatId} props={(int)props} types={types} unblocked={r.UnblockedDamage} blocked={r.BlockedDamage} over={r.OverkillDamage}");
        if (types == KonoeDamageType.None || !target.IsAlive) return;
        object key = (object?)hit?.Context.CardPlay ?? hit?.Context.Attack ?? (object)r;
        var totals = DamageTotals.GetValue(key, _ => new());
        foreach (var type in new[] { KonoeDamageType.Water, KonoeDamageType.Fire })
        {
            if ((types & type) == 0) continue;
            // Native UnblockedDamage is actual HP lost; OverkillDamage is separate.
            int value = type == KonoeDamageType.Water ? r.TotalDamage + r.OverkillDamage : r.UnblockedDamage;
            var k = (target, type); int before = totals.Damage.GetValueOrDefault(k), after = before + value;
            totals.Damage[k] = after;
            int stacks = KonoeAttributeRules.StatusLayers(after, type) - KonoeAttributeRules.StatusLayers(before, type);
            if (stacks <= 0) continue;
            if (_damageLogs <= 80) Entry.Logger.Info($"[KonoeStatus] {type} +{stacks} on {target.CombatId} (total {after})");
            if (type == KonoeDamageType.Water) await PowerCmd.Apply<KonoeWetPower>(c, target, stacks, dealer, source);
            else await PowerCmd.Apply<KonoeBurnPower>(c, target, stacks, dealer, source);
        }
    }
    internal static async Task AfterHit(AttackHitContext c)
    {
        foreach (var target in c.Targets)
        {
            if (!Hits.TryGetValue(target, out var h) || h.Context != c) continue;
            Hits.Remove(target); if (h.Previous != null) Hits.Add(target, h.Previous);
            if (c.CardPlay == null || c.CardSource?.Type != CardType.Attack || !c.Results.Any(r => r.Receiver == target)) continue;
            var state = Plays.GetValue(c.CardPlay, _ => new());
            foreach (var power in h.Statuses) state.Powers.Add(power);
            if (h.Statuses.Any(p => p is KonoeFrozenPower) && state.Shattered.Add(target) && target.IsAlive)
                await CreatureCmd.Damage(c.ChoiceContext, target, 4, ValueProp.Unpowered, c.Dealer!);
        }
    }
    internal static bool CardAttackHitting(Creature target) => Hits.TryGetValue(target, out var h) && h.Context.CardSource?.Type == CardType.Attack;
    internal static async Task CardFinished(PlayerChoiceContext c, CardPlay play)
    {
        if (Plays.TryGetValue(play, out var state))
        {
            Plays.Remove(play);
            foreach (var power in state.Powers)
            {
                if (power.Owner.Powers.Contains(power)) await PowerCmd.Decrement(power);
                if (power is KonoeAirbornePower && play.Player.Creature.IsAlive)
                    await KonoeLinkage.Draw(c, play.Player, card => card.Type == CardType.Attack);
            }
        }
        DamageTotals.Remove(play);
    }
    internal static async Task AttackFinished(PlayerChoiceContext c, AttackCommand attack)
    {
        if (!Commands.TryGetValue(attack, out var state)) return;
        Commands.Remove(attack); DamageTotals.Remove(attack);
        foreach (var (target, d) in state.Defenses)
        {
            if (!attack.Results.SelectMany(r => r).Any(r => r.Receiver == target)) continue;
            if (d.Guard != null && target.Powers.Contains(d.Guard)) await PowerCmd.Remove(d.Guard);
            if (d.Mirror != null && target.Powers.Contains(d.Mirror)) await PowerCmd.Remove(d.Mirror);
            if (d.Reflected > 0 && target.IsAlive && attack.Attacker is { IsAlive: true } attacker)
                await CreatureCmd.Damage(c, attacker, d.Reflected, ValueProp.Unpowered, target);
        }
    }
}

[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyDamage))]
internal static class KonoeSummonDefenseDamagePatch
{
    private static void Postfix(Creature? target, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay, ValueProp props, ref decimal __result)
        => __result = KonoeElementEffects.Defend(__result, target, dealer, cardSource, cardPlay, props);
}
