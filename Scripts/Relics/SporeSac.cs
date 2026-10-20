using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
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
    private int _basePoison = 1, _wins, _temporaryPoison, _combatTurn;
    private bool _spent;
    // Existing save names remain intact. Combat charge now also survives saving.
    [SavedProperty] public int SavedPoisonStacks { get => _basePoison; set { AssertMutable(); _basePoison = Math.Max(1, value); } }
    [SavedProperty] public int SavedWinsTowardNext { get => _wins; set { AssertMutable(); _wins = Math.Max(0, value); } }
    [SavedProperty] public int SavedTemporaryPoison { get => _temporaryPoison; set { AssertMutable(); _temporaryPoison = Math.Max(0, value); } }
    [SavedProperty] public int SavedCombatTurn { get => _combatTurn; set { AssertMutable(); _combatTurn = Math.Max(0, value); } }
    [SavedProperty] public bool SavedSpentThisTurn { get => _spent; set { AssertMutable(); _spent = value; } }
    private SporeSac Live => IsMutable ? Owner.Relics.OfType<SporeSac>().FirstOrDefault() ?? this : this;
    internal int AvailablePoison => !IsMutable ? 1 : Live._spent ? 0 : Live._basePoison + Live._temporaryPoison;
    public override RelicRarity Rarity => RelicRarity.Ancient;
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
                $"[font_size=16][color=#84d64b]{Live._basePoison}[/color][/font_size]")
        ];
    public override Task AfterObtained()
    {
        _basePoison = 1; _wins = _temporaryPoison = _combatTurn = 0; _spent = false;
        RefreshBadge();
        return Task.CompletedTask;
    }
    // Legacy lifecycle call retained; state is now carried by the saved relic itself.
    internal static void ResetCombatScopedState() { }
    public override async Task BeforeCombatStart()
    {
        var live = Live; live._temporaryPoison = live._combatTurn = 0; live._spent = false;
        await PowerCmd.Apply<SporeSacPower>(new ThrowingPlayerChoiceContext(), Owner.Creature, live._basePoison, Owner.Creature, null);
        RefreshDisplayPower();
    }
    public override Task AfterPlayerTurnStart(PlayerChoiceContext context, Player player)
    {
        if (player == Owner)
        {
            var live = Live;
            live._combatTurn++;
            if (live._combatTurn > 1) live._temporaryPoison += 2;
            live._spent = false;
            RefreshDisplayPower();
        }
        return Task.CompletedTask;
    }
    private void RefreshDisplayPower() => Owner.Creature.GetPower<SporeSacPower>()?.SetAmount(AvailablePoison, silent: true);
    public override async Task AfterAttack(PlayerChoiceContext context, AttackCommand command)
    {
        var live = Live;
        var attacker = command.Attacker;
        if (live._spent || Owner.Creature.IsDead || attacker is not { Side: MegaCrit.Sts2.Core.Combat.CombatSide.Enemy, IsDead: false } ||
            !command.Results.SelectMany(hit => hit).Any(result => result.Receiver == Owner.Creature)) return;
        int amount = AvailablePoison;
        // Commit the spent state before applying Poison; nested effects cannot fire again.
        live._spent = true; live._temporaryPoison = 0;
        RefreshDisplayPower(); Flash();
        TerrariaRelicVfx.ReleaseSpores(Owner.Creature, amount);
        await PowerCmd.Apply<PoisonPower>(context, attacker, amount, Owner.Creature, null);
    }
    public override Task AfterCombatVictory(CombatRoom room)
    {
        var live = Live;
        if (++live._wins >= WinsPerBonus) { live._wins = 0; live._basePoison++; }
        live._temporaryPoison = live._combatTurn = 0; live._spent = false;
        Flash(); RefreshBadge();
        return Task.CompletedTask;
    }
    public override async Task AfterCombatEnd(CombatRoom room)
    {
        var live = Live; live._temporaryPoison = live._combatTurn = 0; live._spent = false;
        if (Owner.Creature.GetPower<SporeSacPower>() is { } power) await PowerCmd.Remove(power);
    }
    public override Task AfterRemoved() => Owner.Creature.GetPower<SporeSacPower>() is { } power ? PowerCmd.Remove(power) : Task.CompletedTask;
}
