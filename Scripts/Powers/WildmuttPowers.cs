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
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using OtherworldTreasures.Scripts.Alien;
using OtherworldTreasures.Scripts.Cards.Alien;

namespace OtherworldTreasures.Scripts.Powers;

public abstract class WildmuttPowerBase : ModPowerTemplate
{
    protected abstract string LocKey { get; }
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override LocString Title => new("powers", $"OTHERWORLD_TREASURES_POWER_WILDMUTT_{LocKey}.title");
    public override LocString Description => new("powers", $"OTHERWORLD_TREASURES_POWER_WILDMUTT_{LocKey}.description");
    public override PowerAssetProfile AssetProfile => AlienPowerArt.Profile($"wildmutt/{LocKey}");
}

[RegisterPower]
public class WildmuttPassivePower : WildmuttPowerBase
{
    protected override string LocKey => "PASSIVE";
    public Creature? Prey { get; private set; }
    public bool PlayedSkillThisTurn { get; private set; }
    public bool NextSkillReduced { get; set; }
    public bool NextAttackReduced { get; set; }
    public bool NextPreyBonusDouble { get; set; }
    private HashSet<CardPlay> _doubledExtraPlays = new();
    private Stack<CardPlay> _plays = new();
    private HashSet<CardPlay> _extraUsed = new();
    private bool _leapUsedThisTurn;
    private HashSet<Creature> _rewarded = new();
    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _rewarded = new();
        Prey = null;
        _plays = new();
        _extraUsed = new();
        _doubledExtraPlays = new();
        NextPreyBonusDouble = false;
    }
    public bool IsPrey(Creature? target) => target != null && target == Prey;
    public bool PreyAttacks => Prey?.Monster?.IntendsToAttack == true;
    public bool PreyBelowHalf => Prey is { } prey && prey.CurrentHp * 2 < prey.MaxHp;

    public async Task RefreshPrey(PlayerChoiceContext c)
    {
        var combat = Owner.CombatState;
        if (Prey is { IsAlive: true } existing && combat?.Enemies.Contains(existing) == true) return;
        Prey = combat?.Enemies.Where(e => e.IsAlive).OrderBy(e => e.CurrentHp).ThenBy(e => e.CombatId ?? uint.MaxValue).FirstOrDefault();
        if (Prey != null) await PowerCmd.Apply<WildmuttPreyPower>(c, Prey, 1m, Owner, null, silent: true);
    }

    public override Task BeforeCardPlayed(CardPlay play)
    {
        if (play.Player.Creature != Owner) return Task.CompletedTask;
        _plays.Push(play);
        if (play.Card.Type == CardType.Skill) { PlayedSkillThisTurn = true; NextSkillReduced = false; }
        if (play.Card.Type == CardType.Attack) NextAttackReduced = false;
        return Task.CompletedTask;
    }
    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        bool reduce = card.Owner.Creature == Owner && ((card.Type == CardType.Skill && NextSkillReduced) || (card.Type == CardType.Attack && NextAttackReduced));
        modifiedCost = reduce ? System.Math.Max(0, originalCost - 1) : originalCost;
        return reduce;
    }

    public override async Task AfterDamageGiven(PlayerChoiceContext c, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? source)
    {
        if (dealer != Owner || target != Prey) return;
        if (_plays.Count > 0 && source == _plays.Peek().Card && source.Type == CardType.Attack
            && props.IsPoweredAttack() && _extraUsed.Add(_plays.Peek()))
        {
            var play = _plays.Peek(); // 已占用本次打出的标记，追加攻击和嵌套出牌不会重复触发。
            bool doubled = NextPreyBonusDouble;
            NextPreyBonusDouble = false;
            if (!_leapUsedThisTurn && Owner.HasPower<WildmuttLeapingInstinctPower>())
            {
                _leapUsedThisTurn = true;
                await CreatureCmd.GainBlock(Owner, 6m, ValueProp.Unpowered, null);
                if (Owner.Player is { } player) await CardPileCmd.Draw(c, 1, player);
            }
            if (target.IsAlive)
            {
                var extra = DamageCmd.Attack(Owner.HasPower<WildmuttFeedingFrenzyPower>() ? 10m : 6m)
                    .FromCard(source, play).Targeting(target).WithHitFx("vfx/vfx_attack_slash");
                // 追加命中也是此牌的伤害；锁喉猛咬攻击猎物时整张牌都穿透格挡。
                if (source is WildmuttThroatBite) AlienAttack.BypassBlock(extra);
                if (doubled) _doubledExtraPlays.Add(play);
                try { await extra.Execute(c); }
                finally { _doubledExtraPlays.Remove(play); }
            }
        }
        if (target.IsDead) await RewardAndRetarget(c, target);
    }

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
        => dealer == Owner && props.IsPoweredAttack() && cardPlay != null && _doubledExtraPlays.Contains(cardPlay) ? 2m : 1m;

    private async Task RewardAndRetarget(PlayerChoiceContext c, Creature target)
    {
        if (!_rewarded.Add(target)) return;
        await RefreshPrey(c);
        if (Owner.Player is { } player && Owner.IsAlive)
        {
            await PlayerCmd.GainEnergy(1m, player);
            await CardPileCmd.Draw(c, 1, player);
        }
    }
    public override async Task AfterDeath(PlayerChoiceContext c, Creature creature, bool wasRemovalPrevented, float deathAnimLength)
    {
        if (!wasRemovalPrevented && creature == Prey && creature.IsDead) await RewardAndRetarget(c, creature);
    }
    public override Task AfterCardPlayed(PlayerChoiceContext c, CardPlay play)
    {
        _extraUsed.Remove(play);
        if (_plays.Count > 0 && _plays.Peek() == play) _plays.Pop();
        return Task.CompletedTask;
    }
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext c, Player player)
    {
        if (player.Creature != Owner) return;
        PlayedSkillThisTurn = false;
        NextSkillReduced = false;
        NextAttackReduced = false;
        NextPreyBonusDouble = false;
        _leapUsedThisTurn = false;
        await RefreshPrey(c);
    }
    public override Task AfterSideTurnEnd(PlayerChoiceContext c, CombatSide side, IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner)) { NextSkillReduced = false; NextAttackReduced = false; NextPreyBonusDouble = false; }
        return Task.CompletedTask;
    }
    public override async Task AfterRemoved(Creature oldOwner)
    {
        if (Prey?.GetPower<WildmuttPreyPower>() is { } mark && oldOwner.CombatState is { } combat
            && !combat.PlayerCreatures.Any(c => c.GetPower<WildmuttPassivePower>() is { } other && other != this && other.Prey == Prey))
            await PowerCmd.Remove(mark);
    }
}
[RegisterPower]
public class WildmuttPreyPower : WildmuttPowerBase
{
    protected override string LocKey => "PREY";
    public override PowerType Type => PowerType.Buff;
}
[RegisterPower]
public class WildmuttLeapingInstinctPower : WildmuttPowerBase { protected override string LocKey => "LEAPING_INSTINCT"; }
[RegisterPower]
public class WildmuttFeedingFrenzyPower : WildmuttPowerBase { protected override string LocKey => "FEEDING_FRENZY"; }
[RegisterPower]
public class WildmuttThermalSensePower : WildmuttPowerBase
{
    protected override string LocKey => "THERMAL_SENSE";
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext c, Player player)
    {
        if (player.Creature != Owner) return;
        if (Owner.GetPower<WildmuttPassivePower>()?.PreyAttacks == true)
            await CreatureCmd.GainBlock(Owner, 8m, ValueProp.Unpowered, null);
        else await CardPileCmd.Draw(c, 1, player);
    }
}
