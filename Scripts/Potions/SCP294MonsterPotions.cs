using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.ValueProps;
using OtherworldTreasures.Scripts.Alien;
using OtherworldTreasures.Scripts.Powers;

namespace OtherworldTreasures.Scripts.Potions;

internal readonly record struct MonsterBrewEffect(BrewEffect Effect, int Amount, bool TargetsPlayer);
internal sealed class MonsterBrewPlan
{
    internal required MonsterModel Monster;
    internal required MoveState Move;
    internal required AbstractIntent[] OriginalIntents;
    internal required MonsterBrewEffect[] Effects;
    internal Creature? PlayerTarget;
    internal bool Replaces;
    internal bool IsCombatStart;
    internal int Round;
    private SCP294Potion? _displayPotion;
    private MonsterBrewEffect[]? _displayEffects;
    internal SCP294Potion DisplayPotion
    {
        get
        {
            if (_displayPotion == null || !ReferenceEquals(_displayEffects, Effects))
            {
                _displayPotion = SCP294Potion.MonsterDisplay(this);
                _displayEffects = Effects;
            }
            return _displayPotion;
        }
    }
}

internal static class Scp294MonsterPotions
{
    internal static readonly BrewEffect[] EffectPool =
    [
        BrewEffect.Heal, BrewEffect.HealPercent, BrewEffect.Damage, BrewEffect.Regen,
        BrewEffect.Strength, BrewEffect.StrengthDown, BrewEffect.Dexterity, BrewEffect.DexterityDown,
        BrewEffect.Block, BrewEffect.Weak, BrewEffect.Vulnerable, BrewEffect.Frail, BrewEffect.Poison,
        BrewEffect.Absorption, BrewEffect.Buffer, BrewEffect.Artifact, BrewEffect.Intangible,
        BrewEffect.Thorns, BrewEffect.Plating, BrewEffect.Ritual, BrewEffect.Doom, BrewEffect.Shrink,
        BrewEffect.Demise, BrewEffect.MaxHp
    ];
    private static ConditionalWeakTable<MoveState, MonsterBrewPlan> _plans = new();
    private static ConditionalWeakTable<Creature, MonsterBrewPlan> _started = new();
    private static readonly FieldInfo OnPerform = AccessTools.Field(typeof(MoveState), "_onPerform");
    private static readonly FieldInfo Performed = AccessTools.Field(typeof(MoveState), "_performedAtLeastOnce");
    private static readonly MethodInfo SetIntents = AccessTools.PropertySetter(typeof(MoveState), nameof(MoveState.Intents));

    internal static void Reset() { _plans = new(); _started = new(); }
    internal static bool TryGet(MoveState move, out MonsterBrewPlan plan) => _plans.TryGetValue(move, out plan!);
    internal static Rng RecipeRng(MonsterModel monster, MoveState move, bool combatStart = false)
    {
        var combat = monster.CombatState;
        var run = combat.RunState;
        string key = $"scp294-monster:{run.CurrentActIndex}:{run.CurrentMapCoord}:{monster.Creature.CombatId}:{combat.RoundNumber}:{move.Id}";
        if (combatStart) key += ":combat-start";
        return new Rng((ulong)run.Rng.Seed, key);
    }

    internal static void Prepare(MonsterModel monster)
    {
        if (!monster.IsMutable || monster.Creature.Side != CombatSide.Enemy || monster.Creature.CombatState == null || GhostFear.Panicked(monster.Creature)) return;
        var rules = Scp294ChaosRules.For(monster.CombatState.RunState);
        if (!rules.PotionIntents) return;
        var move = monster.NextMove;
        if (move.Id == MonsterModel.stunnedMoveId || move.Intents.Any(i => i.IntentType is IntentType.Stun or IntentType.Sleep)) return;
        if (_plans.TryGetValue(move, out var old) && old.Round == monster.CombatState.RoundNumber) return;
        var originals = old?.OriginalIntents ?? move.Intents.ToArray();
        var rng = RecipeRng(monster, move);
        var targets = monster.CombatState.PlayerCreatures.Where(c => c.IsAlive).OrderBy(c => c.Player!.NetId).ToArray();
        var target = targets.Length > 0 ? targets[rng.NextInt(targets.Length)] : null;
        bool replaces = rng.NextBool();
        var plan = new MonsterBrewPlan
        {
            Monster = monster, Move = move, OriginalIntents = originals, PlayerTarget = target,
            Replaces = replaces, Round = monster.CombatState.RoundNumber,
            Effects = Brew(rng, allowPlayerDamage: true, rng.NextInt(0, 7))
        };
        _plans.Remove(move);
        _plans.Add(move, plan);
        if (plan.Replaces) SetIntents.Invoke(move, [new AbstractIntent[] { new Scp294MonsterPotionIntent(plan) }]);
        else SetIntents.Invoke(move, [originals.Append(new Scp294MonsterPotionIntent(plan)).ToArray()]);
    }

