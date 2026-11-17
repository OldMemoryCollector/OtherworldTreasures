using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Konoe;

// 连招（定稿规则）：打出后此卡**返回手牌**，并在手牌中**变化为下一阶段招式**；
// 此卡**离开手牌**时变回原始卡牌——无论被打出、弃置、消耗还是洗回牌堆。
// 阶段招式是 Token，不进奖励卡池、不计入卡池数量。
internal static class KonoeCombo
{
    // 打出后回到手牌：改写「打出结算落到哪个牌堆」，由引擎自己搬牌并播返回动画。
    internal static CardLocation HandLocation(Player owner) => new(owner, PileType.Hand, CardPilePosition.Bottom);

    // 同一张牌在两种形态之间就地变形（用 Transform，不是「生成一张新牌」），升级等级随形态带走。
    internal static async Task Morph(CardModel from, CardModel toPrototype)
    {
        if (from.CombatState is not { } combat || from.Pile is null || !from.Owner.Creature.IsAlive) return;
        var to = combat.CreateCard(toPrototype, from.Owner);
        for (int i = 0; i < from.CurrentUpgradeLevel && to.IsUpgradable; i++)
        {
            to.UpgradeInternal();
            to.FinalizeUpgradeInternal();
        }
        await CardCmd.Transform(from, to, CardPreviewStyle.None);
    }

    // 阶段招式离开手牌后落进稳定牌堆（弃牌／消耗／抽牌）时才算「离开」，Play 是打出途中的中转堆。
    internal static bool LeavesHand(CardModel self, PileType oldPileType)
    {
        if (self.Pile is not { } pile || pile.Type == oldPileType) return false;
        return pile.Type is not (PileType.Hand or PileType.Play or PileType.None);
    }
}

// 连招起手：普通／罕见攻击牌，打出后衔接下一阶段。
// 每个攻击阶段都是一个独立的属性攻击：按元素伤害结算、按元素获得元素。
public abstract class KonoeComboCard : ModCardTemplate, IKonoeElementSource
{
    private readonly KonoeElement _element;
    protected KonoeComboCard(int cost, CardRarity rarity, TargetType target, KonoeElement element = default)
        : base(cost, CardType.Attack, rarity, target, DevelopmentContent.Nine) => _element = element;
    public override bool CanBeGeneratedInCombat => true;
    public override bool CanBeGeneratedByModifiers => DevelopmentContent.Nine;
    public override CardAssetProfile AssetProfile => KonoeCardArt.Profile;
    // 连招规则由关键词悬浮提示承担，卡面只写这一阶段的实际效果。
    public override IEnumerable<CardKeyword> CanonicalKeywords => [MyKeywords.Combo];
    public IReadOnlyList<KonoeElement> GrantedElements => _element == default ? [] : [_element];
    protected abstract CardModel CreateStage();
    protected abstract IEnumerable<IHoverTip> StageTips { get; }
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        _element == default ? StageTips : StageTips.Concat(KonoeSpellTips.Elements(KonoeDamage.FromElement(_element)));
    protected Task Cast() => CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);
    protected Creature[] Enemies => CombatState?.HittableEnemies.ToArray() ?? [];
    protected async Task Hit(PlayerChoiceContext context, CardPlay play, int? damage = null, int hits = 1, bool? all = null)
    {
        bool area = all ?? TargetType == TargetType.AllEnemies;
        if (area ? Enemies.Length == 0 : play.Target is not { IsAlive: true }) return;
        var attack = DamageCmd.Attack(damage ?? DynamicVars.Damage.IntValue).FromCard(this, play).WithHitCount(hits).WithHitFx("vfx/vfx_attack_blunt");
        if (area) attack.TargetingAllOpponents(CombatState!); else attack.Targeting(play.Target!);
        await attack.Execute(context);
    }
    protected Task Block(CardPlay play) => CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);
    protected Task Draw(PlayerChoiceContext context) => CardPileCmd.Draw(context, DynamicVars.Cards.IntValue, Owner);
    // 连招：打出后返回手牌（若被「消耗」类效果改写了去处，就按改写的走）。
    protected override CardLocation GetResultLocationForCardPlay()
    {
        var normal = base.GetResultLocationForCardPlay();
        return normal.pileType == PileType.Discard ? KonoeCombo.HandLocation(Owner) : normal;
    }

    // 回到手牌后立刻变为下一阶段招式。
    public override async Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? clonedBy)
    {
        if (!ReferenceEquals(card, this)) return;
        if (oldPileType != PileType.Play || Pile?.Type != PileType.Hand) return;
        await KonoeCombo.Morph(this, CreateStage());
    }
}

