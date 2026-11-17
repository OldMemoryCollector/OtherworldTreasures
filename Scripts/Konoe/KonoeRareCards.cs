using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models.Capabilities;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Konoe;

internal sealed record KonoeRareSpec(int Cost, CardType Type, TargetType Target, CardRarity Rarity, int HeatCost, int CostUpgrade, bool Multiplayer, KonoeBlueVar[] Vars);
internal static class KonoeRareSpecs
{
    internal static readonly KonoeRareSpec[] All = [
        new(2, CardType.Attack, TargetType.AnyEnemy, CardRarity.Rare, 0, 0, false, [new("Damage", 8, 2), new("Hits", 2, 0), new("BonusHits", 1, 1)]),
        new(2, CardType.Attack, TargetType.AnyEnemy, CardRarity.Rare, 0, 0, false, [new("Damage", 16, 5)]),
        new(2, CardType.Attack, TargetType.AllEnemies, CardRarity.Rare, 50, 0, false, [new("Damage", 9, 2), new("Hits", 3, 0), new("BonusHits", 1, 1)]),
        new(2, CardType.Attack, TargetType.AllEnemies, CardRarity.Rare, 50, 0, false, [new("Damage", 24, 7)]),
        new(2, CardType.Attack, TargetType.AllEnemies, CardRarity.Rare, 50, 0, false, [new("Damage", 24, 6), new("BonusDamage", 4, 2)]),
        new(2, CardType.Attack, TargetType.AnyEnemy, CardRarity.Rare, 100, 0, false, [new("Damage", 30, 10), new("Threshold", 35, 5)]),
        new(2, CardType.Power, TargetType.Self, CardRarity.Rare, 0, -1, false, [new("Power", 1, 0)]),
        new(2, CardType.Skill, TargetType.Self, CardRarity.Rare, 0, 0, false, [new("Block", 14, 4), new("Damage", 5, 2)]),
        new(2, CardType.Skill, TargetType.AnyEnemy, CardRarity.Rare, 0, -1, false, []),
        new(2, CardType.Power, TargetType.Self, CardRarity.Rare, 0, -1, false, [new("Elements", 2, 0)]),
        new(2, CardType.Attack, TargetType.AnyEnemy, CardRarity.Rare, 0, 0, false, [new("Damage", 12, 4), new("SplashDamage", 4, 2)]),
        new(1, CardType.Skill, TargetType.AnyEnemy, CardRarity.Rare, 0, -1, false, [new("Damage", 9, 3)]),
        new(2, CardType.Skill, TargetType.AnyEnemy, CardRarity.Rare, 0, -1, false, []),
        new(1, CardType.Attack, TargetType.AnyEnemy, CardRarity.Rare, 0, 0, false, [new("Damage", 9, 3)]),
        new(1, CardType.Skill, TargetType.Self, CardRarity.Rare, 0, 0, false, [new("Block", 6, 3)]),
        new(1, CardType.Skill, TargetType.Self, CardRarity.Rare, 0, -1, false, []),
        new(1, CardType.Skill, TargetType.Self, CardRarity.Rare, 0, 0, false, [new("Block", 5, 3)]),
        new(1, CardType.Power, TargetType.Self, CardRarity.Rare, 0, 0, false, [new("Damage", 5, 2), new("Heat", 8, 4)]),
        new(2, CardType.Skill, TargetType.Self, CardRarity.Rare, 0, 0, false, [new("Block", 16, 4)]),
        new(3, CardType.Skill, TargetType.RandomEnemy, CardRarity.Rare, 0, 0, false, [new("HpLoss", 6, -2)]),
        new(2, CardType.Power, TargetType.Self, CardRarity.Rare, 0, 0, false, [new("Cards", 2, 1)]),
        new(1, CardType.Skill, TargetType.AnyAlly, CardRarity.Rare, 0, -1, true, []),
        new(2, CardType.Power, TargetType.Self, CardRarity.Rare, 0, 0, true, [new("HpLoss", 3, -1)]),
        new(3, CardType.Power, TargetType.Self, CardRarity.Rare, 0, -2, false, [new("Power", 1, 0)]),
        new(3, CardType.Attack, TargetType.AllEnemies, CardRarity.Rare, 0, 0, false, [new("Damage", 10, 3), new("Hits", 2, 0)]),
        new(0, CardType.Skill, TargetType.Self, CardRarity.Ancient, 0, 0, false, [new("Cards", 2, 1)]),
        new(2, CardType.Attack, TargetType.AnyEnemy, CardRarity.Ancient, 0, -1, false, [new("Repeat", 2, 0)])
    ];
}

