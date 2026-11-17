using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Konoe;

[RegisterPower]
public sealed class KonoeComboPreparationPower : ModPowerTemplate
{
    private CardPlay? _consumingPlay;
    private int _spentAmount;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerAssetProfile AssetProfile => KonoePowerArt.For("guard");
    public override Task BeforeCardPlayed(CardPlay play)
    {
        if (_consumingPlay == null && play.Player == Owner.Player && play.Card.Type == CardType.Attack)
        {
            _consumingPlay = play;
            _spentAmount = Amount;
        }
        return Task.CompletedTask;
    }
    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? source, CardPlay? play)
    {
        if (dealer != Owner || source?.Type != CardType.Attack || !props.IsPoweredAttack()) return 0;
        if (_consumingPlay == null) return Amount;
        return ReferenceEquals(play, _consumingPlay) ? _spentAmount : 0;
    }
    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
    {
        if (!ReferenceEquals(play, _consumingPlay)) return;
        int spent = _spentAmount;
        _consumingPlay = null;
        _spentAmount = 0;
        await PowerCmd.ModifyAmount(context, this, -spent, null, null);
    }
}

[RegisterPower]
public sealed class KonoeGuardCounterPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerAssetProfile AssetProfile => KonoePowerArt.For("counter");
    public override async Task AfterDamageReceived(PlayerChoiceContext context, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? source)
    {
        // A fully blocked attack still qualifies; HP loss is deliberately not required.
        if (target != Owner || dealer is not { IsAlive: true } attacker || attacker.Side == Owner.Side || !props.IsPoweredAttack()) return;
        int damage = Amount;
        await PowerCmd.Remove(this);
        if (attacker.IsAlive) await CreatureCmd.Damage(context, attacker, damage, ValueProp.Unpowered, Owner);
    }
    public override async Task AfterSideTurnEnd(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != Owner.Side) await PowerCmd.Remove(this);
    }
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext context, Player player)
    {
        if (player == Owner.Player) await PowerCmd.Remove(this);
    }
}
