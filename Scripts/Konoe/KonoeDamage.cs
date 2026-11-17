using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Combat.AttackHits;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models;
using STS2RitsuLib.Models.Capabilities;

namespace OtherworldTreasures.Scripts.Konoe;

// Element identity is independent of both native attack/block flags and presentation.
// A multi-element hit is still one hit: these tags never multiply damage or hit count.
[Flags]
public enum KonoeDamageType { None = 0, Water = 1, Wind = 2, Fire = 4 }

public sealed record KonoeAttackProfile(KonoeDamageType DamageTypes, string? HitVfxPath = null, bool PreserveSpellVfx = false);

public static class KonoeDamage
{
    // This mod's three element bits in ValueProp. Retain Move/Unpowered/Unblockable.
    // Keeping the flags on the actual hit also makes normal damage modifiers able to
    // query the element after the casting card's queue has already been consumed.
    private const int ElementShift = 24;
    private const ValueProp ElementMask = (ValueProp)(7 << ElementShift);
    private sealed class AttackState(KonoeAttackProfile profile)
    {
        internal KonoeAttackProfile Profile = profile;
        internal bool PresentationAttached;
        internal IReadOnlyList<MegaCrit.Sts2.Core.Entities.Creatures.Creature>? WindTargets;
    }
    private static readonly ConditionalWeakTable<AttackCommand, AttackState> Attacks = new();

    public static KonoeDamageType FromElement(KonoeElement element) => element switch
    {
        KonoeElement.Water => KonoeDamageType.Water,
        KonoeElement.Wind => KonoeDamageType.Wind,
        KonoeElement.Fire => KonoeDamageType.Fire,
        _ => throw new ArgumentOutOfRangeException(nameof(element))
    };
    public static KonoeDamageType FromElements(IEnumerable<KonoeElement> elements)
        => elements.Aggregate(KonoeDamageType.None, (types, element) => types | FromElement(element));
    public static KonoeDamageType FromProps(ValueProp props) => (KonoeDamageType)(((int)props >> ElementShift) & 7);
    public static ValueProp WithTypes(ValueProp props, KonoeDamageType types)
        => (props & ~ElementMask) | (ValueProp)(((int)types & 7) << ElementShift);
    public static bool HasElement(ValueProp props, KonoeElement element) => (FromProps(props) & FromElement(element)) != 0;

    // Pure one/two/three-element lines and the explicitly fire-based rain carry an element.
    // Other mixed spells retain their identity; no arbitrary primary element is chosen.
    public static KonoeDamageType ForSpell(KonoeSpellKind spell) => spell switch
    {
        KonoeSpellKind.Lapis or KonoeSpellKind.Sapphire or KonoeSpellKind.Tanzanite => KonoeDamageType.Water,
        KonoeSpellKind.Emerald or KonoeSpellKind.Peridot or KonoeSpellKind.Malachite => KonoeDamageType.Wind,
        KonoeSpellKind.Coral or KonoeSpellKind.Garnet or KonoeSpellKind.Rubellite or KonoeSpellKind.Uvarovite => KonoeDamageType.Fire,
        _ => KonoeDamageType.None
    };
    public static KonoeDamageType AttachedTypes(CardModel? card)
        => card != null && card.TryGetCapability<KonoeElementInfusionCapability>(out var infusion)
            ? FromElements(infusion.Elements) : KonoeDamageType.None;
    public static KonoeDamageType ForCard(CardModel? card)
    {
        if (card?.Type != CardType.Attack) return KonoeDamageType.None;
        var types = AttachedTypes(card);
        if (card is IKonoeElementSource source) types |= FromElements(source.GrantedElements);
        if (card is KonoeBlackStone blackStone) types |= ForSpell(blackStone.CurrentSpell);
        if (card is KonoeAbyssalFormula abyss) types |= ForSpell(abyss.CurrentSpell);
        return types;
    }
    // Player-facing infusion is limited to untyped attacks. The capability Add API
    // and damage flags intentionally retain support for explicit multi-element content.
    public static bool CanReceiveInfusion(CardModel? card)
        => card?.Type == CardType.Attack && ForCard(card) == KonoeDamageType.None;
    public static bool TryInfuse(CardModel card, KonoeElement element, bool temporary = false)
    {
        if (!CanReceiveInfusion(card)) return false;
        var infusion = card.GetOrCreateCapability<KonoeElementInfusionCapability>();
        if (temporary) infusion.AddUntilPlayed(element);
        else infusion.Add(element);
        return true;
    }
    public static KonoeAttackProfile SpellProfile(KonoeSpellKind spell, KonoeDamageType attached = KonoeDamageType.None, string? hitVfxPath = null)
    {
        var types = ForSpell(spell) | attached;
        if (spell == KonoeSpellKind.BlackStone)
            return new(types, hitVfxPath ?? (types == KonoeDamageType.None ? KonoeAttackVfx.BlackStonePath : null));
        // Even pure-element magic must NOT fall back to the ordinary element hit.
        return new(types, hitVfxPath, PreserveSpellVfx: true);
    }
    public static void ConfigureSpell(AttackCommand attack, KonoeSpellKind spell, string? hitVfxPath = null)
        => SetProfile(attack, SpellProfile(spell, AttachedTypes(attack.ModelSource as CardModel), hitVfxPath));
    public static void Configure(AttackCommand attack, KonoeDamageType types, string? hitVfxPath = null)
        => SetProfile(attack, new(types | AttachedTypes(attack.ModelSource as CardModel), hitVfxPath));
    public static void SetProfile(AttackCommand attack, KonoeAttackProfile profile)
    {
        Attacks.Remove(attack);
        Attacks.Add(attack, new(profile));
    }
    private static AttackState State(AttackCommand attack)
        => Attacks.GetValue(attack, command => new(new(ForCard(command.ModelSource as CardModel))));
    public static KonoeAttackProfile ForAttack(AttackCommand attack) => State(attack).Profile;

