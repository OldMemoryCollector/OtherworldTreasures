using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Models.Capabilities;

namespace OtherworldTreasures.Scripts.Konoe;

public abstract class KonoeCommonCard : ModCardTemplate
{
    protected KonoeCommonCard(int cost, CardType type, TargetType target) : base(cost, type, CardRarity.Common, target, DevelopmentContent.Nine) { }
    public override bool CanBeGeneratedInCombat => true;
    public override bool CanBeGeneratedByModifiers => DevelopmentContent.Nine;
    public override CardAssetProfile AssetProfile => KonoeCardArt.Profile;
    protected Task GainBlock(CardPlay play) => CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);
    protected Task Draw(PlayerChoiceContext context) => CardPileCmd.Draw(context, DynamicVars.Cards.IntValue, Owner);
    protected Task Cast() => CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);
    protected static IHoverTip HeatTip => new HoverTip(new LocString("static_hover_tips", "OTHERWORLD_TREASURES_SECONDARY_RESOURCE_KONOE_HEAT.title"), new LocString("static_hover_tips", "OTHERWORLD_TREASURES_SECONDARY_RESOURCE_KONOE_HEAT.description"));
    protected static IHoverTip LinkageTip => new HoverTip(new LocString("static_hover_tips", "KONOE_LINKAGE.title"), new LocString("static_hover_tips", "KONOE_LINKAGE.description"));
    protected static IHoverTip StorageTip => new HoverTip(new LocString("static_hover_tips", "KONOE_STORAGE.title"), new LocString("static_hover_tips", "KONOE_STORAGE.description"));
}

public abstract class KonoeCommonAttack : KonoeCommonCard, IKonoeElementSource
{
    private readonly KonoeElement _element;
    protected KonoeCommonAttack(KonoeElement element = default, bool all = false) : base(1, CardType.Attack, all ? TargetType.AllEnemies : TargetType.AnyEnemy) => _element = element;
    public IReadOnlyList<KonoeElement> GrantedElements => _element == default ? [] : [_element];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => _element == default ? [] : KonoeSpellTips.Elements(KonoeDamage.FromElement(_element));
    protected async Task Hit(PlayerChoiceContext context, CardPlay play, decimal? amount = null, bool cast = false)
    {
        if (CombatState is not { } combat) return;
        if (TargetType == TargetType.AllEnemies ? !combat.HittableEnemies.Any() : play.Target is not { IsAlive: true }) return;
        var attack = DamageCmd.Attack(amount ?? DynamicVars.Damage.BaseValue).FromCard(this, play);
        if (TargetType == TargetType.AllEnemies) attack.TargetingAllOpponents(combat); else attack.Targeting(play.Target!);
        if (cast) attack.WithAttackerAnim("Cast", Owner.Character.CastAnimDelay);
        await attack.WithHitFx("vfx/vfx_attack_blunt").Execute(context);
    }
    protected async Task KnockDown(PlayerChoiceContext context, CardPlay play)
    {
        if (play.Target is not { IsAlive: true } target) return;
        await PowerCmd.Apply<KonoeKnockdownPower>(context, target, 1, Owner.Creature, this);
    }
}

[RegisterCard(typeof(KonoeCardPool))]
public sealed class KonoeFireAttack : KonoeCommonAttack
{
    public KonoeFireAttack() : base(KonoeElement.Fire) { }
    // Fire carries burn damage on top of the hit: 6 damage = 2 burn (3 more), 9 total.
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6, ValueProp.Move)];
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => Hit(context, play);
}

[RegisterCard(typeof(KonoeCardPool))]
public sealed class KonoeWaterAttack : KonoeCommonAttack
{
    public KonoeWaterAttack() : base(KonoeElement.Water) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(8, ValueProp.Move)];
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => Hit(context, play);
}

[RegisterCard(typeof(KonoeCardPool))]
public sealed class KonoeWindAttack : KonoeCommonAttack
{
    public KonoeWindAttack() : base(KonoeElement.Wind) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(8, ValueProp.Move)];
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => Hit(context, play);
}

[RegisterCard(typeof(KonoeCardPool))]
public sealed class KonoeBackhandIntercept : KonoeCommonAttack
{
    public KonoeBackhandIntercept() : base(KonoeElement.Wind) { }
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(5, ValueProp.Move), new BlockVar(5, ValueProp.Move)];
    protected override void OnUpgrade() { DynamicVars.Damage.UpgradeValueBy(2); DynamicVars.Block.UpgradeValueBy(2); }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play) { await Hit(context, play); await GainBlock(play); }
}

