using System;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Powers;

/// <summary>
/// 恐惧（外星英雄体系）：叠在敌人身上的燃料，同时自带减伤。
/// - 该敌人造成的伤害减少 10% × 层数，最多减少 50%。
/// - 层数不会自己减少，需要被【恐惧收割】这类卡消费掉。
/// 名字沿用原版译名（原版【恐惧】是一张攻击牌，同名不同物，已与作者确认）。
/// </summary>
[RegisterPower]
public class AlienTerrorPower : ModPowerTemplate
{
    // 每层减少的伤害比例、最多减少的比例
    private const decimal ReductionPerStack = 0.1m;
    private const decimal MaxReduction = 0.5m;

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    // 占位图（借用原版易伤图标），专属图标做好后改这里
    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://images/powers/vulnerable_power.png");

    public override LocString Title => new LocString("powers", "OTHERWORLD_TREASURES_POWER_ALIEN_TERROR.title");

    public override LocString Description => new LocString("powers", "OTHERWORLD_TREASURES_POWER_ALIEN_TERROR.description");

    // 只有"这个敌人自己造成"的伤害才减（伤害来源是它时）
    public override decimal ModifyDamageMultiplicative(
        Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (dealer != Owner)
        {
            return 1m;
        }
        return 1m - Math.Min(MaxReduction, ReductionPerStack * Amount);
    }
}