// 连招衍生阶段：只能由上一阶段生成，不进奖励卡池。
public abstract class KonoeComboStage : ModCardTemplate, IKonoeElementSource
{
    private readonly KonoeElement _element;
    protected KonoeComboStage(int cost, CardType type, TargetType target, KonoeElement element = default)
        : base(cost, type, CardRarity.Token, target, false) => _element = element;
    public override bool CanBeGeneratedInCombat => false;
    public override bool CanBeGeneratedByModifiers => false;
    public override CardAssetProfile AssetProfile => KonoeCardArt.Profile;
    public IReadOnlyList<KonoeElement> GrantedElements => _element == default ? [] : [_element];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        _element == default ? [] : KonoeSpellTips.Elements(KonoeDamage.FromElement(_element));
    protected abstract CardModel CreateOriginal();

    // 离开手牌（被打出、被弃置、被消耗、被洗回）时变回原始卡牌。
    public override async Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? clonedBy)
    {
        if (!ReferenceEquals(card, this)) return;
        if (!KonoeCombo.LeavesHand(this, oldPileType)) return;
        await KonoeCombo.Morph(this, CreateOriginal());
    }
    protected Creature[] Enemies => CombatState?.HittableEnemies.ToArray() ?? [];
    protected async Task Hit(PlayerChoiceContext context, CardPlay play, int? damage = null, int hits = 1, bool? all = null)
    {
        bool area = all ?? TargetType == TargetType.AllEnemies;
        if (area ? Enemies.Length == 0 : play.Target is not { IsAlive: true }) return;
        var attack = DamageCmd.Attack(damage ?? DynamicVars.Damage.IntValue).FromCard(this, play).WithHitCount(hits).WithHitFx("vfx/vfx_attack_blunt");
        if (area) attack.TargetingAllOpponents(CombatState!); else attack.Targeting(play.Target!);
        await attack.Execute(context);
    }
    protected Task Draw(PlayerChoiceContext context) => CardPileCmd.Draw(context, DynamicVars.Cards.IntValue, Owner);
}

// ── 普通：风刃再击（两段，只衔接一次）────────────────────────────────
[RegisterCard(typeof(KonoeCardPool))]
public sealed class KonoeWindBladeRepeat : KonoeComboCard
{
    public KonoeWindBladeRepeat() : base(1, CardRarity.Common, TargetType.AnyEnemy, KonoeElement.Wind) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(8, ValueProp.Move)];
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
    protected override CardModel CreateStage() => ModelDb.Card<KonoeWindBladeRepeatStage>();
    protected override IEnumerable<IHoverTip> StageTips => [HoverTipFactory.FromCard<KonoeWindBladeRepeatStage>()];
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play) { await Hit(context, play); }
}

[RegisterCard(typeof(KonoeCardPool))]
public sealed class KonoeWindBladeRepeatStage : KonoeComboStage
{
    public KonoeWindBladeRepeatStage() : base(0, CardType.Attack, TargetType.AnyEnemy, KonoeElement.Wind) { }
    protected override CardModel CreateOriginal() => ModelDb.Card<KonoeWindBladeRepeat>();
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(5, ValueProp.Move)];
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(2);
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => Hit(context, play);
}

