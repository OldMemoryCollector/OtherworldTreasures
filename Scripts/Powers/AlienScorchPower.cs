using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Powers;

/// <summary>
/// 灼伤（外星英雄体系）：叠在敌人身上的燃料，同时自带伤害。
/// - 该敌人【自己回合结束时】，受到 2 × 层数 的伤害（可被格挡）。
/// - 触发后**层数全部消耗**（所以它是"攒够了就炸一次"的燃料，要么被【爆燃】提前用掉，要么等它自己结算）。
/// - 火焰人免疫：持有【熔岩之躯】(HeatblastPassivePower) 的火焰人不吃这次伤害。
/// 名字与本地化都改回原名【灼伤】/ Burn，与原版灼伤牌同名不同物。
/// </summary>
[RegisterPower]
public class AlienScorchPower : ModPowerTemplate
{
    // 每层造成的伤害
    private const decimal DamagePerStack = 2m;

    // 上次结算的回合号：AfterSideTurnEnd 实测一个回合可能被触发两次，用回合号防重
    private int _lastResolvedRound = -1;

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    // 占位图（借用原版中毒图标），专属图标做好后改这里
    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://images/powers/poison_power.png");

    public override LocString Title => new LocString("powers", "OTHERWORLD_TREASURES_POWER_ALIEN_SCORCH.title");

    public override LocString Description => new LocString("powers", "OTHERWORLD_TREASURES_POWER_ALIEN_SCORCH.description");

    // 该敌人【自己回合结束时】结算：受 2 × 层数 伤害（可被格挡），然后层数全部消耗
    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner == null || !Owner.IsAlive)
        {
            return;
        }
        // ① 只处理“该敌人自己那一侧”的回合结束
        if (side != Owner.Side)
        {
            return;
        }
        if (!participants.Contains(Owner))
        {
            return;
        }
        // ③ 火焰人免疫：持有【熔岩之躯】的火焰人不吃这次伤害（玩家侧万一被上了灼伤也不掉血）
        if (Owner.HasPower<HeatblastPassivePower>())
        {
            return;
        }
        // ② 用回合号防重：AfterSideTurnEnd 一个回合可能被触发两次，同一回合只结算一次
        var combatState = Owner.CombatState;
        if (combatState == null)
        {
            return;
        }
        if (_lastResolvedRound == combatState.RoundNumber)
        {
            return;
        }
        _lastResolvedRound = combatState.RoundNumber;

        // ValueProp 不带 Unblockable → 伤害可被格挡（照原版 ConstrictPower / DemisePower 的回合结束伤害写法）
        await CreatureCmd.Damage(choiceContext, Owner, Amount * DamagePerStack, ValueProp.Unpowered, null, null);

        // 触发后层数全部消耗：整条能力移除（下次重新叠从 0 开始）
        await PowerCmd.Remove(this);
    }
}
