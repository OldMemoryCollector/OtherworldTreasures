using System;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Powers;

// 闪光：带有它的敌人每次攻击都有概率落空。
// 原本由凯文每回合施加，凯文暂不做召唤物，改由【贝尔的日常小刀】遗物直接施加。
// 图标暂用原版占位图。
[RegisterPower]
public class FlashPower : ModPowerTemplate
{
    private const string PlaceholderIcon = "res://images/powers/missing_power.png";

    // 每次攻击落空的概率
    private const double MissChance = 0.3;

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: PlaceholderIcon,
        BigIconPath: PlaceholderIcon
    );

    // 只看持有者打出去的伤害；命中时掷一次，落空就这次不造成伤害
    public override decimal ModifyHpLostAfterOstyLate(
        Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (Owner == null || amount < 1m || dealer != Owner)
        {
            return amount;
        }

        return Random.Shared.NextDouble() < MissChance ? 0m : amount;
    }
}
