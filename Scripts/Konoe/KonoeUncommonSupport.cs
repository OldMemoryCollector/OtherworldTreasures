using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models.Capabilities;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Konoe;

internal static class KonoeBlueSupport
{
    internal static KonoeElement Random(Player p) => (KonoeElement)(p.RunState.Rng.CombatCardGeneration.NextInt(3) + 1);
    internal static int Pack(IEnumerable<KonoeElement> elements) => elements.Aggregate(0, KonoeElementRules.Append);
    internal static string ElementText(int packed) => string.Join(" · ", KonoeElementRules.Decode(packed).Select(e => new LocString("cards", "KONOE_ELEMENT_" + e.ToString().ToUpperInvariant()).GetFormattedText()));
    internal static async Task<KonoeElement?> ChooseElement(PlayerChoiceContext c, Player p, IEnumerable<KonoeElement>? allowed = null)
    {
        if (p.Creature.CombatState is not { } combat) return null;
        var available = (allowed ?? new[] { KonoeElement.Water, KonoeElement.Wind, KonoeElement.Fire }).Distinct().ToHashSet();
        var choices = new CardModel[] { ModelDb.Card<KonoeWaterChoice>(), ModelDb.Card<KonoeWindChoice>(), ModelDb.Card<KonoeFireChoice>() }
            .Where(x => available.Contains(((KonoeElementChoice)x).Element)).Select(x => combat.CreateCard(x, p)).ToArray();
        if (choices.Length == 0) return null;
        return (await CardSelectCmd.FromChooseACardScreen(c, choices, p) as KonoeElementChoice)?.Element;
    }
    internal static async Task<bool> Confirm(PlayerChoiceContext c, Player p, int heat)
    {
        if (KonoeHeat.Get(p) < heat || p.Creature.CombatState is not { } combat) return false;
        var yes = combat.CreateCard(ModelDb.Card<KonoeAcceptHeatChoice>(), p); yes.DynamicVars["Heat"].BaseValue = heat;
        var no = combat.CreateCard(ModelDb.Card<KonoeDeclineHeatChoice>(), p);
        return await CardSelectCmd.FromChooseACardScreen(c, new[] { yes, no }, p) is KonoeAcceptHeatChoice;
    }
    internal static async Task Infuse(PlayerChoiceContext c, Player p, CardModel card)
    {
        if (!KonoeDamage.CanReceiveInfusion(card)) return;
        if (await ChooseElement(c, p) is { } element) KonoeDamage.TryInfuse(card, element);
        KonoeElements.Refresh(p);
    }
    internal static async Task Store(PlayerChoiceContext c, Player p)
    {
        var state = await KonoeElements.EnsureState(c, p);
        if (state == null) return;
        var slot = p.Creature.GetPower<KonoeStoragePower>() ?? await PowerCmd.Apply<KonoeStoragePower>(c, p.Creature, 1, p.Creature, null);
        if (slot == null) return;
        slot.PackedElements = state.PackedElements;
        state.PackedElements = 0;
        KonoeElements.Refresh(p);
    }
    internal static async Task Swap(PlayerChoiceContext c, Player p)
    {
        var state = await KonoeElements.EnsureState(c, p);
        if (state == null) return;
        var slot = p.Creature.GetPower<KonoeStoragePower>() ?? await PowerCmd.Apply<KonoeStoragePower>(c, p.Creature, 1, p.Creature, null);
        if (slot == null) return;
        (state.PackedElements, slot.PackedElements) = (slot.PackedElements, state.PackedElements);
        KonoeElements.Refresh(p);
    }
    internal static IReadOnlyList<KonoeElement> SpellElements(KonoeSpellKind spell)
    {
        for (int packed = 1; packed < 64; packed++)
        {
            var elements = KonoeElementRules.Decode(packed);
            if (Pack(elements) == packed && KonoeElementRules.Resolve(packed) == spell) return elements;
        }
        return [];
    }
    internal static async Task AfterSpell(PlayerChoiceContext c, CardModel source, CardPlay play, int degree, IReadOnlyList<KonoeElement> consumed, bool retainSpellDraws = false)
    {
        var p = source.Owner;
        if (!p.Creature.IsAlive) return;
        if (p.Character is Characters.Konoe) await KonoeHeat.GainFromPlay(play, KonoeHeat.SpellIncome);
        if (await KonoeElements.EnsureState(c, p) is { } state) state.SpellsThisTurn++;
        var trace = p.Creature.GetPower<KonoeSpellTracePower>();
        int keep = trace?.Amount ?? 0;
        if (trace != null && consumed.Count > 0) await PowerCmd.Remove(trace);
        if (p.Creature.GetPower<KonoeSpellPermanencePower>() is { Used:false } permanence)
        {
            permanence.Used = true; keep += permanence.Amount;
        }
        if (consumed.Count > 0 && keep > 0)
        {
            var remaining = consumed.ToList();
            for (int i = 0; i < Math.Min(keep, consumed.Count); i++)
                if (await ChooseElement(c, p, remaining) is { } retained) { remaining.Remove(retained); await KonoeElements.Gain(c, p, retained); }
        }
        if (p.Creature.GetPower<KonoeOverdrivePower>() is { SpellUsed:false } overdrive)
        {
            overdrive.SpellUsed = true;
            await KonoeElements.Gain(c, p, Random(p));
        }
        foreach (var ascension in p.Creature.Powers.OfType<KonoeSpellSublimationPower>().ToArray()) await ascension.Trigger(c, degree, retainSpellDraws);
        if (p.Creature.GetPower<KonoeSpellEchoPower>() is { Used: false } echo)
        {
            echo.Used = true;
            for (int i = 0; i < echo.Amount; i++) await KonoeElements.Gain(c, p, Random(p));
        }
        if (p.Creature.GetPower<KonoeResonantSpellPower>() is { SpellUsed: false } resonance)
        {
            resonance.SpellUsed = true;
            foreach (var ally in p.Creature.CombatState?.Players.Where(x => x != p && x.Creature.IsAlive).ToArray() ?? [])
                await CreatureCmd.GainBlock(ally.Creature, resonance.Amount, ValueProp.Unpowered, null);
        }
    }
}

