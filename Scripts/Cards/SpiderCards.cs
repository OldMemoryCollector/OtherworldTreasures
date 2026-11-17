using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using OtherworldTreasures.Scripts.Spider;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models.Capabilities;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Cards;

public abstract class SpiderCard(int cost, CardType type, CardRarity rarity, TargetType target) : ModCardTemplate(cost, type, rarity, target, DevelopmentContent.Spider)
{
    public override bool CanBeGeneratedInCombat => false;
    public override bool CanBeGeneratedByModifiers => false;
    public override int MaxUpgradeLevel => 0;
    public override CardAssetProfile AssetProfile => new(PortraitPath: "res://images/powers/missing_power.png");
}
public abstract class IroncladSpiderCard(int cost, CardType type, CardRarity rarity, TargetType target) : SpiderCard(cost, type, rarity, target)
{ public override CardPoolModel VisualCardPool => ModelDb.CardPool<IroncladCardPool>(); }
public abstract class SilentSpiderCard(int cost, CardType type, CardRarity rarity, TargetType target) : SpiderCard(cost, type, rarity, target)
{ public override CardPoolModel VisualCardPool => ModelDb.CardPool<SilentCardPool>(); }
public abstract class DefectSpiderCard(int cost, CardType type, CardRarity rarity, TargetType target) : SpiderCard(cost, type, rarity, target)
{ public override CardPoolModel VisualCardPool => ModelDb.CardPool<DefectCardPool>(); }

[RegisterCard(typeof(TokenCardPool))]
public sealed class SpiderWeb : IroncladSpiderCard
{
    public SpiderWeb() : base(2, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<SpiderWebPower>()];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p) =>
        await PowerCmd.Apply<SpiderWebGeneratorPower>(c, Owner.Creature, 1, Owner.Creature, this);
}

[RegisterCard(typeof(TokenCardPool))]
public sealed class HangingByASilk : IroncladSpiderCard
{
    public HangingByASilk() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy) { }
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<SpiderSilkPower>(), HoverTipFactory.FromPower<VulnerablePower>()];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        int stacks = p.Target?.GetPower<VulnerablePower>()?.Amount ?? 0;
        int silk = SpiderRules.SilkFromVulnerable(stacks);
        if (silk > 0) await PowerCmd.Apply<SpiderSilkPower>(c, Owner.Creature, silk, Owner.Creature, this);
    }
}

[RegisterCard(typeof(TokenCardPool))]
public sealed class SpiderEcho : IroncladSpiderCard
{
    public SpiderEcho() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self) { }
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p) =>
        await PowerCmd.Apply<SpiderEchoPower>(c, Owner.Creature, 1, Owner.Creature, this);
}

[RegisterCard(typeof(TokenCardPool))]
public sealed class SpiderFlyingBlades : SilentSpiderCard
{
    public SpiderFlyingBlades() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromCard<SpiderShiv>()];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p) =>
        await CardPileCmd.AddToCombatAndPreview<SpiderShiv>(Owner.Creature, PileType.Hand, 3, Owner);
}

[RegisterCard(typeof(TokenCardPool))]
public sealed class SpiderShiv : SilentSpiderCard
{
    public SpiderShiv() : base(0, CardType.Attack, CardRarity.Token, TargetType.AnyEnemy) { }
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override HashSet<CardTag> CanonicalTags => [CardTag.Shiv];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(4m, ValueProp.Move)];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromCard<Shiv>()];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        if (p.Target == null) return;
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, p).Targeting(p.Target)
            .WithHitFx("vfx/vfx_attack_slash").Execute(c);
        await Shiv.CreateInHand(Owner, CombatState!);
    }
}

[RegisterCard(typeof(TokenCardPool))]
public sealed class SpiderVenomInjection : SilentSpiderCard
{
    public SpiderVenomInjection() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy) { }
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<SpiderVenomPower>()];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    { if (p.Target != null) await PowerCmd.Apply<SpiderVenomPower>(c, p.Target, 4, Owner.Creature, this); }
}

[RegisterCard(typeof(TokenCardPool))]
public sealed class SpiderHunt : SilentSpiderCard
{
    public SpiderHunt() : base(2, CardType.Skill, CardRarity.Rare, TargetType.Self) { }
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromCard<SpiderShiv>(), HoverTipFactory.FromPower<SpiderVenomPower>()];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        await PowerCmd.Apply<SpiderHuntPower>(c, Owner.Creature, 1, Owner.Creature, this);
        await CardPileCmd.Draw(c, 2, Owner);
        var cards = (await CardSelectCmd.FromHandForDiscard(c, Owner,
            new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, 2), null, this)).ToArray();
        foreach (var card in cards) await CardCmd.Discard(c, card);
    }
}

[RegisterCard(typeof(TokenCardPool))]
public sealed class SpiderChannel : DefectSpiderCard
{
    public SpiderChannel() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromOrb<SpiderWebOrb>()];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        int count = CombatState?.HittableEnemies.Count ?? 0;
        for (int i = 0; i < count; i++) await OrbCmd.Channel<SpiderWebOrb>(c, Owner);
    }
}

[RegisterCard(typeof(TokenCardPool))]
public sealed class SpiderStatusConversion : DefectSpiderCard
{
    public SpiderStatusConversion() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromCard<SpiderRecovery>()];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        var cards = PileType.Hand.GetPile(Owner).Cards.Where(card => card.Type == CardType.Status).ToArray();
        foreach (var card in cards)
        {
            var replacement = CombatState!.CreateCard<SpiderRecovery>(Owner);
            await CardCmd.Transform(card, replacement);
        }
    }
}

[RegisterCard(typeof(TokenCardPool))]
public sealed class SpiderRecovery : DefectSpiderCard
{
    public SpiderRecovery() : base(0, CardType.Skill, CardRarity.Token, TargetType.Self) { }
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        var selected = (await CardSelectCmd.FromCombatPile(c, PileType.Discard.GetPile(Owner), Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, 1))).FirstOrDefault();
        if (selected != null) await CardPileCmd.Add(selected, PileType.Hand);
    }
}

[RegisterCard(typeof(TokenCardPool))]
public sealed class SteelSpiderWeb : DefectSpiderCard
{
    public SteelSpiderWeb() : base(1, CardType.Power, CardRarity.Rare, TargetType.Self) { }
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p) =>
        await PowerCmd.Apply<SteelSpiderWebPower>(c, Owner.Creature, 1, Owner.Creature, this);
}

[RegisterCard(typeof(TokenCardPool))]
public sealed class SpiderEtherealMark : SpiderCard
{
    public SpiderEtherealMark() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    public override CardPoolModel VisualCardPool => ModelDb.CardPool<NecrobinderCardPool>();
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        var selected = (await CardSelectCmd.FromCombatPile(c, PileType.Draw.GetPile(Owner), Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, 1),
            card => card.Keywords.Contains(CardKeyword.Ethereal) && card.Capability<SpiderEtherealMarkCapability>() == null)).FirstOrDefault();
        selected?.GetOrCreateCapability<SpiderEtherealMarkCapability>();
    }
}
