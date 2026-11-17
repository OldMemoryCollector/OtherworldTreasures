using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Combat.AttackHits;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using OtherworldTreasures.Scripts.Alien;

namespace OtherworldTreasures.Scripts.Konoe;

internal sealed record KonoeBlueVar(string Name, int Value, int Upgrade);
internal sealed record KonoeBlueSpec(int Cost, CardType Type, TargetType Target, int CostUpgrade, bool Multiplayer, KonoeBlueVar[] Vars);
internal static class KonoeBlueSpecs
{
    internal static readonly KonoeBlueSpec[] All = [
        new(1, CardType.Skill, TargetType.Self, -1, false, []),
        new(1, CardType.Skill, TargetType.Self, 0, false, [new("Block", 7, 3)]),
        new(1, CardType.Skill, TargetType.Self, 0, false, [new("Cards", 2, 1), new("HeatCost", 50, 0)]),
        new(1, CardType.Attack, TargetType.AnyEnemy, 0, false, [new("Damage", 9, 3), new("Heat", 10, 5)]),
        new(1, CardType.Skill, TargetType.Self, 0, false, [new("Block", 7, 3), new("Cards", 2, 0)]),
        new(1, CardType.Attack, TargetType.AnyEnemy, 0, false, [new("Damage", 8, 3), new("BonusDamage", 6, 2)]),
        new(1, CardType.Attack, TargetType.AnyEnemy, 0, false, [new("Damage", 8, 3), new("BonusDamage", 4, 2)]),
        new(1, CardType.Skill, TargetType.Self, 0, false, [new("Block", 8, 3)]),
        new(1, CardType.Skill, TargetType.Self, -1, false, []),
        new(1, CardType.Power, TargetType.Self, -1, false, [new("Power", 1, 0)]),
        new(1, CardType.Attack, TargetType.AnyEnemy, 0, false, [new("Damage", 8, 3)]),
        new(1, CardType.Attack, TargetType.AnyEnemy, 0, false, [new("Damage", 9, 3)]),
        new(1, CardType.Skill, TargetType.Self, 0, false, [new("Block", 7, 3)]),
        new(1, CardType.Skill, TargetType.Self, -1, false, []),
        new(1, CardType.Power, TargetType.Self, 0, false, [new("Power", 6, 3)]),
        new(1, CardType.Attack, TargetType.AnyEnemy, 0, false, [new("Damage", 8, 3)]),
        new(1, CardType.Skill, TargetType.Self, 0, false, [new("Block", 14, 4), new("HeatCost", 25, 0)]),
        new(1, CardType.Skill, TargetType.Self, 0, false, [new("Cards", 3, 1)]),
        new(1, CardType.Attack, TargetType.AnyEnemy, 0, false, [new("Damage", 8, 3), new("BonusDamage", 4, 2), new("HeatCost", 20, 0)]),
        new(2, CardType.Attack, TargetType.AllEnemies, 0, false, [new("Damage", 12, 4)]),
        new(1, CardType.Skill, TargetType.AnyEnemy, 0, false, [new("Damage", 8, 3)]),
        new(1, CardType.Attack, TargetType.AnyEnemy, 0, false, [new("Damage", 8, 3), new("BonusDamage", 2, 1)]),
        new(1, CardType.Skill, TargetType.Self, 0, false, [new("Block", 6, 3)]),
        new(0, CardType.Skill, TargetType.Self, 0, false, [new("Cards", 1, 1), new("Heat", 20, 5), new("HeatDebt", 20, 0)]),
        new(2, CardType.Power, TargetType.Self, -1, false, [new("Power", 1, 0)]),
        new(1, CardType.Skill, TargetType.Self, 0, false, [new("Block", 7, 3)]),
        new(1, CardType.Attack, TargetType.AnyEnemy, 0, false, [new("Damage", 9, 3)]),
        new(1, CardType.Skill, TargetType.Self, 0, false, [new("Block", 7, 3), new("BonusBlock", 5, 2), new("Cards", 1, 0), new("FireDamage", 8, 3)]),
        new(1, CardType.Power, TargetType.Self, 0, false, [new("Power", 3, 1)]),
        new(1, CardType.Power, TargetType.Self, 0, false, [new("Power", 1, 0), new("Damage", 5, 2)]),
        new(2, CardType.Attack, TargetType.AnyEnemy, 0, false, [new("Damage", 13, 3), new("Freeze", 1, 1)]),
        new(2, CardType.Attack, TargetType.AnyEnemy, 0, false, [new("Damage", 5, 2), new("AreaDamage", 10, 4)]),
        new(1, CardType.Skill, TargetType.AnyEnemy, -1, false, []),
        new(1, CardType.Skill, TargetType.Self, 0, false, [new("Block", 6, 3)]),
        new(1, CardType.Power, TargetType.Self, 0, false, [new("Power", 1, 1)]),
        new(1, CardType.Attack, TargetType.AnyEnemy, 0, true, [new("Damage", 8, 3), new("BonusDamage", 2, 1)]),
        new(1, CardType.Skill, TargetType.AnyAlly, 0, true, [new("Block", 8, 3)]),
        new(2, CardType.Power, TargetType.Self, -1, true, [new("Power", 4, 0)]),
        new(1, CardType.Skill, TargetType.Self, 0, false, [new("Cards", 2, 1), new("Elements", 1, 0)]),
        new(1, CardType.Skill, TargetType.Self, 0, false, [new("Block", 7, 2), new("BonusBlock", 5, 2)])
    ];
}

