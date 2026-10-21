using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
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
    private static ConditionalWeakTable<Creature, object> _started = new();
    private static readonly FieldInfo OnPerform = AccessTools.Field(typeof(MoveState), "_onPerform");
    private static readonly FieldInfo Performed = AccessTools.Field(typeof(MoveState), "_performedAtLeastOnce");
    private static readonly MethodInfo SetIntents = AccessTools.PropertySetter(typeof(MoveState), nameof(MoveState.Intents));

    internal static void Reset() { _plans = new(); _started = new(); }
    internal static bool TryGet(MoveState move, out MonsterBrewPlan plan) => _plans.TryGetValue(move, out plan!);
    internal static int StatusStrength(int actIndex, RoomType room) => Math.Clamp(actIndex, 0, 2) switch
    {
        0 => room == RoomType.Boss ? 6 : room == RoomType.Elite ? 4 : 2,
        1 => room == RoomType.Boss ? 16 : room == RoomType.Elite ? 8 : 4,
        _ => room == RoomType.Boss ? 32 : room == RoomType.Elite ? 16 : 8
    };

    internal static Rng RecipeRng(MonsterModel monster, MoveState move)
    {
        var combat = monster.CombatState;
        var run = combat.RunState;
        string key = $"scp294-monster:{run.CurrentActIndex}:{run.CurrentMapCoord}:{monster.Creature.CombatId}:{combat.RoundNumber}:{move.Id}";
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
        int damage = OriginalDamage(monster, originals, target);
        int block = originals.Any(i => i.IntentType == IntentType.Defend)
            ? Math.Max(0, (int)Hook.ModifyBlock(monster.CombatState, monster.Creature, OriginalBlock(monster, move), ValueProp.Move, null, null, out _)) : 0;
        var room = monster.CombatState.Encounter?.RoomType ?? monster.CombatState.RunState.CurrentRoom?.RoomType ?? RoomType.Monster;
        var plan = new MonsterBrewPlan
        {
            Monster = monster, Move = move, OriginalIntents = originals, PlayerTarget = target,
            Replaces = true, Round = monster.CombatState.RoundNumber,
            Effects = Brew(rng, true, originals.Length * 2,
                damage, block, StatusStrength(monster.CombatState.RunState.CurrentActIndex, room))
        };
        _plans.Remove(move);
        _plans.Add(move, plan);
        if (plan.Replaces) SetIntents.Invoke(move, [new AbstractIntent[] { new Scp294MonsterPotionIntent(plan) }]);
        else if (plan.Effects.Length > 0) SetIntents.Invoke(move, [originals.Append(new Scp294MonsterPotionIntent(plan)).ToArray()]);
        else SetIntents.Invoke(move, [originals]);
    }

    internal static int OriginalDamage(MonsterModel monster, IEnumerable<AbstractIntent> intents, Creature? target)
    {
        // Native AttackIntent previews use LocalContext, which can differ between peers.
        // Use the recipe's shared chosen target to calculate the same cap on every peer.
        int total = 0;
        foreach (var attack in intents.OfType<AttackIntent>())
        {
            decimal single = Hook.ModifyDamage(monster.CombatState.RunState, monster.CombatState, target,
                monster.Creature, attack.DamageCalc?.Invoke() ?? 0m, ValueProp.Move, null, null,
                ModifyDamageHookType.All, CardPreviewMode.None, out _);
            total += Math.Max(0, (int)single) * Math.Max(1, attack.Repeats);
        }
        return total;
    }

    internal static MonsterBrewEffect[] Brew(Rng rng, bool replaces, int count, int damage, int block, int status)
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
            int amount = effect == BrewEffect.Doom ? rng.NextInt(1, 11) : replaces ? effect switch
            {
                BrewEffect.Damage => rng.NextInt(0, Math.Max(0, damage) + 1),
                BrewEffect.Block => rng.NextInt(0, Math.Max(0, block) + 1),
                _ => status
            } : SCP294Potion.VanillaBaseline(effect) * rng.NextInt(1, 4);
            bool player = IsHarmful(effect) && (replaces || effect != BrewEffect.Damage);
            result.Add(new MonsterBrewEffect(effect, amount, player));
        }
        return result.ToArray();
    }

    private static bool IsHarmful(BrewEffect effect) => effect is BrewEffect.Damage or BrewEffect.StrengthDown or
        BrewEffect.DexterityDown or BrewEffect.Weak or BrewEffect.Vulnerable or BrewEffect.Frail or
        BrewEffect.Poison or BrewEffect.Doom or BrewEffect.Shrink or BrewEffect.Demise;

    internal static async Task CombatStart(MonsterModel monster)
    {
        await Scp294ChaosRules.WaitReady();
        if (monster.Creature.Side != CombatSide.Enemy || monster.Creature.CombatState == null || _started.TryGetValue(monster.Creature, out _)) return;
        var rules = Scp294ChaosRules.For(monster.CombatState.RunState);
        if (!rules.MonsterPotions || rules.PotionIntents) return;
        _started.Add(monster.Creature, new object());
        var rng = RecipeRng(monster, monster.NextMove);
        var players = monster.CombatState.PlayerCreatures.Where(c => c.IsAlive).OrderBy(c => c.Player!.NetId).ToArray();
        var plan = new MonsterBrewPlan
        {
            Monster = monster, Move = monster.NextMove, OriginalIntents = monster.NextMove.Intents.ToArray(),
            PlayerTarget = players.Length > 0 ? players[rng.NextInt(players.Length)] : null,
            Effects = Brew(rng, false, rng.NextInt(0, 7), 0, 0, 0),
            Replaces = false, Round = monster.CombatState.RoundNumber
        };
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

    // DefendIntent has no numeric value. Read the block expression used by each native move,
    // including its live ascension-dependent property, rather than inventing a block cap.
    private static readonly Dictionary<(string Monster, string Move), string> BlockExpressions = new()
    {
        [("Aeonglass", "EbbMove")] = "EbbBlock", [("Axebot", "BootUpMove")] = "BootUpBlock",
        [("AxeRubyRaider", "SwingMove")] = "SwingBlock", [("BowlbugEgg", "BiteMove")] = "ProtectBlock",
        [("CrossbowRubyRaider", "ReloadMove")] = "3", [("Crusher", "GuardedStrikeMove")] = "18",
        [("Guardbot", "GuardMove")] = "15", [("InfestedPrism", "RadiateMove")] = "RadiateBlock",
        [("InfestedPrism", "PulsateMove")] = "PulsateBlock", [("LagavulinMatriarch", "Slash2Move")] = "Slash2Block",
        [("LouseProgenitor", "CurlAndGrowMove")] = "CurlBlock", [("MagiKnight", "PowerShieldMove")] = "PowerShieldBlock",
        [("MagiKnight", "PrepMove")] = "PowerShieldBlock", [("MechaKnight", "WindupMove")] = "15",
        [("Nibbit", "SliceMove")] = "SliceBlock", [("PunchConstruct", "ReadyMove")] = "10",
        [("Queen", "BurnBrightForMeMove")] = "20", [("Seapunk", "BubbleBurpMove")] = "BubbleBlock",
        [("SlitheringStrangler", "ThwackMove")] = "5", [("TheForgotten", "MiasmaMove")] = "8",
        [("TheObscura", "HardeningStrikeMove")] = "HardeningStrikeBlock", [("Tunneler", "BurrowMove")] = "BlockGain"
    };
    internal static int OriginalBlock(MonsterModel monster, MoveState move)
    {
        var action = (Delegate)OnPerform.GetValue(move)!;
        if (BlockExpressions.TryGetValue((monster.GetType().Name, action.Method.Name), out var expression))
            return int.TryParse(expression, out var constant) ? constant : Convert.ToInt32(AccessTools.Property(monster.GetType(), expression).GetValue(monster));
        // Compatibility for modded monsters exposing a single conventional block property.
        var properties = monster.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(p => p.Name.Contains("Block", StringComparison.OrdinalIgnoreCase) && p.PropertyType == typeof(int) && p.GetIndexParameters().Length == 0).ToArray();
        return properties.Length == 1 ? Math.Max(0, (int)properties[0].GetValue(monster)!) : 0;
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
