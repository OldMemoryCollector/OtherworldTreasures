using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using OtherworldTreasures.Scripts.Powers;

namespace OtherworldTreasures.Scripts.Alien;

// 0.111.0 没有“只执行一个招式中的格挡”公开事件。自身恐惧能力用公开 MoveState
// 包装当前招式，保留原版 MonsterModel.PerformMove 的历史记录、状态推进和清理管线。
// 下列命令过滤只在该包装器执行混合格挡招式的 AsyncLocal 作用域内生效；不修改 Hook、
// 网络编号、注册顺序，也不拦截没有魂飞的怪物。前缀均低优先级，先让其他模组处理。
[HarmonyPatch(typeof(MonsterModel), nameof(MonsterModel.RollMove))]
internal static class GhostFearRollMovePatch
{
    [HarmonyPostfix, HarmonyPriority(Priority.Low)]
    internal static void Postfix(MonsterModel __instance)
        => __instance.Creature?.GetPower<AlienTerrorPower>()?.RefreshEscapeIntent();
}

[HarmonyPatch(typeof(MonsterModel), nameof(MonsterModel.SetMoveImmediate))]
internal static class GhostFearSetMovePatch
{
    [HarmonyPostfix, HarmonyPriority(Priority.Low)]
    internal static void Postfix(MonsterModel __instance)
        => __instance.Creature?.GetPower<AlienTerrorPower>()?.RefreshEscapeIntent();
}

[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.Add), [typeof(Creature)])]
internal static class GhostFearSummonPatch
{
    // 高层 Add 重载返回尚未入场的对象；跳过唯一的入场方法，不产生召唤/奖励/入场事件。
    [HarmonyPrefix, HarmonyPriority(Priority.Low)]
    internal static bool Prefix(ref Task __result)
    {
        if (!GhostFear.InBlockOnlyMove) return true;
        __result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch]
internal static class GhostFearNonBlockCommandPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var method in typeof(CreatureCmd).GetMethods(BindingFlags.Public | BindingFlags.Static))
            if (method.ReturnType == typeof(Task) && method.Name is
                nameof(CreatureCmd.Heal) or nameof(CreatureCmd.Escape) or nameof(CreatureCmd.Kill) or
                nameof(CreatureCmd.SetCurrentHp) or nameof(CreatureCmd.SetMaxAndCurrentHp) or
                nameof(CreatureCmd.GainMaxHp) or nameof(CreatureCmd.LoseMaxHp) or nameof(CreatureCmd.Stun))
                yield return method;
        yield return AccessTools.Method(typeof(PowerCmd), nameof(PowerCmd.Remove), [typeof(PowerModel)]);
        yield return AccessTools.Method(typeof(PowerCmd), nameof(PowerCmd.Apply),
            [typeof(PlayerChoiceContext), typeof(PowerModel), typeof(Creature), typeof(decimal), typeof(Creature), typeof(CardModel), typeof(bool)]);
    }

    [HarmonyPrefix, HarmonyPriority(Priority.Low)]
    internal static bool Prefix(ref Task __result)
    {
        if (!GhostFear.InBlockOnlyMove) return true;
        __result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch(typeof(PowerCmd), nameof(PowerCmd.ModifyAmount),
    [typeof(PlayerChoiceContext), typeof(PowerModel), typeof(decimal), typeof(Creature), typeof(CardModel), typeof(bool)])]
internal static class GhostFearModifyPowerPatch
{
    [HarmonyPrefix, HarmonyPriority(Priority.Low)]
    internal static bool Prefix(PowerModel power, Creature? applier, ref Task<int> __result)
    {
        if (!GhostFear.InBlockOnlyMove || (applier != null && !GhostFear.IsBlockOnlyMove(applier))) return true;
        __result = Task.FromResult(power.Amount);
        return false;
    }
}

[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.SetMaxHp), [typeof(Creature), typeof(decimal)])]
internal static class GhostFearMaxHpPatch
{
    [HarmonyPrefix, HarmonyPriority(Priority.Low)]
    internal static bool Prefix(Creature creature, ref Task<decimal> __result)
    {
        if (!GhostFear.InBlockOnlyMove) return true;
        __result = Task.FromResult((decimal)creature.MaxHp);
        return false;
    }
}
