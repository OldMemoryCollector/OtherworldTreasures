using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace OtherworldTreasures.Scripts.Powers;

/// <summary>
/// 强化（buff 名：三点强化）：造成的伤害和获得的格挡值变为三倍。
/// 由库拉的骰子三点能力添加。在 KurasDice 遗物的 AfterPlayerTurnStart 中移除。
/// </summary>
public class AmplifyPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    // 本地化：buff 名称与描述（powers 表，见 localization/zhs/powers.json）
    // 显式指定 key，避免依赖模型自动生成的 Id.Entry
    public override LocString Title => new LocString("powers", "OTHERWORLD_TREASURES_POWER_AMPLIFY_POWER.title");

    public override LocString Description => new LocString("powers", "OTHERWORLD_TREASURES_POWER_AMPLIFY_POWER.description");

    // 玩家造成的伤害 ×3
    public override decimal ModifyDamageMultiplicative(
        Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (dealer == Owner)
            return 3m;
        return 1m;
    }

    // 玩家获得的格挡 ×3
    public override decimal ModifyBlockMultiplicative(
        Creature target, decimal block, ValueProp props,
        CardModel? cardSource, CardPlay? cardPlay)
    {
        if (target == Owner)
            return 3m;
        return 1m;
    }
}