// ── 罕见：对空风势 → 对空风势·落 ────────────────────────────────────
[RegisterCard(typeof(KonoeCardPool))]
public sealed class KonoeAntiAirMomentum : KonoeComboCard
{
    public KonoeAntiAirMomentum() : base(1, CardRarity.Uncommon, TargetType.AnyEnemy, KonoeElement.Wind) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(8, ValueProp.Move), new DynamicVar("BonusDamage", 4)];
    protected override void OnUpgrade() { DynamicVars.Damage.UpgradeValueBy(3); DynamicVars["BonusDamage"].UpgradeValueBy(2); }
    protected override CardModel CreateStage() => ModelDb.Card<KonoeAntiAirMomentumStage>();
    protected override IEnumerable<IHoverTip> StageTips => [HoverTipFactory.FromCard<KonoeAntiAirMomentumStage>(), HoverTipFactory.FromPower<KonoeAirbornePower>()];
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        bool airborne = play.Target?.HasPower<KonoeAirbornePower>() == true;
        await Hit(context, play);
        if (airborne) await Hit(context, play, DynamicVars["BonusDamage"].IntValue);
    }
}

[RegisterCard(typeof(KonoeCardPool))]
public sealed class KonoeAntiAirMomentumStage : KonoeComboStage
{
    public KonoeAntiAirMomentumStage() : base(0, CardType.Attack, TargetType.AnyEnemy, KonoeElement.Wind) { }
    protected override CardModel CreateOriginal() => ModelDb.Card<KonoeAntiAirMomentum>();
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6, ValueProp.Move)];
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await Hit(context, play);
        if (play.Target is { IsAlive: true } target) await PowerCmd.Apply<KonoeKnockdownPower>(context, target, 1, Owner.Creature, this);
    }
}

// ── 罕见：低位连扫 → 低位连扫·斩 ────────────────────────────────────
[RegisterCard(typeof(KonoeCardPool))]
public sealed class KonoeLowChainSweep : KonoeComboCard
{
    public KonoeLowChainSweep() : base(1, CardRarity.Uncommon, TargetType.AnyEnemy, KonoeElement.Wind) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(8, ValueProp.Move)];
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
    protected override CardModel CreateStage() => ModelDb.Card<KonoeLowChainSweepStage>();
    protected override IEnumerable<IHoverTip> StageTips => [HoverTipFactory.FromCard<KonoeLowChainSweepStage>()];
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play) { await Hit(context, play); }
}

[RegisterCard(typeof(KonoeCardPool))]
public sealed class KonoeLowChainSweepStage : KonoeComboStage
{
    public KonoeLowChainSweepStage() : base(0, CardType.Attack, TargetType.AnyEnemy, KonoeElement.Fire) { }
    protected override CardModel CreateOriginal() => ModelDb.Card<KonoeLowChainSweep>();
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(8, ValueProp.Move)];
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => Hit(context, play);
}

// ── 罕见：空中连舞 → 空中连舞·终 ────────────────────────────────────
[RegisterCard(typeof(KonoeCardPool))]
public sealed class KonoeAerialDance : KonoeComboCard
{
    public KonoeAerialDance() : base(1, CardRarity.Uncommon, TargetType.AnyEnemy, KonoeElement.Water) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(4, ValueProp.Move), new DynamicVar("Hits", 2)];
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(2);
    protected override CardModel CreateStage() => ModelDb.Card<KonoeAerialDanceStage>();
    protected override IEnumerable<IHoverTip> StageTips => [HoverTipFactory.FromPower<KonoeAirbornePower>(), HoverTipFactory.FromCard<KonoeAerialDanceStage>()];
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await Hit(context, play, hits: DynamicVars["Hits"].IntValue);
        if (play.Target is { IsAlive: true } target && !target.HasPower<KonoeAirbornePower>())
            await PowerCmd.Apply<KonoeAirbornePower>(context, target, 1, Owner.Creature, this);
    }
}

[RegisterCard(typeof(KonoeCardPool))]
public sealed class KonoeAerialDanceStage : KonoeComboStage
{
    public KonoeAerialDanceStage() : base(1, CardType.Attack, TargetType.AnyEnemy, KonoeElement.Wind) { }
    protected override CardModel CreateOriginal() => ModelDb.Card<KonoeAerialDance>();
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(12, ValueProp.Move)];
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(4);
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => Hit(context, play);
}

