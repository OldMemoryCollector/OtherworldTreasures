using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using OtherworldTreasures.Scripts.Alien;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Powers;

/// <summary>普通/精英8层魂飞、16层魄散；Boss阈值翻倍。</summary>
[RegisterPower]
public class AlienTerrorPower : ModPowerTemplate
{
    internal const int SoulFrightThreshold = 8;
    internal const int DisperseThreshold = 16;
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerAssetProfile AssetProfile => AlienPowerArt.Profile(Stage == 2 ? "ghostfreak/disperse" : Stage == 1 ? "ghostfreak/soul_fright" : "ghostfreak/fear");
    public override LocString Title => new("powers", Stage == 2 ? "OTHERWORLD_TREASURES_POWER_GHOSTFREAK_DISPERSE.title" : Stage == 1 ? "OTHERWORLD_TREASURES_POWER_GHOSTFREAK_SOUL_FRIGHT.title" : "OTHERWORLD_TREASURES_POWER_ALIEN_TERROR.title");
    public override LocString Description
    {
        get
        {
            var description = new LocString("powers", "OTHERWORLD_TREASURES_POWER_ALIEN_TERROR.description");
            description.Add("SoulFrightThreshold", PanicThreshold);
            description.Add("DisperseThreshold", EscapeThreshold);
            return description;
        }
    }
    public bool IsBoss => (Owner.CombatState?.Encounter?.RoomType == RoomType.Boss
        || Owner.CombatState?.RunState.CurrentRoom?.RoomType == RoomType.Boss) && Owner.IsPrimaryEnemy;
    public int PanicThreshold => SoulFrightThreshold * (IsBoss ? 2 : 1);
    public int EscapeThreshold => DisperseThreshold * (IsBoss ? 2 : 1);
    public int Stage => Amount >= EscapeThreshold ? 2 : Amount >= PanicThreshold ? 1 : 0;
    private MoveState? _originalMove;
    private MoveState? _escapeMove;
    private MoveState? _restrictedMove;
    private bool _refreshing;
    private bool _expiresAtNextPlayerTurnEnd;

    internal void ExpireAfterGhostfreak() => _expiresAtNextPlayerTurnEnd = true;

