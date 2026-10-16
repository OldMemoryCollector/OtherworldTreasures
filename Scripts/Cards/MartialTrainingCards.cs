using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.ValueProps;
using OtherworldTreasures.Scripts.Events;
using OtherworldTreasures.Scripts.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Cards;

public abstract class MartialTrainingCard : ModCardTemplate, IDerivedCard
{
    protected MartialTrainingCard(int cost, CardType type, CardRarity rarity, TargetType target) : base(cost, type, rarity, target, true) { }
    public abstract int UseLimit { get; }
    protected abstract string ArtName { get; }
    public override int MaxUpgradeLevel => 0;
    protected override bool IsPlayable => !IsMutable || Owner == null || TrainingProgress.Remaining(Owner, this, UseLimit) > 0;
    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType) =>
        card != this || TrainingProgress.Remaining(Owner, this, UseLimit) > 0;
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://OtherworldTreasures/images/cards/training/{ArtName}.png",
        VisualStyle: Rarity == CardRarity.Ancient ? CardVisualStyle.Ancient : CardVisualStyle.Default);
    public override IEnumerable<CardKeyword> CanonicalKeywords => [MyKeywords.Remove];
    protected override CardLocation GetResultLocationForCardPlay() => new(Owner, Entry.ExclusionPile, CardPilePosition.Top);
    protected override IEnumerable<IHoverTip> AdditionalHoverTips
    {
        get
        {
            var description = new LocString("cards", "OTHERWORLD_TREASURES_TRAINING_USES.description");
            description.Add("Remaining", IsMutable && Owner != null ? TrainingProgress.Remaining(Owner, this, UseLimit) : UseLimit);
            return base.AdditionalHoverTips.Append(new HoverTip(new LocString("cards", "OTHERWORLD_TREASURES_TRAINING_USES.title"), description));
        }
    }

    protected sealed override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        if (!TrainingProgress.Spend(Owner, this, UseLimit)) return;
        // Remove permanent originals before effect resolution. Combat instances finish their
        // current play normally; copies and upgrade tiers all consult the same saved counter.
        if (TrainingProgress.Remaining(Owner, this, UseLimit) == 0)
            foreach (var original in Owner.Deck.Cards.Where(c => c.Id == Id).ToList())
                await CardPileCmd.RemoveFromDeck(original, showPreview: false);
        await Perform(context, play);
    }
    protected abstract Task Perform(PlayerChoiceContext context, CardPlay play);
}

[RegisterCard(typeof(TokenCardPool))]
public sealed class SolarFlare : MartialTrainingCard
{
    public SolarFlare() : base(1, CardType.Skill, CardRarity.Common, TargetType.AllEnemies) { }
    public override int UseLimit => 2;
    protected override string ArtName => "solar_flare";
    protected override async Task Perform(PlayerChoiceContext context, CardPlay play)
    {
        var enemies = CombatState!.Enemies.Where(e => e.IsAlive).ToList();
        // 先闪光演出再结算眩晕；等待在 Instant 模式下被跳过，眩晕仍只结算一次。
        await UI.MartialArtsVfx.SolarFlare(Owner, enemies);
        foreach (var enemy in enemies) await CreatureCmd.Stun(enemy);
    }
}

[RegisterCard(typeof(TokenCardPool))]
public sealed class SpecialBeamCannon : MartialTrainingCard
{
    public SpecialBeamCannon() : base(0, CardType.Attack, CardRarity.Common, TargetType.AllEnemies) { }
    public override int UseLimit => 3;
    protected override string ArtName => "special_beam_cannon";
    protected override bool HasEnergyCostX => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("DamagePerEnergy", 10)];
    protected override async Task Perform(PlayerChoiceContext context, CardPlay play)
    {
        // 蓄力与光束只服务演出；伤害仍由原命令按现有目标序列结算，不额外创建伤害。
        await UI.MartialArtsVfx.SpecialBeamCannon(Owner, CombatState!.Enemies.Where(e => e.IsAlive).ToList());
        await DamageCmd.Attack(DynamicVars["DamagePerEnergy"].BaseValue * ResolveEnergyXValue()).FromCard(this, play)
            .TargetingAllOpponents(CombatState!).Execute(context);
    }
}

[RegisterCard(typeof(TokenCardPool))]
public sealed class Kaioken : MartialTrainingCard
{
    public Kaioken() : base(0, CardType.Skill, CardRarity.Ancient, TargetType.Self) { }
    public override int UseLimit => 3;
    public override int MaxUpgradeLevel => 2;
    protected override string ArtName => "kaioken";
    public override string Title => new LocString("cards", $"{Id.Entry}.title{CurrentUpgradeLevel}").GetFormattedText();
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("HpCost", 6), new DynamicVar("Bonus", 100)];
    protected override void OnUpgrade()
    {
        DynamicVars["HpCost"].BaseValue = CurrentUpgradeLevel == 1 ? 36 : 72;
        DynamicVars["Bonus"].BaseValue = CurrentUpgradeLevel == 1 ? 1000 : 10000;
    }
    protected override async Task Perform(PlayerChoiceContext context, CardPlay play)
    {
        int cost = DynamicVars["HpCost"].IntValue;
        bool lethal = Owner.Creature.CurrentHp + TemporaryHealthPower.Available(Owner.Creature) <= cost;
        int normalCost = cost - TemporaryHealthPower.Spend(Owner.Creature, cost);
        await CreatureCmd.SetCurrentHp(Owner.Creature, Math.Max(1, Owner.Creature.CurrentHp - normalCost));
        await PowerCmd.Apply<KaiokenPower>(context, Owner.Creature, DynamicVars["Bonus"].BaseValue, Owner.Creature, this);
        if (lethal) await PowerCmd.Apply<KaiokenDoomPower>(context, Owner.Creature, 1, Owner.Creature, this);
    }
}

[RegisterCard(typeof(TokenCardPool))]
public sealed class SpiritBomb : MartialTrainingCard
{
    public SpiritBomb() : base(0, CardType.Skill, CardRarity.Ancient, TargetType.AllEnemies) { }
    public override int UseLimit => 1;
    protected override string ArtName => "spirit_bomb";
    public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;
    protected override Task Perform(PlayerChoiceContext context, CardPlay play) => SpiritBombPower.Begin(context, Owner, this);
}

[RegisterCard(typeof(TokenCardPool))]
public sealed class DonateEnergy : ModCardTemplate, IDerivedCard
{
    public DonateEnergy() : base(0, CardType.Skill, CardRarity.Token, TargetType.Self, true) { }
    public override CardAssetProfile AssetProfile => new(PortraitPath: "res://OtherworldTreasures/images/placeholders/beta.png");
    [MegaCrit.Sts2.Core.Saves.Runs.SavedProperty]
    public string CasterKey { get; set; } = "0";
    public ulong CasterId
    {
        get => ulong.TryParse(CasterKey, out var value) ? value : 0;
        set => CasterKey = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
    [MegaCrit.Sts2.Core.Saves.Runs.SavedProperty]
    public int BombId { get; set; }
    protected override bool HasEnergyCostX => true;
    public override int MaxUpgradeLevel => 0;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [MyKeywords.Remove];
    protected override CardLocation GetResultLocationForCardPlay() => new(Owner, PileType.None, CardPilePosition.Top);
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => SpiritBombPower.Contribute(
        context, Owner, CasterId, BombId, play.Resources.EnergySpent);
}
