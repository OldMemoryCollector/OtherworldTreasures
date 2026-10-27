using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Relics;

// Distinct from CrossNecklace, whose existing save ID belongs to the Ankh Charm.
[RegisterRelic(typeof(SharedRelicPool))]
public sealed class TerrariaCrossNecklace : ModRelicTemplate
{
    private sealed class AttackWindow(AttackCommand command)
    {
        internal readonly AttackCommand Command = command;
        internal bool FirstHitSeen;
        internal bool IsSingleHit;
        internal bool ReduceSingleHit;
    }
    private List<AttackWindow> _attacks = new();
    private bool _singleAttackUsed;
    public override RelicRarity Rarity => RelicRarity.Event;
    public override bool IsAllowedInShops => false;
    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://OtherworldTreasures/images/relics/TerrariaCrossNecklace_85.png",
        IconOutlinePath: "res://OtherworldTreasures/images/relics/TerrariaCrossNecklace_85.png",
        BigIconPath: "res://OtherworldTreasures/images/relics/TerrariaCrossNecklace.png");
    protected override void DeepCloneFields() { base.DeepCloneFields(); _attacks = new(); }
    public override Task BeforeCombatStart()
    {
        _attacks.Clear(); _singleAttackUsed = false;
        return Task.CompletedTask;
    }
    public override Task AfterPlayerTurnStartEarly(PlayerChoiceContext context, Player player)
    {
        if (player == Owner) _singleAttackUsed = false;
        return Task.CompletedTask;
    }
    public override Task BeforeAttack(AttackCommand command)
    {
        if (command.Attacker is { Side: CombatSide.Enemy }) _attacks.Add(new AttackWindow(command));
        return Task.CompletedTask;
    }
    internal void ObserveFinalHitCount(AttackCommand command, decimal hitCount)
    {
        var window = _attacks.LastOrDefault(entry => ReferenceEquals(entry.Command, command));
        if (window != null) window.IsSingleHit = hitCount > 0 && hitCount <= 1;
    }
    private AttackWindow? Current(Creature? dealer) => _attacks.LastOrDefault(window => window.Command.Attacker == dealer);
    public override decimal ModifyDamageCap(Creature? target, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay) =>
        target == Owner.Creature && props.HasFlag(ValueProp.Move) && Current(dealer)?.FirstHitSeen == true ? 0m : decimal.MaxValue;
    public override Task BeforeDamageReceived(PlayerChoiceContext context, Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? source)
    {
        if (target == Owner.Creature && props.HasFlag(ValueProp.Move) && Current(dealer) is { } window)
        {
            if (window.FirstHitSeen) Flash();
            else if (window.IsSingleHit && !_singleAttackUsed)
            {
                // Commit on the actual hit, including a fully blocked hit. Previews are read-only.
                _singleAttackUsed = true;
                window.ReduceSingleHit = true;
            }
            window.FirstHitSeen = true;
        }
        return Task.CompletedTask;
    }
    public override decimal ModifyHpLostBeforeOsty(Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource) =>
        target == Owner.Creature && props.HasFlag(ValueProp.Move) && Current(dealer)?.ReduceSingleHit == true
            ? Math.Max(0m, amount - 6m) : amount;
    public override Task AfterModifyingHpLostBeforeOsty() { Flash(); return Task.CompletedTask; }
    public override Task AfterAttack(PlayerChoiceContext context, AttackCommand command)
    {
        _attacks.RemoveAll(window => ReferenceEquals(window.Command, command));
        return Task.CompletedTask;
    }
    public override Task AfterCombatEnd(CombatRoom room)
    {
        _attacks.Clear(); _singleAttackUsed = false;
        return Task.CompletedTask;
    }
}

// Observe the final native count so a later hit-count modifier cannot turn a
// multi-hit attack into a falsely eligible single hit (or vice versa).
[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyAttackHitCount))]
internal static class TerrariaCrossNecklaceHitCount
{
    private static void Postfix(ICombatState combatState, AttackCommand attackCommand, decimal __result)
    {
        if (attackCommand.Attacker?.Side != CombatSide.Enemy) return;
        foreach (var relic in combatState.Players.SelectMany(player => player.Relics.OfType<TerrariaCrossNecklace>()))
            relic.ObserveFinalHitCount(attackCommand, __result);
    }
}