public abstract class KonoeUncommonCard : ModCardTemplate, IAttackHitHookListener
{
    private readonly int _index;
    private KonoeBlueSpec Spec => KonoeBlueSpecs.All[_index];
    protected KonoeUncommonCard(int index) : base(KonoeBlueSpecs.All[index].Cost, KonoeBlueSpecs.All[index].Type, CardRarity.Uncommon, KonoeBlueSpecs.All[index].Target, DevelopmentContent.Nine)
    {
        _index = index;
        if (index == 16) this.SecondaryCosts().Set(KonoeHeat.ResourceId, Spec.Vars.Single(v => v.Name == "HeatCost").Value);
    }
    public override bool CanBeGeneratedInCombat => true;
    public override bool CanBeGeneratedByModifiers => DevelopmentContent.Nine;
    public override CardAssetProfile AssetProfile => KonoeCardArt.Profile;
    public override bool GainsBlock => Spec.Vars.Any(v => v.Name == "Block") || _index == 10;
    public override CardMultiplayerConstraint MultiplayerConstraint => Spec.Multiplayer ? CardMultiplayerConstraint.MultiplayerOnly : CardMultiplayerConstraint.None;
    protected override IEnumerable<DynamicVar> CanonicalVars => Spec.Vars.Select(v => v.Name switch
    {
        "Damage" => (DynamicVar)new DamageVar(v.Value, ValueProp.Move),
        "Block" => new BlockVar(v.Value, ValueProp.Move),
        "Cards" => new CardsVar(v.Value),
        _ => new DynamicVar(v.Name, v.Value)
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
            if (Spec.Vars.Any(v => v.Name.StartsWith("Heat")) || _index == 34) yield return new HoverTip(new LocString("static_hover_tips", "OTHERWORLD_TREASURES_SECONDARY_RESOURCE_KONOE_HEAT.title"), new LocString("static_hover_tips", "OTHERWORLD_TREASURES_SECONDARY_RESOURCE_KONOE_HEAT.description"));
            if (_index is 1 or 25) yield return new HoverTip(new LocString("static_hover_tips", "KONOE_STORAGE.title"), new LocString("static_hover_tips", "KONOE_STORAGE.description"));
            if (_index == 5) { yield return HoverTipFactory.FromPower<KonoeAirbornePower>(); yield return HoverTipFactory.FromPower<KonoeKnockdownPower>(); }
            if (_index == 30) yield return HoverTipFactory.FromPower<KonoeFrozenPower>();
            if (_index == 19) yield return HoverTipFactory.FromPower<WeakPower>();
            if (_index == 13) yield return new HoverTip(new LocString("static_hover_tips", "KONOE_ELEMENT_INFUSION.title"), new LocString("static_hover_tips", "KONOE_ELEMENT_INFUSION.description"));
        }
    }
    public Task BeforeAttackHit(AttackHitContext c) { if (ReferenceEquals(c.CardSource, this)) AlienAttack.BeforeHit(c); return Task.CompletedTask; }
    private int N(string name) => DynamicVars[name].IntValue;
    private Task Block(CardPlay p) => CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, p);
    private Task Draw(PlayerChoiceContext c) => CardPileCmd.Draw(c, N("Cards"), Owner);
    private Creature[] Enemies => CombatState?.HittableEnemies.ToArray() ?? [];
    private async Task<AttackCommand?> Hit(PlayerChoiceContext c, CardPlay p, int? damage = null, int hits = 1, bool? all = null, bool piercing = false, Creature? target = null)
    {
        bool area = all ?? TargetType == TargetType.AllEnemies;
        target ??= p.Target;
        if (area ? Enemies.Length == 0 : target is not { IsAlive:true }) return null;
        var attack = DamageCmd.Attack(damage ?? N("Damage")).FromCard(this, p).WithHitCount(hits).WithHitFx("vfx/vfx_attack_blunt");
        if (area) attack.TargetingAllOpponents(CombatState!); else attack.Targeting(target!);
        if (piercing) AlienAttack.BypassBlock(attack);
        return await attack.Execute(c);
    }
    private Task<T?> Apply<T>(PlayerChoiceContext c, int amount) where T : PowerModel => PowerCmd.Apply<T>(c, Owner.Creature, amount, Owner.Creature, this);
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        if (CombatState is not { } combat) return;
        if (Type != CardType.Attack && _index != 32) await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);
        switch (_index)
        {
            case 0:
                if (await KonoeBlueSupport.ChooseElement(c, Owner) is { } refined)
                {
                    await KonoeElements.Gain(c, Owner, refined, this);
                    if (Owner.Creature.GetPower<KonoeElementsPower>() is { } state) state.PackedElements = KonoeBlueSupport.Pack(Enumerable.Repeat(refined, KonoeElementRules.Decode(state.PackedElements).Count));
                    KonoeElements.Refresh(Owner);
                }
                break;
            case 1:
                await Block(p);
                // 术式封存：槽位已有组合时改为调换，旧组合回到当前，不直接覆盖丢失。
                if (Owner.Creature.GetPower<KonoeStoragePower>() is { PackedElements: not 0 }) await KonoeBlueSupport.Swap(c, Owner);
                else await KonoeBlueSupport.Store(c, Owner);
                await KonoeElements.Gain(c, Owner, KonoeBlueSupport.Random(Owner), this); break;
            case 2:
                await Draw(c);
                var freeable = PileType.Hand.GetPile(Owner).Cards.Where(x => !x.EnergyCost.CostsX && x.EnergyCost.Canonical >= 0).ToList();
                if (freeable.Count > 0 && await KonoeHeat.Spend(p, N("HeatCost")))
                {
                    var selected = freeable[Owner.RunState.Rng.CombatCardGeneration.NextInt(freeable.Count)];
                    selected.EnergyCost.SetThisTurn(0);
                    selected.InvokeEnergyCostChanged();
                }
                break;
            case 3:
                await Hit(c, p);
                int burned = KonoeElementRules.Decode(KonoeElements.Get(Owner)).Count;
                KonoeElements.Consume(Owner);
                await KonoeHeat.GainFromPlay(p, burned * N("Heat")); break;
            case 4:
                await Block(p); await Apply<KonoeTeleportDrawPower>(c, N("Cards")); break;
            case 5:
                bool floating = p.Target?.HasPower<KonoeAirbornePower>() == true;
                await Hit(c, p);
                if (floating)
                {
                    await Hit(c, p, N("BonusDamage"));
                    if (p.Target is { IsAlive:true } target) await PowerCmd.Apply<KonoeKnockdownPower>(c, target, 1, Owner.Creature, this);
                }
                break;
            case 6:
                int types = KonoeElementRules.Decode(KonoeElements.Get(Owner)).Distinct().Count();
                await Hit(c, p);
                if (types > 0) await Hit(c, p, N("BonusDamage"), hits: types); break;
            case 7:
                await Block(p); await Apply<KonoeManaReboundPower>(c, 1); break;
            case 8:
                if (await KonoeBlueSupport.ChooseElement(c, Owner) is { } delayed)
                {
                    await KonoeElements.Gain(c, Owner, delayed, this);
                    var power = (KonoeDelayedElementPower)ModelDb.Power<KonoeDelayedElementPower>().ToMutable();
                    power.Element = (int)delayed; power.DueTurn = Owner.PlayerCombatState!.TurnNumber + 1;
                    await PowerCmd.Apply(c, power, Owner.Creature, 1, Owner.Creature, this);
                }
                break;
            case 9: await Apply<KonoeSpellEchoPower>(c, N("Power")); break;
            case 10:
                bool attacking = p.Target?.Monster?.IntendsToAttack == true;
                var attack = await Hit(c, p);
                if (attacking && attack != null) await CreatureCmd.GainBlock(Owner.Creature, attack.Results.SelectMany(x => x).Sum(x => x.TotalDamage), ValueProp.Move, p);
                break;
            case 11:
                await Hit(c, p);
                if (p.Target is { IsAlive:true } severed)
                {
                    await CreatureCmd.LoseBlock(c, severed, severed.Block, Owner.Creature);
                    await PowerCmd.Apply<KonoeAttackInterruptedPower>(c, severed, 1, Owner.Creature, this);
                }
                break;
            case 12:
                await Block(p); await Apply<KonoeSpellTracePower>(c, 1); break;
            case 13:
                var guided = (await CardSelectCmd.FromCombatPile(c, PileType.Draw.GetPile(Owner), Owner, new CardSelectorPrefs(SelectionScreenPrompt, 1), KonoeDamage.CanReceiveInfusion)).FirstOrDefault();
                if (guided != null) { await CardPileCmd.Add(guided, PileType.Hand); await KonoeBlueSupport.Infuse(c, Owner, guided); } break;
            case 14: await Apply<KonoeMagicCurtainPower>(c, N("Power")); break;
            case 15:
                var previous = CombatManager.Instance.History.CardPlaysFinished.LastOrDefault(e => e.CardPlay.Player == Owner && e.HappenedThisTurn(combat) && e.CardPlay.Card.Type == CardType.Attack)?.CardPlay.Card;
                await Hit(c, p);
                if (previous != null && previous != this && previous.Pile != null && previous.Pile.Type != PileType.Play)
                {
                    previous.EnergyCost.AddThisTurn(1); previous.InvokeEnergyCostChanged(); await CardPileCmd.Add(previous, PileType.Hand);
                }
                break;
            case 16:
                await Block(p);
                break;
            case 17:
                await Draw(c);
                int discardCount = Math.Min(2, PileType.Hand.GetPile(Owner).Cards.Count);
                if (discardCount > 0)
                    await CardCmd.Discard(c, await CardSelectCmd.FromHandForDiscard(c, Owner, new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, discardCount), null, this));
                break;
            case 18:
                bool piercing = await KonoeBlueSupport.Confirm(c, Owner, N("HeatCost")) && await KonoeHeat.Spend(p, N("HeatCost"));
                await Hit(c, p, piercing ? N("Damage") + N("BonusDamage") : (int?)null, piercing: piercing); break;
            case 19:
                await Hit(c, p);
                if (await KonoeBlueSupport.ChooseElement(c, Owner) is { } collapsed)
                {
                    var elements = KonoeElementRules.Decode(KonoeElements.Get(Owner)); int count = elements.Count(e => e == collapsed);
                    if (Owner.Creature.GetPower<KonoeElementsPower>() is { } state) state.PackedElements = KonoeBlueSupport.Pack(elements.Where(e => e != collapsed));
                    KonoeElements.Refresh(Owner);
                    if (count > 0) await PowerCmd.Apply<WeakPower>(c, Enemies, count, Owner.Creature, this);
                }
                break;
            case 20:
                await Hit(c, p);
                if (p.Target is { IsAlive:true } displaced) await PowerCmd.Apply<KonoeAttackInterruptedPower>(c, displaced, 1, Owner.Creature, this);
                break;
            case 21:
                int debuffs = p.Target?.Powers.Where(x => x.IsVisible && x.GetTypeForAmount(x.Amount) == PowerType.Debuff).Select(x => x.Id).Distinct().Count() ?? 0;
                await Hit(c, p);
                if (debuffs > 0) await Hit(c, p, N("BonusDamage"), hits: debuffs); break;
            case 22:
                await Block(p);
                if (await KonoeBlueSupport.ChooseElement(c, Owner, KonoeElementRules.Decode(KonoeElements.Get(Owner))) is { } copied) await KonoeElements.Gain(c, Owner, copied, this);
                break;
            case 23:
                await Draw(c); await KonoeHeat.GainFromPlay(p, N("Heat"));
                var debt = (KonoeHeatDebtPower)ModelDb.Power<KonoeHeatDebtPower>().ToMutable(); debt.DueTurn = Owner.PlayerCombatState!.TurnNumber + 1;
                await PowerCmd.Apply(c, debt, Owner.Creature, N("HeatDebt"), Owner.Creature, this); break;
            case 24: await Apply<KonoeOffensiveAfterimagePower>(c, N("Power")); break;
            case 25:
                await Block(p);
                // 翠绿之门：不弹选择，按槽位状态自动执行——槽位空则存储，已有则交换。
                if (Owner.Creature.GetPower<KonoeStoragePower>() is { PackedElements: not 0 }) await KonoeBlueSupport.Swap(c, Owner);
                else await KonoeBlueSupport.Store(c, Owner);
                break;
            case 26:
                await Hit(c, p);
                if (p.Target is { IsAlive:true } pressed) await PowerCmd.Apply<KonoeKnockdownPower>(c, pressed, 1, Owner.Creature, this);
                break;
            case 27:
                var latest = KonoeElementRules.Decode(KonoeElements.Get(Owner)).LastOrDefault();
                await Block(p);
                if (latest == KonoeElement.Water) await CreatureCmd.GainBlock(Owner.Creature, N("BonusBlock"), ValueProp.Move, p);
                else if (latest == KonoeElement.Wind) await Draw(c);
                else if (latest == KonoeElement.Fire && Enemies.Length > 0)
                {
                    var fire = DamageCmd.Attack(N("FireDamage")).FromCard(this, p).TargetingRandomOpponents(combat).WithAttackerAnim("Cast", Owner.Character.CastAnimDelay);
                    KonoeDamage.Configure(fire, KonoeDamageType.Fire);
                    await fire.Execute(c);
                }
                break;
            case 28: await Apply<KonoeManaOverflowPower>(c, N("Power")); break;
            case 29: await Apply<KonoeFamiliarPactPower>(c, N("Damage")); break;
            case 30:
                bool freezing = p.Target?.Monster?.IntendsToAttack == true;
                await Hit(c, p);
                if (freezing && p.Target is { IsAlive:true } frozen) await PowerCmd.Apply<KonoeFrozenPower>(c, frozen, N("Freeze"), Owner.Creature, this);
                break;
            case 31:
                var single = combat.CreateCard(ModelDb.Card<KonoeVolleySingleChoice>(), Owner); single.DynamicVars.Damage.BaseValue = N("Damage");
                var area = combat.CreateCard(ModelDb.Card<KonoeVolleyAllChoice>(), Owner); area.DynamicVars.Damage.BaseValue = N("AreaDamage");
                var volley = await CardSelectCmd.FromChooseACardScreen(c, new[] { single, area }, Owner);
                if (volley is KonoeVolleySingleChoice) await Hit(c, p, hits: 3, all: false);
                else if (volley is KonoeVolleyAllChoice) await Hit(c, p, N("AreaDamage"), all: true);
                break;
            case 32:
                var possible = Enum.GetValues<KonoeSpellKind>().Where(spell => KonoeBlueSupport.SpellElements(spell).Count is 1 or 2).ToList();
                var spells = new List<CardModel>();
                for (int i = 0; i < 3; i++)
                {
                    int at = Owner.RunState.Rng.CombatCardGeneration.NextInt(possible.Count);
                    var card = (KonoeImprovisedSpellChoice)combat.CreateCard(ModelDb.Card<KonoeImprovisedSpellChoice>(), Owner); card.Configure(possible[at]); possible.RemoveAt(at); spells.Add(card);
                }
                if (await CardSelectCmd.FromChooseACardScreen(c, spells, Owner) is KonoeImprovisedSpellChoice selectedSpell)
                    await KonoeSpellCasting.Release(c, this, p, selectedSpell.Spell, p.Target, false, false);
                break;
            case 33: await Block(p); await Apply<KonoeInvertingMirrorPower>(c, 1); break;
            case 34: await Apply<KonoeDominionEchoPower>(c, N("Power")); break;
            case 35:
                await Hit(c, p);
                if (p.Target is { IsAlive:true } pressured) await PowerCmd.Apply<KonoeCrossPressurePower>(c, pressured, N("BonusDamage"), Owner.Creature, this);
                break;
            case 36:
                await Block(p);
                if (p.Target is { IsAlive:true } ally && ally.Player != null && ally.Player != Owner)
                    await CreatureCmd.GainBlock(ally, decimal.Floor(Owner.Creature.Block / 2m), ValueProp.Unpowered, p);
                break;
            case 37: await Apply<KonoeResonantSpellPower>(c, N("Power")); break;
            case 38:
                await Draw(c);
                if (Owner.Creature.IsAlive) await KonoeElements.Gain(c, Owner, KonoeBlueSupport.Random(Owner), this);
                KonoeElements.Refresh(Owner);
                break;
            case 39:
                await Block(p);
                bool chained = CombatManager.Instance.History.CardPlaysFinished.Any(e => e.CardPlay.Player == Owner && e.HappenedThisTurn(combat) && e.CardPlay.Card.Type == CardType.Skill && !ReferenceEquals(e.CardPlay.Card, this));
                if (chained) await CreatureCmd.GainBlock(Owner.Creature, N("BonusBlock"), ValueProp.Move, p);
                break;
        }
    }
}