    internal static MonsterBrewEffect[] Brew(Rng rng, bool allowPlayerDamage, int count)
    {
        var pool = EffectPool.ToList();
        var result = new List<MonsterBrewEffect>();
        for (int i = 0; i < count; i++)
        {
            if (pool.Count == 0) pool.AddRange(EffectPool.Where(e => e != BrewEffect.Doom || !result.Any(r => r.Effect == BrewEffect.Doom)));
            int index = rng.NextInt(pool.Count);
            var effect = pool[index];
            pool.RemoveAt(index);
            // Doom is a lethal threshold, so monster brews never scale it from the native 33-layer potion.
            int amount = effect == BrewEffect.Doom ? rng.NextInt(1, 11)
                : SCP294Potion.VanillaBaseline(effect) * rng.NextInt(1, 4);
            // Each effect chooses its recipient independently, regardless of whether it helps or harms.
            // Opening cocktails retain the explicit restriction against direct player damage.
            bool player = (allowPlayerDamage || effect != BrewEffect.Damage) && rng.NextBool();
            result.Add(new MonsterBrewEffect(effect, amount, player));
        }
        return result.ToArray();
    }

    internal static async Task CombatStart(MonsterModel monster)
    {
        await Scp294ChaosRules.WaitReady();
        if (monster.Creature.Side != CombatSide.Enemy || !monster.Creature.IsAlive || monster.Creature.CombatState == null || _started.TryGetValue(monster.Creature, out _)) return;
        var rules = Scp294ChaosRules.For(monster.CombatState.RunState);
        if (!rules.MonsterPotions) return;
        var rng = RecipeRng(monster, monster.NextMove, combatStart: true);
        var players = monster.CombatState.PlayerCreatures.Where(c => c.IsAlive).OrderBy(c => c.Player!.NetId).ToArray();
        var plan = new MonsterBrewPlan
        {
            Monster = monster, Move = monster.NextMove, OriginalIntents = monster.NextMove.Intents.ToArray(),
            PlayerTarget = players.Length > 0 ? players[rng.NextInt(players.Length)] : null,
            Effects = Brew(rng, allowPlayerDamage: false, rng.NextInt(0, 7)),
            Replaces = false, IsCombatStart = true, Round = monster.CombatState.RoundNumber
        };
        _started.Add(monster.Creature, plan);
        await Use(plan);
    }

    internal static async Task Perform(MoveState move, MonsterBrewPlan plan, IReadOnlyList<Creature> targets)
    {
        await Scp294ChaosRules.WaitReady();
        Performed.SetValue(move, true);
        if (!plan.Monster.Creature.IsAlive) return;
        await Use(plan);
        if (!plan.Replaces && plan.Monster.Creature.IsAlive)
            await ((Func<IReadOnlyList<Creature>, Task>)OnPerform.GetValue(move)!)(targets);
    }

    internal static async Task Use(MonsterBrewPlan plan)
    {
        var source = plan.Monster.Creature;
        var context = new BlockingPlayerChoiceContext();
        var animationTarget = plan.Effects.Any(e => e.TargetsPlayer && e.Amount > 0) && plan.PlayerTarget?.IsAlive == true
            ? plan.PlayerTarget : source;
        if (TestMode.IsOff && NCombatRoom.Instance is { } room)
        {
            var from = room.GetCreatureNode(source);
            var to = room.GetCreatureNode(animationTarget);
            if (from != null && to != null)
            {
                var image = plan.DisplayPotion.Image;
                var vfx = NItemThrowVfx.Create(from.VfxSpawnPosition, to.VfxSpawnPosition, image);
                if (vfx != null)
                {
                    vfx.ZIndex = 0;
                    vfx.ZAsRelative = true;
                    room.CombatVfxContainer.AddChildSafely(vfx);
                }
                await Cmd.Wait(0.5f);
            }
        }
        foreach (bool toPlayer in new[] { false, true })
        {
            if (!source.IsAlive) break;
            var effects = plan.Effects.Where(e => e.TargetsPlayer == toPlayer && e.Amount > 0 &&
                (!GhostFear.Panicked(source) || e.Effect == BrewEffect.Block)).ToArray();
            if (effects.Length == 0) continue;
            var target = toPlayer ? plan.PlayerTarget : source;
            if (target?.IsAlive != true)
                target = toPlayer ? plan.Monster.CombatState.PlayerCreatures.FirstOrDefault(c => c.IsAlive) : source;
            if (target == null) continue;
            foreach (var effect in effects)
            {
                if (!source.IsAlive || !target.IsAlive) break;
                await Apply(context, source, target, effect);
            }
        }
    }

