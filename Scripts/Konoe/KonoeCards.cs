using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.CardSelection;
using STS2RitsuLib.Models.Capabilities;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Konoe;

[RegisterCard(typeof(KonoeCardPool))]
public sealed class KonoeRandomElement : ModCardTemplate
{
    public KonoeRandomElement() : base(0, CardType.Skill, CardRarity.Basic, TargetType.Self, DevelopmentContent.Nine) { }
    public override bool CanBeGeneratedInCombat => false;
    public override bool CanBeGeneratedByModifiers => false;
    public override CardAssetProfile AssetProfile => KonoeCardArt.Profile;
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        [new HoverTip(new LocString("static_hover_tips", IsUpgraded ? "KONOE_PERSISTENT_INFUSION.title" : "KONOE_TEMPORARY_INFUSION.title"),
                      new LocString("static_hover_tips", IsUpgraded ? "KONOE_PERSISTENT_INFUSION.description" : "KONOE_TEMPORARY_INFUSION.description"))];
    protected override void OnUpgrade() { }
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        var selected = (await CardSelectCmd.FromHand(context, Owner, new CardSelectorPrefs(SelectionScreenPrompt, 1),
            KonoeDamage.CanReceiveInfusion, this)).FirstOrDefault();
        if (selected == null) return;
        await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);
        var element = KonoeBlueSupport.Random(Owner);
        KonoeDamage.TryInfuse(selected, element, temporary: !IsUpgraded);
        KonoeElements.Refresh(Owner);
    }
}

internal static class KonoeCardArt
{
    internal static readonly CardAssetProfile Profile = new(PortraitPath: "res://OtherworldTreasures/images/characters/nine/card_placeholder.jpg");
}

[RegisterCard(typeof(KonoeCardPool))]
[RegisterArchaicToothTranscendence(typeof(KonoeAbyssalFormula))]
public sealed class KonoeBlackStone : ModCardTemplate
{
    private KonoeSpellKind? _resolving;
    private KonoeSpellKind _series;
    internal bool LastPlayWasCombination => _series != KonoeSpellKind.BlackStone;
    public KonoeBlackStone() : base(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy, DevelopmentContent.Nine) { }
    public override bool CanBeGeneratedInCombat => false;
    public override bool CanBeGeneratedByModifiers => false;
    public override CardAssetProfile AssetProfile => KonoeCardArt.Profile;
    public KonoeSpellKind CurrentSpell => _resolving ?? KonoeElementRules.Resolve(IsMutable ? KonoeElements.Get(Owner) : 0);
    public KonoeSpellValues Values => KonoeSpellValues.For(CurrentSpell, IsUpgraded);
    public override TargetType TargetType => KonoeSpellValues.NeedsTarget(CurrentSpell) ? TargetType.AnyEnemy : KonoeSpellValues.TargetsAll(CurrentSpell) ? TargetType.AllEnemies : TargetType.Self;
    public override bool GainsBlock => Values.Block > 0;
    public override string Title => new LocString("cards", SpellKey(CurrentSpell) + ".title").GetFormattedText() + (IsUpgraded ? "+" : "");
    internal static string SpellKey(KonoeSpellKind spell) => "KONOE_SPELL_" + spell.ToString().ToUpperInvariant();
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(6, ValueProp.Move), new BlockVar(0, ValueProp.Move),
        new DynamicVar("Hits", 1), new DynamicVar("Stacks", 0), new DynamicVar("Bonus", 0), new DynamicVar("Turns", 0), new DynamicVar("Followup", 0)
    ];
    public override void AfterCreated() { base.AfterCreated(); RefreshForm(); }
    protected override void OnUpgrade()
    {
        var before = KonoeSpellValues.For(CurrentSpell, false);
        RefreshForm();
        var after = Values;
        foreach (var (key, oldValue, newValue) in new[] {
            ("Damage", before.Damage, after.Damage), ("Block", before.Block, after.Block),
            ("Stacks", before.Stacks, after.Stacks), ("Bonus", before.Bonus, after.Bonus),
            ("Followup", before.Followup, after.Followup) })
            if (newValue > oldValue) DynamicVars[key].UpgradeValueBy(0);
    }
    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _resolving = null;
        _series = KonoeSpellKind.BlackStone;
    }
    internal void RefreshForm()
    {
        if (!IsMutable) return;
        var v = Values;
        DynamicVars.Damage.Props = KonoeDamage.WithTypes(KonoeSpellValues.DamageProps(CurrentSpell), KonoeDamage.ForSpell(CurrentSpell) | KonoeDamage.AttachedTypes(this));
        DynamicVars.Damage.BaseValue = v.Damage;
        DynamicVars.Block.BaseValue = v.Block;
        DynamicVars["Hits"].BaseValue = v.Hits;
        DynamicVars["Stacks"].BaseValue = v.Stacks;
        DynamicVars["Bonus"].BaseValue = v.Bonus;
        DynamicVars["Turns"].BaseValue = v.Turns;
        DynamicVars["Followup"].BaseValue = v.Followup;
    }
    protected override void AddExtraArgsToDescription(LocString description)
    {
        var text = new LocString("cards", SpellKey(CurrentSpell) + ".description");
        DynamicVars.AddTo(text);
        description.Add("SpellText", text);
    }
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => KonoeSpellTips.For(CurrentSpell, DynamicVars["Stacks"].IntValue);
    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (play.IsFirstInSeries) _series = KonoeElementRules.Resolve(KonoeElements.Get(Owner));
        _resolving = _series;
        RefreshForm();
        bool finished = false;
        try
        {
            await KonoeSpellCasting.Release(context, this, play, _series, play.Target, play.IsFirstInSeries, IsUpgraded);
            finished = true;
        }
        finally
        {
            if (play.IsLastInSeries || !finished) _resolving = null;
            RefreshForm(); KonoeElements.Refresh(Owner);
        }
    }
}