// ── 罕见：魔导连奏 → 魔导连奏·终 ────────────────────────────────────
[RegisterCard(typeof(KonoeCardPool))]
public sealed class KonoeMagusCadenza : KonoeComboCard
{
    public KonoeMagusCadenza() : base(1, CardRarity.Uncommon, TargetType.AnyEnemy, KonoeElement.Water) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(8, ValueProp.Move)];
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(2);
    protected override CardModel CreateStage() => ModelDb.Card<KonoeMagusCadenzaStage>();
    protected override IEnumerable<IHoverTip> StageTips =>
    [
        HoverTipFactory.FromCard<KonoeMagusCadenzaStage>(),
        new HoverTip(new LocString("static_hover_tips", "KONOE_ELEMENTS.title"), new LocString("static_hover_tips", "KONOE_ELEMENTS.description"))
    ];
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await Hit(context, play);
        await KonoeElements.Gain(context, Owner, KonoeBlueSupport.Random(Owner), this);
        KonoeElements.Refresh(Owner);
    }
}

[RegisterCard(typeof(KonoeCardPool))]
public sealed class KonoeMagusCadenzaStage : KonoeComboStage
{
    public KonoeMagusCadenzaStage() : base(0, CardType.Attack, TargetType.AnyEnemy, KonoeElement.Fire) { }
    protected override CardModel CreateOriginal() => ModelDb.Card<KonoeMagusCadenza>();
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(8, ValueProp.Move)];
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await Hit(context, play);
        await KonoeElements.Gain(context, Owner, KonoeBlueSupport.Random(Owner), this);
        KonoeElements.Refresh(Owner);
    }
}

// ── 罕见：风击二择 → 风击二择·续 ────────────────────────────────────
[RegisterCard(typeof(KonoeCardPool))]
public sealed class KonoeWindStrikeChoice : KonoeComboCard
{
    public KonoeWindStrikeChoice() : base(1, CardRarity.Uncommon, TargetType.AnyEnemy, KonoeElement.Wind) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(8, ValueProp.Move), new CardsVar(1)];
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(2);
    protected override CardModel CreateStage() => ModelDb.Card<KonoeWindStrikeChoiceStage>();
    protected override IEnumerable<IHoverTip> StageTips => [HoverTipFactory.FromCard<KonoeWindStrikeChoiceStage>()];
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play) { await Hit(context, play); await Draw(context); }
}

[RegisterCard(typeof(KonoeCardPool))]
public sealed class KonoeWindStrikeChoiceStage : KonoeComboStage
{
    public KonoeWindStrikeChoiceStage() : base(0, CardType.Attack, TargetType.AnyEnemy, KonoeElement.Wind) { }
    protected override CardModel CreateOriginal() => ModelDb.Card<KonoeWindStrikeChoice>();
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6, ValueProp.Move), new CardsVar(1)];
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(2);
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play) { await Hit(context, play); await Draw(context); }
}

// ── 罕见：真红追击 → 真红追击·袭 ────────────────────────────────────
[RegisterCard(typeof(KonoeCardPool))]
public sealed class KonoeCrimsonPursuit : KonoeComboCard
{
    public KonoeCrimsonPursuit() : base(2, CardRarity.Uncommon, TargetType.AnyEnemy, KonoeElement.Fire) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(11, ValueProp.Move)];
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
    protected override CardModel CreateStage() => ModelDb.Card<KonoeCrimsonPursuitStage>();
    protected override IEnumerable<IHoverTip> StageTips => [HoverTipFactory.FromCard<KonoeCrimsonPursuitStage>()];
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play) { await Cast(); await Hit(context, play); }
}

[RegisterCard(typeof(KonoeCardPool))]
public sealed class KonoeCrimsonPursuitStage : KonoeComboStage
{
    public KonoeCrimsonPursuitStage() : base(1, CardType.Attack, TargetType.AnyEnemy, KonoeElement.Fire) { }
    protected override CardModel CreateOriginal() => ModelDb.Card<KonoeCrimsonPursuit>();
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(10, ValueProp.Move)];
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => Hit(context, play);
}

