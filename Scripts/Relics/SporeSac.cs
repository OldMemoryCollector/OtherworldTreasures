using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using OtherworldTreasures.Scripts.Powers;
using OtherworldTreasures.Scripts.UI;
using STS2RitsuLib.Combat.Ui.ExtraCornerAmountLabels;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Relics;

[RegisterRelic(typeof(SharedRelicPool))]
public sealed class SporeSac : ModRelicTemplate, IRelicExtraIconAmountLabelSpecsProvider, IRelicExtraIconAmountLabelsChangeSource
{
    private const int WinsPerBonus = 3;
    private int _poisonPerSpore = 1, _initialSpores = 1, _spores, _wins;
    private bool _nextBonusAddsSpores;

    // Preserve existing permanent poison and victory progress when loading old runs.
    // New saves also retain the stock of spores and the alternating growth phase.
    [SavedProperty] public int SavedPoisonStacks { get => _poisonPerSpore; set { AssertMutable(); _poisonPerSpore = Math.Max(1, value); } }
    [SavedProperty] public int SavedWinsTowardNext { get => _wins; set { AssertMutable(); _wins = Math.Max(0, value); } }
    [SavedProperty] public int SavedInitialSpores { get => _initialSpores; set { AssertMutable(); _initialSpores = Math.Max(1, value); } }
    [SavedProperty] public int SavedSporeCount { get => _spores; set { AssertMutable(); _spores = Math.Max(0, value); } }
    [SavedProperty] public bool SavedNextBonusAddsSpores { get => _nextBonusAddsSpores; set { AssertMutable(); _nextBonusAddsSpores = value; } }

    private SporeSac Live => IsMutable ? Owner.Relics.OfType<SporeSac>().FirstOrDefault() ?? this : this;
    internal int AvailableSpores => !IsMutable ? 1 : Live._spores;
    internal int PoisonPerSpore => Live._poisonPerSpore;
    public override RelicRarity Rarity => RelicRarity.Event;
    public override bool IsAllowedInShops => false;
    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://OtherworldTreasures/images/relics/Spore_Sac.png",
        IconOutlinePath: "res://OtherworldTreasures/images/relics/Spore_Sac.png",
        BigIconPath: "res://OtherworldTreasures/images/relics/Spore_Sac.png");
    public event Action? RelicExtraIconAmountLabelsInvalidated;
    private void RefreshBadge() => Live.RelicExtraIconAmountLabelsInvalidated?.Invoke();
    public IReadOnlyList<ExtraIconAmountLabelSpec> GetRelicExtraIconAmountLabelSpecs() =>
        !IsMutable || !(RunManager.Instance?.IsInProgress ?? false) ? [] :
        [
            ExtraIconAmountLabelSpec.RichText(ExtraIconAmountLabelCorner.TopLeft,
                $"[font_size=16]{Live._wins}/{WinsPerBonus}[/font_size]"),
            ExtraIconAmountLabelSpec.RichText(ExtraIconAmountLabelCorner.BottomRight,
                $"[font_size=16][color=#84d64b]{Live._initialSpores}×{Live._poisonPerSpore}[/color][/font_size]")
        ];
    public override Task AfterObtained()
    {
        _poisonPerSpore = _initialSpores = 1;
        _spores = _wins = 0;
        _nextBonusAddsSpores = false;
        RefreshBadge();
        return Task.CompletedTask;
    }
    // Legacy lifecycle call retained; state is carried by each saved relic.
    internal static void ResetCombatScopedState() { }
    public override async Task BeforeCombatStart()
    {
        var live = Live;
        live._spores = live._initialSpores;
        await PowerCmd.Apply<SporeSacPower>(new ThrowingPlayerChoiceContext(), Owner.Creature,
            live._spores, Owner.Creature, null);
        RefreshDisplayPower();
    }
    public override Task AfterPlayerTurnStart(PlayerChoiceContext context, Player player)
    {
        if (player == Owner)
        {
            // The first player turn is also a turn start: the initial spore and
            // these two spores are separate grants, so a fresh combat opens at 3.
            Live._spores += 2;
            RefreshDisplayPower();
        }
        return Task.CompletedTask;
    }
    private void RefreshDisplayPower() => Owner.Creature.GetPower<SporeSacPower>()?.SetAmount(AvailableSpores, silent: true);
    public override async Task AfterAttack(PlayerChoiceContext context, AttackCommand command)
    {
        var live = Live;
        var attacker = command.Attacker;
        if (live._spores <= 0 || Owner.Creature.IsDead ||
            attacker is not { Side: CombatSide.Enemy, IsDead: false } ||
            !command.Results.SelectMany(hit => hit).Any(result => result.Receiver == Owner.Creature)) return;
        int spores = live._spores;
        int poison = spores * live._poisonPerSpore;
        // Commit consumption first so nested effects cannot release the same stock.
        live._spores = 0;
        RefreshDisplayPower();
        Flash();
        TerrariaRelicVfx.ReleaseSpores(Owner.Creature, spores);
        await PowerCmd.Apply<PoisonPower>(context, attacker, poison, Owner.Creature, null);
    }
    public override Task AfterCombatVictory(CombatRoom room)
    {
        var live = Live;
        if (++live._wins >= WinsPerBonus)
        {
            live._wins = 0;
            if (live._nextBonusAddsSpores) live._initialSpores++;
            else live._poisonPerSpore++;
            live._nextBonusAddsSpores = !live._nextBonusAddsSpores;
        }
        live._spores = 0;
        Flash();
        RefreshBadge();
        return Task.CompletedTask;
    }
    public override async Task AfterCombatEnd(CombatRoom room)
    {
        Live._spores = 0;
        if (Owner.Creature.GetPower<SporeSacPower>() is { } power) await PowerCmd.Remove(power);
    }
    public override Task AfterRemoved() => Owner.Creature.GetPower<SporeSacPower>() is { } power ? PowerCmd.Remove(power) : Task.CompletedTask;
}