public abstract class KonoeBlueChoice : ModCardTemplate
{
    protected KonoeBlueChoice() : base(0, CardType.Skill, CardRarity.Token, TargetType.Self, false) { }
    public override bool CanBeGeneratedInCombat => false;
    public override bool CanBeGeneratedByModifiers => false;
    public override CardAssetProfile AssetProfile => KonoeCardArt.Profile;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Heat", 0), new DamageVar(0, ValueProp.Move)];
    protected override Task OnPlay(PlayerChoiceContext c, CardPlay p) => Task.CompletedTask;
}
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeAcceptHeatChoice : KonoeBlueChoice { }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeDeclineHeatChoice : KonoeBlueChoice { }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeStoreChoice : KonoeBlueChoice { }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeSwapChoice : KonoeBlueChoice { }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeVolleySingleChoice : KonoeBlueChoice { }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeVolleyAllChoice : KonoeBlueChoice { }
[RegisterCard(typeof(KonoeCardPool))]
public sealed class KonoeImprovisedSpellChoice : KonoeBlueChoice
{
    public KonoeSpellKind Spell { get; private set; }
    internal void Configure(KonoeSpellKind spell)
    {
        Spell = spell; var v = KonoeSpellValues.For(spell, false);
        DynamicVars.Damage.Props = KonoeDamage.WithTypes(KonoeSpellValues.DamageProps(spell), KonoeDamage.ForSpell(spell));
        foreach (var (key, value) in new[] { ("Damage", v.Damage), ("Block", v.Block), ("Hits", v.Hits), ("Stacks", v.Stacks), ("Bonus", v.Bonus), ("Turns", v.Turns), ("Followup", v.Followup) }) DynamicVars[key].BaseValue = value;
    }
    public override string Title => new LocString("cards", KonoeBlackStone.SpellKey(Spell) + ".title").GetFormattedText();
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => KonoeSpellTips.For(Spell, DynamicVars["Stacks"].IntValue);
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(0, ValueProp.Move), new BlockVar(0, ValueProp.Move), new DynamicVar("Hits", 1), new DynamicVar("Stacks", 0), new DynamicVar("Bonus", 0), new DynamicVar("Turns", 0), new DynamicVar("Followup", 0)];
    protected override void AddExtraArgsToDescription(LocString description)
    {
        var text = new LocString("cards", KonoeBlackStone.SpellKey(Spell) + ".description");
        DynamicVars.AddTo(text);
        description.Add("SpellText", text);
    }
}

[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeDetonateChoice : KonoeBlueChoice { }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeDelayChoice : KonoeBlueChoice { }