[RegisterCard(typeof(KonoeCardPool))]
public sealed class KonoeCrouchingLight : KonoeCommonAttack
{
    public KonoeCrouchingLight() : base(KonoeElement.Water) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(8, ValueProp.Move)];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => base.AdditionalHoverTips.Append(LinkageTip);
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await Hit(context, play);
        await KonoeLinkage.Draw(context, Owner, KonoeStance.IsCrouching);
    }
}

[RegisterCard(typeof(KonoeCardPool))]
public sealed class KonoeCrouchingMedium : KonoeCommonAttack
{
    public KonoeCrouchingMedium() : base(KonoeElement.Wind) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(8, ValueProp.Move)];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => base.AdditionalHoverTips.Append(LinkageTip);
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await Hit(context, play);
        await KonoeLinkage.Draw(context, Owner, KonoeStance.IsAntiAir);
    }
}

[RegisterCard(typeof(KonoeCardPool))]
public sealed class KonoeLowSweep : KonoeCommonAttack
{
    public KonoeLowSweep() : base(KonoeElement.Fire) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6, ValueProp.Move)];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => base.AdditionalHoverTips.Append(HoverTipFactory.FromPower<KonoeKnockdownPower>());
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play) { await Hit(context, play); await KnockDown(context, play); }
}

[RegisterCard(typeof(KonoeCardPool))]
public sealed class KonoeRisingStrike : KonoeCommonAttack
{
    public KonoeRisingStrike() : base(KonoeElement.Water) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(8, ValueProp.Move)];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => base.AdditionalHoverTips.Append(HoverTipFactory.FromPower<KonoeAirbornePower>());
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await Hit(context, play);
        if (play.Target is { IsAlive: true } target) await PowerCmd.Apply<KonoeAirbornePower>(context, target, 1, Owner.Creature, this);
    }
}

[RegisterCard(typeof(KonoeCardPool))]
public sealed class KonoeAntiAirHeavy : KonoeCommonAttack
{
    public KonoeAntiAirHeavy() : base(KonoeElement.Fire) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6, ValueProp.Move), new DynamicVar("BonusDamage", 5)];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => base.AdditionalHoverTips.Append(HoverTipFactory.FromPower<KonoeAirbornePower>());
    protected override void OnUpgrade() { DynamicVars.Damage.UpgradeValueBy(3); DynamicVars["BonusDamage"].UpgradeValueBy(2); }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        bool airborne = play.Target?.HasPower<KonoeAirbornePower>() == true;
        await Hit(context, play);
        if (airborne) await Hit(context, play, DynamicVars["BonusDamage"].BaseValue);
    }
}

[RegisterCard(typeof(KonoeCardPool))]
public sealed class KonoeAerialCombo : KonoeCommonAttack
{
    public KonoeAerialCombo() : base(KonoeElement.Wind) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(8, ValueProp.Move), new CardsVar(1)];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => base.AdditionalHoverTips.Append(HoverTipFactory.FromPower<KonoeAirbornePower>());
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        bool airborne = play.Target?.HasPower<KonoeAirbornePower>() == true;
        await Hit(context, play);
        if (airborne) await Draw(context);
    }
}

[RegisterCard(typeof(KonoeCardPool))]
public sealed class KonoeAerialPress : KonoeCommonAttack
{
    public KonoeAerialPress() : base(KonoeElement.Fire, all: true) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6, ValueProp.Move)];
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => Hit(context, play);
}

[RegisterCard(typeof(KonoeCardPool))]
public sealed class KonoeComboAdjustment : KonoeCommonCard
{
    public KonoeComboAdjustment() : base(0, CardType.Skill, TargetType.Self) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1)];
    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1);
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await Cast();
        await Draw(context);
        await CardCmd.Discard(context, await CardSelectCmd.FromHandForDiscard(context, Owner, new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt, DynamicVars.Cards.IntValue), null, this));
    }
}

[RegisterCard(typeof(KonoeCardPool))]
public sealed class KonoeTeleport : KonoeCommonCard
{
    public KonoeTeleport() : base(1, CardType.Skill, TargetType.Self) { }
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(5, ValueProp.Move), new CardsVar(2)];
    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3);
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play) { await Cast(); await GainBlock(play); await Draw(context); }
}

