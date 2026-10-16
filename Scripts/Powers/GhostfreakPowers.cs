using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using OtherworldTreasures.Scripts.Alien;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Powers;

public abstract class GhostfreakPowerBase : ModPowerTemplate
{
    protected abstract string LocKey { get; }
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerAssetProfile AssetProfile => AlienPowerArt.Profile($"ghostfreak/{LocKey}");
    public override LocString Title => new("powers", $"OTHERWORLD_TREASURES_POWER_GHOSTFREAK_{LocKey}.title");
    public override LocString Description => new("powers", $"OTHERWORLD_TREASURES_POWER_GHOSTFREAK_{LocKey}.description");
}
[RegisterPower]
public class GhostfreakPassivePower : GhostfreakPowerBase
{
    protected override string LocKey => "PASSIVE";
    public override PowerStackType StackType => PowerStackType.Single;
    private int _lastTurn = -1;
    public bool NextAttackReversed { get; set; }
    private Stack<CardPlay> _plays = new();
    private Dictionary<CardPlay, HashSet<Creature>> _reversed = new();
    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _plays = new();
        _reversed = new();
    }
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext c, Player player)
    {
        if (player.Creature != Owner || player.PlayerCombatState is not { } state || _lastTurn == state.TurnNumber) return;
        _lastTurn = state.TurnNumber;
        Owner.GetPower<GhostfreakInterlacePower>()?.ResetTurn();
        await PowerCmd.Apply<IntangiblePower>(c, Owner, 1m, Owner, null);
        if (Owner.CombatState is { } combat)
            foreach (var enemy in combat.Enemies.Where(e => e.IsAlive).ToList())
                await GhostFear.Apply(c, enemy, 1, Owner, null);
    }
    public override Task BeforeCardPlayed(CardPlay play)
    {
        if (play.Player.Creature == Owner)
        {
            _plays.Push(play);
            if (play.Card.Type == CardType.Attack && NextAttackReversed)
            {
                NextAttackReversed = false;
                _reversed[play] = new();
            }
        }
        return Task.CompletedTask;
    }
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? source, CardPlay? play)
        => dealer == Owner && source?.Type == CardType.Attack && props.IsPoweredAttack()
            && (play != null ? _reversed.ContainsKey(play) : NextAttackReversed) ? 2m : 1m;
    public override async Task AfterDamageGiven(PlayerChoiceContext c, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? source)
    {
        if (dealer != Owner || !props.IsPoweredAttack() || !target.IsAlive || result.TotalDamage <= 0 || _plays.Count == 0) return;
        var play = _plays.Peek();
        if (play.Card == source && _reversed.TryGetValue(play, out var targets) && targets.Add(target))
            await GhostFear.Apply(c, target, 2, Owner, source);
    }
    public override Task AfterCardPlayed(PlayerChoiceContext c, CardPlay play)
    {
        _reversed.Remove(play);
        if (_plays.Count > 0 && _plays.Peek() == play) _plays.Pop();
        return Task.CompletedTask;
    }
    internal async Task<bool> ConsumeIntangible(PlayerChoiceContext c, CardModel source)
    {
        if (Owner.GetPower<IntangiblePower>() is not { Amount: > 0 } intangible) return false;
        await PowerCmd.ModifyAmount(c, intangible, -1m, Owner, source);
        if (Owner.GetPower<GhostfreakInterlacePower>() is { } interlace) await interlace.OnConsumed(c);
        return true;
    }
    public override Task AfterRemoved(Creature oldOwner)
    {
        // 牌组/自身能力还原时撤去鬼影被动，以此作为本次变身实际结束的时点。
        if (oldOwner.CombatState is { } combat)
            foreach (var enemy in combat.Enemies.Where(enemy => enemy.IsAlive).ToList())
                enemy.GetPower<AlienTerrorPower>()?.ExpireAfterGhostfreak();
        return Task.CompletedTask;
    }
}
[RegisterPower]
public class GhostfreakTrueFormPower : GhostfreakPowerBase { protected override string LocKey => "TRUE_FORM"; }
[RegisterPower]
public class GhostfreakInterlacePower : GhostfreakPowerBase
{
    protected override string LocKey => "INTERLACE";
    private bool _triggered;
    internal void ResetTurn() => _triggered = false;
    internal async Task OnConsumed(PlayerChoiceContext c)
    {
        if (_triggered) return;
        _triggered = true;
        if (Owner.Player is { } player) await PlayerCmd.GainEnergy(Amount, player);
        if (Owner.CombatState is { } combat)
            foreach (var enemy in combat.Enemies.Where(e => e.IsAlive).ToList())
                await GhostFear.Apply(c, enemy, 2 * Amount, Owner, null);
    }
}
[RegisterPower]
public class GhostfreakDominionPower : GhostfreakPowerBase
{
    protected override string LocKey => "DOMINION";
    public override async Task AfterPowerAmountChanged(PlayerChoiceContext c, PowerModel power,
        decimal amount, Creature? applier, CardModel? cardSource)
    {
        // 只在实际恐惧层数从阈值以下跨入魂飞时触发；继续叠加或禁止意图不重复扣血。
        if (power is not AlienTerrorPower fear || amount <= 0 || power.Owner.Side == Owner.Side
            || !power.Owner.IsAlive || fear.Amount < fear.PanicThreshold
            || fear.Amount - (int)amount >= fear.PanicThreshold) return;
        await CreatureCmd.Damage(c, power.Owner, 12m * Amount,
            ValueProp.Unpowered | ValueProp.Unblockable, Owner, null, null);
    }
    public override async Task AfterBlockGained(Creature creature, decimal amount, ValueProp props, CardModel? source)
    {
        if (creature.Side != Owner.Side && amount >= 2)
            await CreatureCmd.GainBlock(Owner, System.Math.Floor(amount / 2) * Amount, ValueProp.Unpowered, null);
    }
}
