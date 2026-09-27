using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace OtherworldTreasures.Scripts.Powers;

/// <summary>
/// 驯服：被【桃太郎丸子】驯服的随从的固有特性（正面 buff）。
/// 它造成的攻击伤害 -50%（被丸子驯服后变得温顺，出手没那么重）。
/// 由 MomotaroDumplings 在召唤随从时施加，随从死亡/离场即随之消失。
/// 图标由 GamePatches 中针对本类型的 Postfix 指定。
/// </summary>
public class TamedPetPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    // 固定效果、不层叠：隐藏数字（恒为 1）
    public override PowerStackType StackType => PowerStackType.Single;

    public override LocString Title => new LocString("powers", "OTHERWORLD_TREASURES_POWER_TAMED_PET.title");

    public override LocString Description => new LocString("powers", "OTHERWORLD_TREASURES_POWER_TAMED_PET.description");

    // 只削弱"这个能力持有者（驯服的随从）自己造成"的攻击伤害
    public override decimal ModifyDamageMultiplicative(
        Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (Owner == null || dealer != Owner || !props.IsPoweredAttack())
        {
            return 1m;
        }
        return 0.5m;
    }
}
