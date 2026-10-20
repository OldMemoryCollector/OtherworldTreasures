using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Relics;

// A distinct relic from CrossNecklace (the existing Ankh Charm save ID).
[RegisterRelic(typeof(SharedRelicPool))]
public sealed class TerrariaCrossNecklace : ModRelicTemplate
{
    private sealed class AttackWindow(AttackCommand command)
    {
        internal readonly AttackCommand Command = command;
        internal bool FirstHitSeen;
    }
    private List<AttackWindow> _attacks = new();
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://OtherworldTreasures/images/relics/TerrariaCrossNecklace_85.png",
        IconOutlinePath: "res://OtherworldTreasures/images/relics/TerrariaCrossNecklace_85.png",
        BigIconPath: "res://OtherworldTreasures/images/relics/TerrariaCrossNecklace.png");
    protected override void DeepCloneFields() { base.DeepCloneFields(); _attacks = new(); }
    public override Task BeforeCombatStart() { _attacks.Clear(); return Task.CompletedTask; }
    public override Task BeforeAttack(AttackCommand command)
    {
        if (command.Attacker is { Side: CombatSide.Enemy }) _attacks.Add(new AttackWindow(command));
        return Task.CompletedTask;
    }
    private AttackWindow? Current(Creature? dealer) => _attacks.LastOrDefault(window => window.Command.Attacker == dealer);
    public override decimal ModifyDamageCap(Creature? target, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay) =>
        target == Owner.Creature && props.HasFlag(ValueProp.Move) && Current(dealer)?.FirstHitSeen == true ? 0m : decimal.MaxValue;
    public override Task BeforeDamageReceived(PlayerChoiceContext context, Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? source)
    {
        if (target == Owner.Creature && props.HasFlag(ValueProp.Move) && Current(dealer) is { } window)
        {
            if (window.FirstHitSeen) Flash();
            window.FirstHitSeen = true;
        }
        return Task.CompletedTask;
    }
    public override Task AfterAttack(PlayerChoiceContext context, AttackCommand command)
    {
        _attacks.RemoveAll(window => ReferenceEquals(window.Command, command));
        return Task.CompletedTask;
    }
    public override Task AfterCombatEnd(CombatRoom room) { _attacks.Clear(); return Task.CompletedTask; }
}