    public static IReadOnlyList<string> HitVfxPaths(KonoeAttackProfile profile)
    {
        if (profile.HitVfxPath is { } explicitPath) return [explicitPath];
        if (profile.PreserveSpellVfx) return [];
        var paths = new List<string>(3);
        if ((profile.DamageTypes & KonoeDamageType.Water) != 0) paths.Add(KonoeAttackVfx.WaterPath);
        if ((profile.DamageTypes & KonoeDamageType.Wind) != 0) paths.Add(KonoeAttackVfx.WindPath);
        if ((profile.DamageTypes & KonoeDamageType.Fire) != 0) paths.Add(KonoeAttackVfx.FirePath);
        return paths;
    }
    // Multiple tags retain all damage/gain effects. The newest attachment leads the
    // body motion; the other distinct effects emit from that same hand/impact key.
    public static string? PrimaryVfxPath(KonoeAttackProfile profile, CardModel? card)
    {
        var paths = HitVfxPaths(profile);
        if (paths.Count == 0) return null;
        if (card != null && card.TryGetCapability<KonoeElementInfusionCapability>(out var infusion) && infusion.Elements.Count > 0)
        {
            string path = infusion.Elements[^1] switch
            {
                KonoeElement.Water => KonoeAttackVfx.WaterPath, KonoeElement.Wind => KonoeAttackVfx.WindPath, _ => KonoeAttackVfx.FirePath
            };
            if (paths.Contains(path)) return path;
        }
        return paths[0];
    }
    internal static IReadOnlyList<MegaCrit.Sts2.Core.Entities.Creatures.Creature> ExpandWindTargets(AttackCommand attack, IReadOnlyList<MegaCrit.Sts2.Core.Entities.Creatures.Creature> original)
    {
        var state = State(attack);
        if ((state.Profile.DamageTypes & KonoeDamageType.Wind) == 0 || attack.Attacker?.CombatState is not { } combat) return original;
        if (state.WindTargets == null)
        {
            var all = combat.GetOpponentsOf(attack.Attacker).Where(e => e.IsAlive).ToArray();
            int start = attack.IsSingleTargeted && original.Count > 0 ? Array.IndexOf(all, original[0]) : 0;
            state.WindTargets = all.Skip(Math.Max(0, start)).Take(4).ToArray();
        }
        return state.WindTargets.Where(e => e.IsAlive).ToArray();
    }
    internal static int WindIndex(AttackCommand attack, MegaCrit.Sts2.Core.Entities.Creatures.Creature target)
        => Math.Max(0, State(attack).WindTargets?.ToList().IndexOf(target) ?? 0);
    internal static void AttachPresentation(AttackCommand attack)
    {
        var state = State(attack);
        if (state.PresentationAttached) return;
        state.PresentationAttached = true;
        var paths = HitVfxPaths(state.Profile);
        if (paths.Count == 0 || attack.Attacker == null) return; // Leave each spell's existing/custom presentation intact.
        attack.WithHitFx(null, attack.HitSfx, attack.TmpHitSfx);
        string primary = PrimaryVfxPath(state.Profile, attack.ModelSource as CardModel)!;
        attack.WithAttackerAnim(KonoeAttackVfx.AnimName(primary), KonoeAttackVfx.EmissionDelay(primary));
        attack.WithAttackerFx(() => KonoeAttackVfx.CreateAction(primary, attack.Attacker, paths));
    }
}

// Use the framework's existing per-hit extension point, not a replacement damage pipeline.
[RegisterSingleton]
public sealed class KonoeElementAttackHooks : HookedSingletonModel, IAttackHitHookListener
{
    public KonoeElementAttackHooks() : base(HookType.Combat) { }
    public override Task BeforeAttack(AttackCommand command)
    {
        KonoeDamage.AttachPresentation(command);
        return Task.CompletedTask;
    }
    public async Task BeforeAttackHit(AttackHitContext context)
    {
        var types = KonoeDamage.ForAttack(context.Attack).DamageTypes;
        if (types != KonoeDamageType.None) context.DamageProps = KonoeDamage.WithTypes(context.DamageProps, types);
        KonoeElementEffects.LogHit(context, types);
        await KonoeElementEffects.BeforeHit(context);
    }
    public Task AfterAttackHit(AttackHitContext context) => KonoeElementEffects.AfterHit(context);
    public override Task AfterAttack(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context, AttackCommand attack) => KonoeElementEffects.AttackFinished(context, attack);
    public override Task AfterCardPlayedLate(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context, MegaCrit.Sts2.Core.Entities.Cards.CardPlay play) => KonoeElementEffects.CardFinished(context, play);
    public override Task AfterDamageReceived(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext context, MegaCrit.Sts2.Core.Entities.Creatures.Creature target, MegaCrit.Sts2.Core.Entities.Creatures.DamageResult result, ValueProp props, MegaCrit.Sts2.Core.Entities.Creatures.Creature? dealer, CardModel? source)
        => KonoeElementEffects.DamageReceived(context, target, result, props, dealer, source);
}

[HarmonyLib.HarmonyPatch(typeof(AttackCommand), "GetPossibleTargets")]
internal static class KonoeWindTargetExpansionPatch
{
    private static void Postfix(AttackCommand __instance, ref IReadOnlyList<MegaCrit.Sts2.Core.Entities.Creatures.Creature> __result)
        => __result = KonoeDamage.ExpandWindTargets(__instance, __result);
}