[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeRefinement : KonoeUncommonCard { public KonoeRefinement() : base(0) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeSeal : KonoeUncommonCard { public KonoeSeal() : base(1) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeRapidCancel : KonoeUncommonCard { public KonoeRapidCancel() : base(2) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeManaBurn : KonoeUncommonCard { public KonoeManaBurn() : base(3) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeUncommonTeleport : KonoeUncommonCard { public KonoeUncommonTeleport() : base(4) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeAerialPursuit : KonoeUncommonCard { public KonoeAerialPursuit() : base(5) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeElementRift : KonoeUncommonCard { public KonoeElementRift() : base(6) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeManaRebound : KonoeUncommonCard { public KonoeManaRebound() : base(7) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeDelayedChant : KonoeUncommonCard { public KonoeDelayedChant() : base(8) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeSpellEcho : KonoeUncommonCard { public KonoeSpellEcho() : base(9) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeSkyIntercept : KonoeUncommonCard { public KonoeSkyIntercept() : base(10) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeBarrierSever : KonoeUncommonCard { public KonoeBarrierSever() : base(11) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeSpellTrace : KonoeUncommonCard { public KonoeSpellTrace() : base(12) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeMagicGuidance : KonoeUncommonCard { public KonoeMagicGuidance() : base(13) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeMagicCurtain : KonoeUncommonCard { public KonoeMagicCurtain() : base(14) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeComboRecycle : KonoeUncommonCard { public KonoeComboRecycle() : base(15) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeResidualWall : KonoeUncommonCard { public KonoeResidualWall() : base(16) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeSpellRedraw : KonoeUncommonCard { public KonoeSpellRedraw() : base(17) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoePiercingBullet : KonoeUncommonCard { public KonoePiercingBullet() : base(18) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeSpellCollapse : KonoeUncommonCard { public KonoeSpellCollapse() : base(19) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeWillDisplace : KonoeUncommonCard { public KonoeWillDisplace() : base(20) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeWeakpointPierce : KonoeUncommonCard { public KonoeWeakpointPierce() : base(21) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeElementCopy : KonoeUncommonCard { public KonoeElementCopy() : base(22) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeHeatAdvance : KonoeUncommonCard { public KonoeHeatAdvance() : base(23) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeOffensiveAfterimage : KonoeUncommonCard { public KonoeOffensiveAfterimage() : base(24) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeEmeraldGate : KonoeUncommonCard { public KonoeEmeraldGate() : base(25) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeAzurePressure : KonoeUncommonCard { public KonoeAzurePressure() : base(26) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeSulfurEnhancement : KonoeUncommonCard { public KonoeSulfurEnhancement() : base(27) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeManaOverflow : KonoeUncommonCard { public KonoeManaOverflow() : base(28) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeFamiliarPact : KonoeUncommonCard { public KonoeFamiliarPact() : base(29) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeFrostBullet : KonoeUncommonCard { public KonoeFrostBullet() : base(30) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeBulletArray : KonoeUncommonCard { public KonoeBulletArray() : base(31) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeImprovisedCasting : KonoeUncommonCard { public KonoeImprovisedCasting() : base(32) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeInvertingMirror : KonoeUncommonCard { public KonoeInvertingMirror() : base(33) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeDominionEcho : KonoeUncommonCard { public KonoeDominionEcho() : base(34) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeCrossPressure : KonoeUncommonCard { public KonoeCrossPressure() : base(35) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeMirrorWall : KonoeUncommonCard { public KonoeMirrorWall() : base(36) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeResonantSpell : KonoeUncommonCard { public KonoeResonantSpell() : base(37) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeSpiritDispatch : KonoeUncommonCard { public KonoeSpiritDispatch() : base(38) { } }
[RegisterCard(typeof(KonoeCardPool))] public sealed class KonoeBarrierChain : KonoeUncommonCard { public KonoeBarrierChain() : base(39) { } }
