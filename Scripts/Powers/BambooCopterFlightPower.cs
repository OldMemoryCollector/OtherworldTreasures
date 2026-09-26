using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace OtherworldTreasures.Scripts.Powers;

/// <summary>
/// 起飞：竹蜻蜓的能力。
/// - 被攻击时受到的伤害 ×0.5（−50%）
/// - 被攻击 3 次后失效（Amount 即剩余次数，归零前主动移除）
/// - 视觉：起飞时把人物抬升并略微缩小（飞高了看起来小一点），失效时落回地面并还原
/// 图标由 GamePatches 中针对本类型的 Postfix 指定。
/// </summary>
public class BambooCopterFlightPower : PowerModel
{
    // 起飞时人物抬升的高度（像素）
    private const float LiftHeight = 70f;
    private const double LiftDuration = 0.4;
    // 起飞时人物的缩放倍率（比地面小一点，与抬升同步）
    private const float FlightScale = 0.85f;

    // 起飞前人物的原始位置与缩放，落地时还原
    private Vector2? _groundBodyPosition;
    private Vector2? _groundBodyScale;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    // 本地化：buff 名称与描述（powers 表）
    public override LocString Title => new LocString("powers", "OTHERWORLD_TREASURES_POWER_BAMBOO_COPTER_FLIGHT_POWER.title");

    public override LocString Description => new LocString("powers", "OTHERWORLD_TREASURES_POWER_BAMBOO_COPTER_FLIGHT_POWER.description");

    // 被敌人攻击时受到的伤害减少 50%（与"被攻击 3 次后失效"对齐，非攻击伤害不减免）
    public override decimal ModifyDamageMultiplicative(
        Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (target != Owner)
        {
            return 1m;
        }
        if (!props.IsPoweredAttack())
        {
            return 1m;
        }
        return 0.5m;
    }

    // 被攻击：消耗一次起飞次数（Amount 即剩余次数）
    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext, Creature target, DamageResult result,
        ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner)
        {
            return;
        }
        // 只统计"被攻击"（攻击性伤害），非攻击伤害不消耗次数
        if (!props.IsPoweredAttack())
        {
            return;
        }

        if (Amount <= 1)
        {
            Entry.Logger.Info("[BambooCopter] Flight broken (charges exhausted), landing");
            await PowerCmd.Remove(this);
        }
        else
        {
            Entry.Logger.Info($"[BambooCopter] Hit taken, remaining charges = {Amount - 1}");
            await PowerCmd.Decrement(this);
        }
    }

    // 起飞 / 落地：抬升与还原人物
    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        StartFlying();
        return Task.CompletedTask;
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        Land();
        return Task.CompletedTask;
    }

    private Node2D? GetBody()
    {
        return NCombatRoom.Instance?.GetCreatureNode(Owner)?.Visuals?.GetCurrentBody();
    }

    private void StartFlying()
    {
        var body = GetBody();
        if (body == null || _groundBodyPosition != null)
        {
            return; // 已在飞行中，不重复起飞（避免把空中位置当成地面位置）
        }
        // 用 Body 而不是 Visuals：受击抖动 AnimShake 会把 Visuals.Position 归零，会破坏抬升
        _groundBodyPosition = body.Position;
        _groundBodyScale = body.Scale;
        var tween = body.CreateTween().SetParallel();
        tween.TweenProperty(body, "position", _groundBodyPosition.Value + new Vector2(0f, -LiftHeight), LiftDuration);
        tween.TweenProperty(body, "scale", _groundBodyScale.Value * FlightScale, LiftDuration);
    }

    private void Land()
    {
        var body = GetBody();
        if (body == null || _groundBodyPosition == null || _groundBodyScale == null)
        {
            return;
        }
        var tween = body.CreateTween().SetParallel();
        tween.TweenProperty(body, "position", _groundBodyPosition.Value, LiftDuration);
        tween.TweenProperty(body, "scale", _groundBodyScale.Value, LiftDuration);
        // 清空，便于落地后再次起飞时重新记录
        _groundBodyPosition = null;
        _groundBodyScale = null;
    }
}