// ── 罕见：坠地狱葬 → 坠地狱葬·灭 ────────────────────────────────────
[RegisterCard(typeof(KonoeCardPool))]
public sealed class KonoeHellfallBurial : KonoeComboCard
{
    public KonoeHellfallBurial() : base(2, CardRarity.Uncommon, TargetType.AnyEnemy, KonoeElement.Fire) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(11, ValueProp.Move)];
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(4);
    protected override CardModel CreateStage() => ModelDb.Card<KonoeHellfallBurialStage>();
    protected override IEnumerable<IHoverTip> StageTips => [HoverTipFactory.FromCard<KonoeHellfallBurialStage>(), HoverTipFactory.FromPower<KonoeKnockdownPower>()];
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await Hit(context, play);
        if (play.Target is { IsAlive: true } target) await PowerCmd.Apply<KonoeKnockdownPower>(context, target, 1, Owner.Creature, this);
    }
}

[RegisterCard(typeof(KonoeCardPool))]
public sealed class KonoeHellfallBurialStage : KonoeComboStage
{
    public KonoeHellfallBurialStage() : base(1, CardType.Attack, TargetType.AnyEnemy, KonoeElement.Fire) { }
    protected override CardModel CreateOriginal() => ModelDb.Card<KonoeHellfallBurial>();
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(8, ValueProp.Move)];
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => Hit(context, play);
}

// ── 罕见：冰晶连奏 → 冰晶连奏·碎 ────────────────────────────────────
[RegisterCard(typeof(KonoeCardPool))]
public sealed class KonoeFrostCadenza : KonoeComboCard
{
    public KonoeFrostCadenza() : base(1, CardRarity.Uncommon, TargetType.AnyEnemy, KonoeElement.Water) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(8, ValueProp.Move), new DynamicVar("Freeze", 1)];
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
    protected override CardModel CreateStage() => ModelDb.Card<KonoeFrostCadenzaStage>();
    protected override IEnumerable<IHoverTip> StageTips => [HoverTipFactory.FromCard<KonoeFrostCadenzaStage>(), HoverTipFactory.FromPower<KonoeFrozenPower>()];
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await Cast(); await Hit(context, play);
        if (play.Target is { IsAlive: true } target) await PowerCmd.Apply<KonoeFrozenPower>(context, target, DynamicVars["Freeze"].IntValue, Owner.Creature, this);
    }
}

[RegisterCard(typeof(KonoeCardPool))]
public sealed class KonoeFrostCadenzaStage : KonoeComboStage
{
    public KonoeFrostCadenzaStage() : base(1, CardType.Attack, TargetType.AnyEnemy, KonoeElement.Water) { }
    protected override CardModel CreateOriginal() => ModelDb.Card<KonoeFrostCadenza>();
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(10, ValueProp.Move)];
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(4);
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => Hit(context, play);
}

// ── 罕见：新星狱葬 → 新星狱葬·终 ────────────────────────────────────
[RegisterCard(typeof(KonoeCardPool))]
public sealed class KonoeNovaBurial : KonoeComboCard
{
    public KonoeNovaBurial() : base(2, CardRarity.Uncommon, TargetType.AllEnemies, KonoeElement.Fire) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(10, ValueProp.Move)];
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(4);
    protected override CardModel CreateStage() => ModelDb.Card<KonoeNovaBurialStage>();
    protected override IEnumerable<IHoverTip> StageTips => [HoverTipFactory.FromCard<KonoeNovaBurialStage>()];
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play) { await Cast(); await Hit(context, play); }
}

[RegisterCard(typeof(KonoeCardPool))]
public sealed class KonoeNovaBurialStage : KonoeComboStage
{
    public KonoeNovaBurialStage() : base(1, CardType.Attack, TargetType.AllEnemies, KonoeElement.Fire) { }
    protected override CardModel CreateOriginal() => ModelDb.Card<KonoeNovaBurial>();
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(8, ValueProp.Move)];
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => Hit(context, play);
}