public abstract class KonoeRareCard : ModCardTemplate, IKonoeElementSource
{
    private readonly int _index;
    private KonoeRareSpec Spec => KonoeRareSpecs.All[_index];
    protected KonoeRareCard(int index) : base(KonoeRareSpecs.All[index].Cost, KonoeRareSpecs.All[index].Type, KonoeRareSpecs.All[index].Rarity, KonoeRareSpecs.All[index].Target, DevelopmentContent.Nine)
    {
        _index = index;
        if (Spec.HeatCost > 0) this.SecondaryCosts().Set(KonoeHeat.ResourceId, Spec.HeatCost);
    }
    public virtual IReadOnlyList<KonoeElement> GrantedElements => [];
    public override bool CanBeGeneratedInCombat => true;
    public override bool CanBeGeneratedByModifiers => DevelopmentContent.Nine;
    public override CardAssetProfile AssetProfile => KonoeCardArt.Profile;
    public override bool GainsBlock => Spec.Vars.Any(v => v.Name == "Block");
    public override CardMultiplayerConstraint MultiplayerConstraint => Spec.Multiplayer ? CardMultiplayerConstraint.MultiplayerOnly : CardMultiplayerConstraint.None;
    protected override IEnumerable<DynamicVar> CanonicalVars => Spec.Vars.Select(v => v.Name switch
    {
        "Damage" => (DynamicVar)new DamageVar(v.Value, ValueProp.Move), "Block" => new BlockVar(v.Value, ValueProp.Move),
        "Cards" => new CardsVar(v.Value), "HpLoss" => new HpLossVar(v.Value), _ => new DynamicVar(v.Name, v.Value)
    });
    protected override void OnUpgrade()
    {
        if (Spec.CostUpgrade != 0) EnergyCost.UpgradeBy(Spec.CostUpgrade);
        foreach (var v in Spec.Vars) if (v.Upgrade != 0) DynamicVars[v.Name].UpgradeValueBy(v.Upgrade);
    }
    protected override IEnumerable<IHoverTip> AdditionalHoverTips
    {
        get
        {
            yield return new HoverTip(new LocString("static_hover_tips", "KONOE_ELEMENTS.title"), new LocString("static_hover_tips", "KONOE_ELEMENTS.description"));
            if (Spec.HeatCost > 0 || Spec.Vars.Any(v => v.Name == "Heat") || _index is 13 or 17) yield return new HoverTip(new LocString("static_hover_tips", "OTHERWORLD_TREASURES_SECONDARY_RESOURCE_KONOE_HEAT.title"), new LocString("static_hover_tips", "OTHERWORLD_TREASURES_SECONDARY_RESOURCE_KONOE_HEAT.description"));
            if (_index is 0 or 1) yield return HoverTipFactory.FromPower<KonoeAirbornePower>();
            if (_index == 2) yield return HoverTipFactory.FromPower<KonoeOverdrivePower>(3);
            if (_index is 3 or 4 or 12) yield return new HoverTip(new LocString("static_hover_tips", "KONOE_STORAGE.title"), new LocString("static_hover_tips", "KONOE_STORAGE.description"));
            if (_index == 14) yield return HoverTipFactory.FromPower<OneTwoPunchPower>();
            if (_index == 15) yield return new HoverTip(new LocString("static_hover_tips", "KONOE_ELEMENT_INFUSION.title"), new LocString("static_hover_tips", "KONOE_ELEMENT_INFUSION.description"));
            if (_index is 11 or 18) yield return HoverTipFactory.FromPower<KonoeAttackInterruptedPower>();
            if (_index == 24) yield return HoverTipFactory.FromPower<KonoeKnockdownPower>();
            if (_index == 23) yield return HoverTipFactory.FromPower<KonoeCrimsonWitchFormPower>();
        }
    }
    private int N(string key) => DynamicVars[key].IntValue;
    private Creature[] Enemies => CombatState?.HittableEnemies.ToArray() ?? [];
    private Task Block(CardPlay p) => CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, p);
    private async Task Hit(PlayerChoiceContext c, CardPlay p, int? damage = null, int hits = 1, bool? all = null)
    {
        bool area = all ?? TargetType == TargetType.AllEnemies;
        if (area ? Enemies.Length == 0 : p.Target is not { IsAlive:true }) return;
        var attack = DamageCmd.Attack(damage ?? N("Damage")).FromCard(this, p).WithHitCount(hits).WithHitFx("vfx/vfx_attack_blunt");
        if (area) attack.TargetingAllOpponents(CombatState!); else attack.Targeting(p.Target!);
        await attack.Execute(c);
    }
    private Task<T?> Apply<T>(PlayerChoiceContext c, int amount) where T : PowerModel => PowerCmd.Apply<T>(c, Owner.Creature, amount, Owner.Creature, this);
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        if (CombatState is not { } combat) return;
        if (Type != CardType.Attack && _index is not (8 or 12 or 19)) await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);
        switch (_index)
        {
            case 0:
                await Hit(c, p, hits: N("Hits") + (p.Target?.HasPower<KonoeAirbornePower>() == true ? N("BonusHits") : 0)); break;
            case 1:
                bool attacking = p.Target?.Monster?.IntendsToAttack == true;
                await Hit(c, p);
                if (attacking && p.Target is { IsAlive:true } target)
                {
                    await PowerCmd.Apply<KonoeAttackInterruptedPower>(c, target, 1, Owner.Creature, this);
                    await PowerCmd.Apply<KonoeAirbornePower>(c, target, 1, Owner.Creature, this);
                }
                break;
            case 2:
                await Hit(c, p, hits: N("Hits") + (KonoeRareSupport.Overdrive(Owner) ? N("BonusHits") : 0)); break;
            case 3:
                await Hit(c, p);
                var state = await KonoeElements.EnsureState(c, Owner);
                var slot = Owner.Creature.GetPower<KonoeStoragePower>() ?? await Apply<KonoeStoragePower>(c, 1);
                if (state != null) state.PackedElements = KonoeRareSupport.RandomThird(Owner);
                if (slot != null) slot.PackedElements = KonoeRareSupport.RandomThird(Owner);
                KonoeElements.Refresh(Owner); break;
            case 4:
                await Hit(c, p);
                var stored = Owner.Creature.GetPower<KonoeStoragePower>();
                int consumed = KonoeElementRules.Decode(KonoeElements.Get(Owner)).Count + KonoeElementRules.Decode(stored?.PackedElements ?? 0).Count;
                KonoeElements.Consume(Owner); if (stored != null) stored.PackedElements = 0;
                if (consumed > 0) await Hit(c, p, N("BonusDamage"), hits: consumed);
                break;
            case 5:
                if (p.Target is { IsAlive:true } execute && execute.CurrentHp * 100m <= execute.MaxHp * N("Threshold")) await CreatureCmd.Kill(execute);
                else await Hit(c, p);
                break;
            case 6: await Apply<KonoeAutomaticChantPower>(c, N("Power")); break;
            case 7:
                await Block(p);
                var hand = PileType.Hand.GetPile(Owner).Cards.ToArray();
                await CardCmd.Discard(c, hand);
                if (hand.Length > 0) await Hit(c, p, hits: hand.Length, all: true);
                break;
            case 8:
                while (Owner.Creature.IsAlive && KonoeElementRules.Decode(KonoeElements.Get(Owner)).Count < 3)
                {
                    if (await KonoeBlueSupport.ChooseElement(c, Owner) is not { } added) break;
                    await KonoeElements.Gain(c, Owner, added, this);
                }
                await KonoeRareSupport.Current(c, this, p); break;
            case 9: await Apply<KonoeSpellPermanencePower>(c, N("Elements")); break;
            case 10:
                await Hit(c, p);
                if (Enemies.Length > 0) await Hit(c, p, N("SplashDamage"), all: true);
                break;
            case 11:
                await Hit(c, p);
                if (p.Target is { IsAlive:true } bound)
                {
                    await PowerCmd.Apply<KonoeAttackInterruptedPower>(c, bound, 1, Owner.Creature, this);
                    await PowerCmd.Apply<KonoeKnockdownPower>(c, bound, 1, Owner.Creature, this);
                }
                break;
            case 12:
                int second = Owner.Creature.GetPower<KonoeStoragePower>()?.PackedElements ?? 0;
                await KonoeRareSupport.Current(c, this, p);
                await KonoeRareSupport.Stored(c, this, p, second); break;
            case 13:
                await Hit(c, p);
                int heat = KonoeHeat.Get(Owner);
                if (heat > 0 && await KonoeHeat.Spend(p, heat) && heat / 10 > 0) await Hit(c, p, hits: heat / 10);
                break;
            case 14:
                await Block(p); await Apply<OneTwoPunchPower>(c, 1); break;
            case 15:
                var infusable = PileType.Hand.GetPile(Owner).Cards.Where(KonoeDamage.CanReceiveInfusion).ToArray();
                if (infusable.Length > 0 && await KonoeBlueSupport.ChooseElement(c, Owner) is { } infused)
                {
                    foreach (var card in infusable) KonoeDamage.TryInfuse(card, infused);
                    KonoeElements.Refresh(Owner);
                }
                break;
            case 16:
                await Block(p);
                var returned = (await CardSelectCmd.FromCombatPile(c, PileType.Discard.GetPile(Owner), Owner, new CardSelectorPrefs(SelectionScreenPrompt, 1), x => x.Type == CardType.Skill && !x.EnergyCost.CostsX && x.EnergyCost.Canonical >= 0)).FirstOrDefault();
                if (returned != null) { returned.EnergyCost.SetThisTurn(0); returned.InvokeEnergyCostChanged(); await CardPileCmd.Add(returned, PileType.Hand); }
                break;
            case 17:
                var scales = (KonoeWitchScalesPower)ModelDb.Power<KonoeWitchScalesPower>().ToMutable(); scales.Heat = N("Heat");
                await PowerCmd.Apply(c, scales, Owner.Creature, N("Damage"), Owner.Creature, this); break;
            case 18:
                await Block(p);
                foreach (var sealedEnemy in Enemies.ToArray()) await PowerCmd.Apply<KonoeAttackInterruptedPower>(c, sealedEnemy, 1, Owner.Creature, this);
                break;
            case 19:
                await CreatureCmd.Damage(c, Owner.Creature, N("HpLoss"), ValueProp.Unblockable | ValueProp.Unpowered, Owner.Creature, this, p);
                if (!Owner.Creature.IsAlive) break;
                var candidates = KonoeRareSupport.Degree(3).ToList();
                for (int i = 0; i < 3 && Owner.Creature.IsAlive && Enemies.Length > 0; i++)
                {
                    int index = Owner.RunState.Rng.CombatCardGeneration.NextInt(candidates.Count); var spell = candidates[index]; candidates.RemoveAt(index);
                    await KonoeSpellCasting.Release(c, this, p, spell, KonoeRareSupport.RandomTarget(Owner), false, false);
                }
                break;
            case 20: await Apply<KonoeSpellSublimationPower>(c, N("Cards")); break;
            case 21:
                if (p.Target?.Player is { } ally && ally != Owner && ally.Creature.IsAlive)
                {
                    var chosen = (await CardSelectCmd.FromCombatPile(c, PileType.Draw.GetPile(ally), ally, new CardSelectorPrefs(SelectionScreenPrompt, 1))).FirstOrDefault();
                    if (chosen != null) await CardPileCmd.Add(chosen, PileType.Hand);
                }
                if (await KonoeBlueSupport.ChooseElement(c, Owner) is { } gift) await KonoeElements.Gain(c, Owner, gift, this);
                break;
            case 22: await Apply<KonoeWitchProtectionPower>(c, N("HpLoss")); break;
            case 23: await Apply<KonoeCrimsonWitchFormPower>(c, N("Power")); break;
            case 24:
                await Hit(c, p, hits: N("Hits") + (Enemies.Any(e => e.HasPower<KonoeKnockdownPower>()) ? 1 : 0), all: true);
                break;
            case 25:
                if (await KonoeBlueSupport.ChooseElement(c, Owner) is { } prime)
                    for (int i = 0; i < 2; i++) await KonoeElements.Gain(c, Owner, prime, this);
                await CardPileCmd.Draw(c, N("Cards"), Owner); break;
        }
    }
}

