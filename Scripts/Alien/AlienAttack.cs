using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Combat.AttackHits;

namespace OtherworldTreasures.Scripts.Alien;

internal static class AlienAttack
{
    // 仅标记本模组创建的命令；RitsuLib 的公开命中事件修改当次伤害，保留原版 Execute 管线。
    // 弱引用键随命令回收，不把战斗对象保存在全局强引用集合中。
    private static readonly ConditionalWeakTable<AttackCommand, object> Piercing = new();
    internal static void BypassBlock(AttackCommand attack) => Piercing.GetValue(attack, _ => new object());
    internal static void BeforeHit(AttackHitContext context)
    {
        if (Piercing.TryGetValue(context.Attack, out _)) context.DamageProps |= ValueProp.Unblockable;
    }
}