// 应势：这张牌留在手牌中，敌人攻击你之前自动打出一次，不消耗能量。
// 关键词只解释通用机制（保留在手牌、条件满足自动打出、不耗能量），
// 具体触发条件与数值由卡面正文自己写。
// 手动打出给 Block（5，升级 +2）；应势自动打出只给 PoiseBlock（3，升级 +2）。
[RegisterCard(typeof(KonoeCardPool))]
public sealed class KonoePoise : KonoeCommonCard
{
    public KonoePoise() : base(1, CardType.Skill, TargetType.Self) { }
    public override bool GainsBlock => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [MyKeywords.Poise];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(5, ValueProp.Move), new DynamicVar("PoiseBlock", 3)];
    protected override void OnUpgrade() { DynamicVars.Block.UpgradeValueBy(2); DynamicVars["PoiseBlock"].UpgradeValueBy(2); }

    // 应势自带「临时保留」：进手时施加单回合保留，跨过敌方回合后在下个回合弃置。
    // 不使用原生 Retain 关键词（永久保留）。
    public override Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? clonedBy)
    {
        if (ReferenceEquals(card, this) && Pile?.Type == PileType.Hand) CardCmd.ApplySingleTurnRetain(this);
        return Task.CompletedTask;
    }

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await Cast();
        if (play.IsAutoPlay)
        {
            int poise = DynamicVars["PoiseBlock"].IntValue;
            if (poise > 0) await CreatureCmd.GainBlock(Owner.Creature, poise, ValueProp.Move, play);
            return;
        }
        await GainBlock(play);
    }

    // 手牌里的卡牌本身就是 combat hook listener（CombatState.IterateHookListeners 会遍历所有牌堆），
    // 所以可以直接监听「敌人攻击你之前」。自动打出后这张牌离开手牌，因此只会触发一次。
    public override async Task BeforeAttack(AttackCommand command)
    {
        if (Pile?.Type != PileType.Hand) return;
        if (Owner?.Creature is not { IsAlive: true } me) return;
        if (command.Attacker is not { IsAlive: true } attacker || attacker.Side == me.Side) return;
        if (command.TargetSide != me.Side) return;
        if (CombatState is null || CombatManager.Instance.IsOverOrEnding) return;
        await CardCmd.AutoPlay(new BlockingPlayerChoiceContext(), this, null, AutoPlayType.Default);
    }
}

[RegisterCard(typeof(KonoeCardPool))]
public sealed class KonoeGuardedChant : KonoeCommonCard
{
    public KonoeGuardedChant() : base(1, CardType.Skill, TargetType.Self) { }
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(6, ValueProp.Move)];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromKeyword(CardKeyword.Retain)];
    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3);
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await Cast(); await GainBlock(play);
        var selected = (await CardSelectCmd.FromHand(context, Owner, new CardSelectorPrefs(SelectionScreenPrompt, 1), c => !c.Keywords.Contains(CardKeyword.Retain), this)).FirstOrDefault();
        if (selected != null) CardCmd.ApplySingleTurnRetain(selected);
    }
}

[RegisterCard(typeof(KonoeCardPool))]
public sealed class KonoeComboPreparation : KonoeCommonCard
{
    public KonoeComboPreparation() : base(1, CardType.Skill, TargetType.Self) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(2), new DynamicVar("BonusDamage", 3)];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<KonoeComboPreparationPower>(DynamicVars["BonusDamage"].IntValue)];
    protected override void OnUpgrade() => DynamicVars["BonusDamage"].UpgradeValueBy(2);
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await Cast(); await Draw(context);
        await PowerCmd.Apply<KonoeComboPreparationPower>(context, Owner.Creature, DynamicVars["BonusDamage"].BaseValue, Owner.Creature, this);
    }
}

[RegisterCard(typeof(KonoeCardPool))]
public sealed class KonoeGuardCounter : KonoeCommonCard
{
    public KonoeGuardCounter() : base(1, CardType.Skill, TargetType.Self) { }
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(6, ValueProp.Move), new DynamicVar("DamageBack", 5)];
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [HoverTipFactory.FromPower<KonoeGuardCounterPower>(DynamicVars["DamageBack"].IntValue)];
    protected override void OnUpgrade() { DynamicVars.Block.UpgradeValueBy(2); DynamicVars["DamageBack"].UpgradeValueBy(2); }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        await Cast(); await GainBlock(play);
        await PowerCmd.Apply<KonoeGuardCounterPower>(context, Owner.Creature, DynamicVars["DamageBack"].BaseValue, Owner.Creature, this);
    }
}

// 术式调律：换掉当前元素组合与储存组合，让已布好的术式立刻可用。
[RegisterCard(typeof(KonoeCardPool))]
public sealed class KonoeFormulaTuning : KonoeCommonCard
{
    public KonoeFormulaTuning() : base(1, CardType.Skill, TargetType.Self) { }
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [StorageTip];
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var state = await KonoeElements.EnsureState(context, Owner);
        if (state == null) return;
        await Cast();
        var slot = Owner.Creature.GetPower<KonoeStoragePower>();
        if (slot == null) return;
        (state.PackedElements, slot.PackedElements) = (slot.PackedElements, state.PackedElements);
        KonoeElements.Refresh(Owner);
    }
}