    public override async Task AfterSideTurnEnd(PlayerChoiceContext c, CombatSide side, IEnumerable<Creature> participants)
    {
        // 由本次鬼影变身实际结束时启动，不使用战斗的第3/4回合编号。
        // 正常收尾在最后一个敌方回合结束；下一次玩家回合结束就是变身后的第4回合末。
        if (!_expiresAtNextPlayerTurnEnd || side != CombatSide.Player || Owner.CombatState is not { } combat)
            return;
        // 恐惧由全队共享，仍有鬼影在场时保留；最后一位鬼影结束后才开始完整的延迟清理。
        if (combat.PlayerCreatures.Any(player => player.HasPower<GhostfreakPassivePower>())) return;
        await PowerCmd.Remove(this);
    }
    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _originalMove = _escapeMove = _restrictedMove = null;
        _refreshing = false;
    }
    public override Task AfterApplied(Creature? applier, CardModel? source)
    {
        DisplayAmountChanged += RefreshEscapeIntent;
        RefreshEscapeIntent();
        return Task.CompletedTask;
    }
    internal void RefreshEscapeIntent()
    {
        if (_refreshing || Owner.Monster is not { MoveStateMachine: not null } monster || !Owner.IsAlive) return;
        _refreshing = true;
        try
        {
            if (monster.NextMove != _escapeMove && monster.NextMove != _restrictedMove)
            {
                _originalMove = monster.NextMove;
                _escapeMove = null;
                _restrictedMove = null;
            }
            if (Stage >= 2)
            {
                if (_escapeMove == null)
                {
                    _escapeMove = new MoveState("OTHERWORLD_TREASURES_FEAR_ESCAPE", _ => GhostFear.DefeatByFear(Owner), new GhostfreakDisperseIntent())
                    { FollowUpState = _originalMove };
                }
                if (monster.NextMove != _escapeMove) monster.SetMoveImmediate(_escapeMove, forceTransition: true);
            }
            else if (Stage == 1 && _originalMove is { } original)
            {
                _restrictedMove ??= new MoveState(original.Id, targets => PerformRestrictedMove(original, targets), original.Intents.ToArray())
                {
                    FollowUpState = original.FollowUpState,
                    FollowUpStateId = original.FollowUpStateId,
                    MustPerformOnceBeforeTransitioning = original.MustPerformOnceBeforeTransitioning,
                };
                if (monster.NextMove != _restrictedMove) monster.SetMoveImmediate(_restrictedMove, forceTransition: true);
            }
            else if (_originalMove != null && (monster.NextMove == _escapeMove || monster.NextMove == _restrictedMove))
            {
                monster.SetMoveImmediate(_originalMove, forceTransition: true);
                _originalMove = null;
                _escapeMove = null;
                _restrictedMove = null;
            }
        }
        finally { _refreshing = false; }
    }

    private async Task PerformRestrictedMove(MoveState original, IReadOnlyList<Creature> targets)
    {
        if (!Owner.IsAlive) return;
        if (Stage == 0) { await original.PerformMove(targets); return; }
        if (Stage >= 2) { await GhostFear.DefeatByFear(Owner); return; }
        // 没有格挡的行动完全不调用原招式，召唤、治疗、强化及未知意图均不会执行。
        // 混合行动沿用原版格挡数值和目标，其他效果在本次异步作用域内受限。
        if (original.Intents.Any(i => i.IntentType == IntentType.Defend))
            using (GhostFear.EnterBlockOnlyMove(Owner)) await original.PerformMove(targets);
    }
    public override Task BeforeSideTurnStart(PlayerChoiceContext c, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combat)
    {
        if (participants.Contains(Owner)) RefreshEscapeIntent();
        return Task.CompletedTask;
    }
    public override int ModifyAttackHitCount(AttackCommand attack, int hitCount)
        => attack.Attacker == Owner && Stage >= 1 ? 0 : hitCount;
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? source, CardPlay? play)
        => dealer == Owner && Stage >= 1 && (props.IsPoweredAttack() || GhostFear.IsBlockOnlyMove(Owner)) ? 0m : 1m;
    public override decimal ModifyPowerAmountGivenMultiplicative(PowerModel power, Creature giver, decimal amount, Creature? target, CardModel? source)
    {
        if (giver == Owner && Stage >= 1 && amount != 0)
        {
            return 0;
        }
        return 1m;
    }
    public override async Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
    {
        if (Stage < 1 || (Owner.Monster?.IsPerformingMove != true && !GhostFear.IsBlockOnlyMove(Owner)) || creator != null
            || card.Owner.Creature.Side == Owner.Side || card.Type is not (CardType.Status or CardType.Curse)) return;
        await CardPileCmd.RemoveFromCombat(card, skipVisuals: true);
        HandUiSync.RemoveFromHandUi([card]);
    }
    public override bool ShouldAfflict(CardModel card, AfflictionModel affliction)
    {
        if (Stage >= 1 && (Owner.Monster?.IsPerformingMove == true || GhostFear.IsBlockOnlyMove(Owner)) && card.Owner.Creature.Side != Owner.Side)
        {
            return false;
        }
        return true;
    }
    public override Task AfterRemoved(Creature oldOwner)
    {
        DisplayAmountChanged -= RefreshEscapeIntent;
        if (oldOwner.IsAlive && _originalMove != null && oldOwner.Monster is { } monster && (monster.NextMove == _escapeMove || monster.NextMove == _restrictedMove))
            monster.SetMoveImmediate(_originalMove, forceTransition: true);
        return Task.CompletedTask;
    }
}