[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeCrimsonAssault : KonoeRareCard { public KonoeCrimsonAssault() : base(0) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeAsterRoar : KonoeRareCard { public KonoeAsterRoar() : base(1) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeInfernalAnnihilation : KonoeRareCard { public KonoeInfernalAnnihilation() : base(2) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeMoltenNova : KonoeRareCard { public KonoeMoltenNova() : base(3) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeAzuriteBurial : KonoeRareCard { public KonoeAzuriteBurial() : base(4) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeColorlessInfinity : KonoeRareCard { public KonoeColorlessInfinity() : base(5) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeAutomaticChant : KonoeRareCard { public KonoeAutomaticChant() : base(6) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeScorchingBurst : KonoeRareCard { public KonoeScorchingBurst() : base(7) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeSpellCompletion : KonoeRareCard { public KonoeSpellCompletion() : base(8) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeSpellPermanence : KonoeRareCard { public KonoeSpellPermanence() : base(9) { } }

// 火之迦具土：单体重击后引爆一圈火属性余波。
[RegisterCard(typeof(KonoeCardPool))]
public sealed class KonoeKagutsuchi : KonoeRareCard
{
    public KonoeKagutsuchi() : base(10) { }
    public override IReadOnlyList<KonoeElement> GrantedElements => [KonoeElement.Fire];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => base.AdditionalHoverTips.Concat(KonoeSpellTips.Elements(KonoeDamage.FromElement(KonoeElement.Fire)));
}

// 强制拘束·Mind Eater：锁住目标，本回合无法攻击并失去格挡。
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeMindEater : KonoeRareCard { public KonoeMindEater() : base(11) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeDoubleChant : KonoeRareCard { public KonoeDoubleChant() : base(12) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeHeatDischarge : KonoeRareCard { public KonoeHeatDischarge() : base(13) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeComboRelease : KonoeRareCard { public KonoeComboRelease() : base(14) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeElementImbuement : KonoeRareCard { public KonoeElementImbuement() : base(15) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeReversalFormula : KonoeRareCard { public KonoeReversalFormula() : base(16) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeWitchScales : KonoeRareCard { public KonoeWitchScales() : base(17) { } }

// 回廊封锁：铺开格挡，并把所有敌人关在本回合之外。
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeCorridorSeal : KonoeRareCard { public KonoeCorridorSeal() : base(18) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeWitchForbiddenSpell : KonoeRareCard { public KonoeWitchForbiddenSpell() : base(19) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeSpellSublimation : KonoeRareCard { public KonoeSpellSublimation() : base(20) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeWitchGuidance : KonoeRareCard { public KonoeWitchGuidance() : base(21) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeWitchProtection : KonoeRareCard { public KonoeWitchProtection() : base(22) { } }

// 红莲魔女形态：每回合第一次攻击都会追加一次当前组合魔法。
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeCrimsonWitchForm : KonoeRareCard { public KonoeCrimsonWitchForm() : base(23) { } }

// 骸葬·镇魂曲：全场多段打击，敌人处于击倒时再多打一段。
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeRequiemBurial : KonoeRareCard { public KonoeRequiemBurial() : base(24) { } }

[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeThreePrimes : KonoeRareCard { public KonoeThreePrimes() : base(25) { } }

[RegisterCard(typeof(KonoeCardPool))]
public sealed class KonoeAbyssalFormula : KonoeRareCard
{
    private KonoeSpellKind? _resolving;
    private KonoeSpellKind _series;
    internal bool LastPlayWasCombination => _series != KonoeSpellKind.BlackStone;
    public KonoeAbyssalFormula() : base(26) { }
    public KonoeSpellKind CurrentSpell => _resolving ?? KonoeElementRules.Resolve(IsMutable ? KonoeElements.Get(Owner) : 0);
    public override TargetType TargetType => KonoeSpellValues.NeedsTarget(CurrentSpell) ? TargetType.AnyEnemy : KonoeSpellValues.TargetsAll(CurrentSpell) ? TargetType.AllEnemies : TargetType.Self;
    public override bool GainsBlock => KonoeSpellValues.For(CurrentSpell, false).Block > 0;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6, ValueProp.Move), new BlockVar(0, ValueProp.Move), new DynamicVar("Hits", 1), new DynamicVar("Stacks", 0), new DynamicVar("Bonus", 0), new DynamicVar("Turns", 0), new DynamicVar("Followup", 0), new DynamicVar("Repeat", 2)];
    public override void AfterCreated() { base.AfterCreated(); RefreshForm(); }
    internal void RefreshForm()
    {
        if (!IsMutable) return;
        var v = KonoeSpellValues.For(CurrentSpell, false);
        DynamicVars.Damage.Props = KonoeDamage.WithTypes(KonoeSpellValues.DamageProps(CurrentSpell), KonoeDamage.ForSpell(CurrentSpell) | KonoeDamage.AttachedTypes(this));
        foreach (var (key, value) in new[] { ("Damage", v.Damage), ("Block", v.Block), ("Hits", v.Hits), ("Stacks", v.Stacks), ("Bonus", v.Bonus), ("Turns", v.Turns), ("Followup", v.Followup) }) DynamicVars[key].BaseValue = value;
    }
    protected override void AddExtraArgsToDescription(LocString description)
    {
        var spellText = new LocString("cards", KonoeBlackStone.SpellKey(CurrentSpell) + ".description");
        DynamicVars.AddTo(spellText); description.Add("SpellText", spellText);
        description.Add("SpellName", new LocString("cards", KonoeBlackStone.SpellKey(CurrentSpell) + ".title"));
    }
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => base.AdditionalHoverTips.Concat(KonoeSpellTips.For(CurrentSpell, DynamicVars["Stacks"].IntValue));
    protected override void DeepCloneFields() { base.DeepCloneFields(); _resolving = null; _series = KonoeSpellKind.BlackStone; }
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        if (p.IsFirstInSeries) _series = KonoeElementRules.Resolve(KonoeElements.Get(Owner));
        _resolving = _series; RefreshForm();
        bool finished = false;
        try
        {
            for (int i = 0; i < 2; i++) await KonoeSpellCasting.Release(c, this, p, _series, p.Target, p.IsFirstInSeries && i == 0, false);
            finished = true;
        }
        finally { if (p.IsLastInSeries || !finished) _resolving = null; RefreshForm(); KonoeElements.Refresh(Owner); }
    }
}