    internal static async Task Apply(PlayerChoiceContext context, Creature source, Creature target, MonsterBrewEffect effect)
    {
        int n = effect.Amount;
        if (n <= 0) return;
        switch (effect.Effect)
        {
            case BrewEffect.Heal: await CreatureCmd.Heal(target, n); break;
            case BrewEffect.HealPercent: await CreatureCmd.Heal(target, target.MaxHp * n / 100m); break;
            case BrewEffect.Damage: await CreatureCmd.Damage(context, target, n, ValueProp.Unpowered, source, null, null); break;
            case BrewEffect.Block: await CreatureCmd.GainBlock(target, n, ValueProp.Unpowered, null); break;
            case BrewEffect.MaxHp: await CreatureCmd.GainMaxHp(target, n); break;
            case BrewEffect.Regen: await PowerCmd.Apply<RegenPower>(context, target, n, source, null); break;
            case BrewEffect.Strength: await PowerCmd.Apply<StrengthPower>(context, target, n, source, null); break;
            case BrewEffect.StrengthDown: await PowerCmd.Apply<StrengthPower>(context, target, -n, source, null); break;
            case BrewEffect.Dexterity: await PowerCmd.Apply<DexterityPower>(context, target, n, source, null); break;
            case BrewEffect.DexterityDown: await PowerCmd.Apply<DexterityPower>(context, target, -n, source, null); break;
            case BrewEffect.Weak: await PowerCmd.Apply<WeakPower>(context, target, n, source, null); break;
            case BrewEffect.Vulnerable: await PowerCmd.Apply<VulnerablePower>(context, target, n, source, null); break;
            case BrewEffect.Frail: await PowerCmd.Apply<FrailPower>(context, target, n, source, null); break;
            case BrewEffect.Poison: await PowerCmd.Apply<PoisonPower>(context, target, n, source, null); break;
            case BrewEffect.Absorption: await PowerCmd.Apply<TemporaryHealthPower>(context, target, n, source, null); break;
            case BrewEffect.Buffer: await PowerCmd.Apply<BufferPower>(context, target, n, source, null); break;
            case BrewEffect.Artifact: await PowerCmd.Apply<ArtifactPower>(context, target, n, source, null); break;
            case BrewEffect.Intangible: await PowerCmd.Apply<IntangiblePower>(context, target, n, source, null); break;
            case BrewEffect.Thorns: await PowerCmd.Apply<ThornsPower>(context, target, n, source, null); break;
            case BrewEffect.Plating: await PowerCmd.Apply<PlatingPower>(context, target, n, source, null); break;
            case BrewEffect.Ritual: await PowerCmd.Apply<RitualPower>(context, target, n, source, null); break;
            case BrewEffect.Doom: await PowerCmd.Apply<DoomPower>(context, target, n, source, null); break;
            case BrewEffect.Shrink: await PowerCmd.Apply<ShrinkPower>(context, target, n, source, null); break;
            case BrewEffect.Demise: await PowerCmd.Apply<DemisePower>(context, target, n, source, null); break;
        }
    }


}

[HarmonyPatch(typeof(MonsterModel), nameof(MonsterModel.RollMove))]
internal static class Scp294MonsterPotionRoll
{
    private static void Postfix(MonsterModel __instance) => Scp294MonsterPotions.Prepare(__instance);
}
[HarmonyPatch(typeof(MonsterModel), nameof(MonsterModel.SetMoveImmediate))]
internal static class Scp294MonsterPotionImmediate
{
    private static void Postfix(MonsterModel __instance) => Scp294MonsterPotions.Prepare(__instance);
}
[HarmonyPatch(typeof(MoveState), nameof(MoveState.PerformMove))]
internal static class Scp294MonsterPotionPerform
{
    private static bool Prefix(MoveState __instance, IEnumerable<Creature> targets, ref Task __result)
    {
        if (!Scp294MonsterPotions.TryGet(__instance, out var plan) || plan.Monster.Creature.Side != CombatSide.Enemy) return true;
        __result = Scp294MonsterPotions.Perform(__instance, plan, targets.ToArray());
        return false;
    }
}


// The base hook is only extended for native enemy models; no global Hook dispatcher is changed.
[HarmonyPatch(typeof(AbstractModel), nameof(AbstractModel.BeforeCombatStart))]
internal static class Scp294MonsterCombatStart
{
    private static void Postfix(AbstractModel __instance, ref Task __result)
    {
        if (__instance is MonsterModel monster && monster.IsMutable && monster.Creature.Side == CombatSide.Enemy)
            __result = After(__result, monster);
    }
    private static async Task After(Task original, MonsterModel monster)
    {
        await original;
        await Scp294MonsterPotions.CombatStart(monster);
    }
}
