using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using OtherworldTreasures.Scripts.Alien;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;

namespace OtherworldTreasures.Scripts.Powers;

/// <summary>V2.1 热爆：监听所有实际消耗；仅灼伤回血，状态牌均计入本回合消耗。</summary>
[RegisterPower]
public class HeatblastPassivePower : HeatblastPowerBase
{
    protected override string LocKey => "PASSIVE";
    public override PowerStackType StackType => PowerStackType.Single;
    public int BurnedStatusCountThisTurn { get; private set; }
    public bool HasBurnedStatusThisTurn => BurnedStatusCountThisTurn > 0;
    public bool HasGainedExtraEnergyThisTurn { get; private set; }
    private PlayerCombatState? _energyState;
    private bool _observeEnergy;
    private int _lastStartTurn = -1;

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        SubscribeEnergy();
        _observeEnergy = true;
        return Task.CompletedTask;
    }

    private void SubscribeEnergy()
    {
        var state = Owner.Player?.PlayerCombatState;
        if (_energyState == state) return;
        if (_energyState != null) _energyState.EnergyChanged -= OnEnergyChanged;
        _energyState = state;
        if (state != null) state.EnergyChanged += OnEnergyChanged;
    }

    private void OnEnergyChanged(int before, int after)
    {
        // 只观察自己的能力仍在场时的实际正增长；基础能量重置期间关闭观察。
        if (!_observeEnergy || after <= before || Owner.GetPower<HeatblastPassivePower>() != this) return;
        HasGainedExtraEnergyThisTurn = true;
        Owner.GetPower<HeatblastSupercombustionPower>()?.OnExtraEnergyGained();
    }

    public override Task BeforeSideTurnStart(PlayerChoiceContext context, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combat)
    {
        if (participants.Contains(Owner))
        {
            BurnedStatusCountThisTurn = 0;
            HasGainedExtraEnergyThisTurn = false;
            _observeEnergy = false;
            Owner.GetPower<HeatblastSupercombustionPower>()?.ResetPendingAttack();
        }
        return Task.CompletedTask;
    }

    public override Task AfterEnergyReset(Player player)
    {
        if (player.Creature == Owner)
        {
            SubscribeEnergy();
            _observeEnergy = true;
        }
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext context, Player player)
    {
        if (player.Creature != Owner || Owner.CombatState is not { } combat) return;
        if (player.PlayerCombatState is not { } turn || _lastStartTurn == turn.TurnNumber) return;
        _lastStartTurn = turn.TurnNumber;
        SubscribeEnergy();
        _observeEnergy = true;
        await HeatblastBurns.AddToHand(combat, player, 1);
        foreach (var enemy in combat.Enemies.Where(e => e.IsAlive).ToList())
            await AlienScorch.Apply(context, enemy, 1, null, Owner);
    }

    public override async Task AfterCardExhausted(PlayerChoiceContext context, CardModel card, bool causedByEthereal)
    {
        if (card.Owner.Creature != Owner || card.Type != CardType.Status) return;
        BurnedStatusCountThisTurn++;
        if (HeatblastStatus.IsBurnCard(card))
            await CreatureCmd.Heal(Owner, 2m);
    }

    public override async Task BeforeSideTurnEnd(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner) || Owner.Player is not { } player) return;
        await HeatblastStatus.Exhaust(context, player, HeatblastStatus.InHand(player), skipVisuals: true);
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        if (_energyState != null) _energyState.EnergyChanged -= OnEnergyChanged;
        _energyState = null;
        _observeEnergy = false;
        return Task.CompletedTask;
    }
}
