using MegaCrit.Sts2.Core.Runs;
using OtherworldTreasures.Scripts.Relics;
// TimeCloth 既是遗物类名又是同级的 TimeClothState 所在命名空间名，直接用简单名会解析成命名空间，故起别名
using TimeClothRelic = OtherworldTreasures.Scripts.Relics.TimeCloth;

namespace OtherworldTreasures.Scripts;

/// <summary>
/// 跑局生命周期中的状态复位。
///
/// 问题：各遗物把"本场战斗只能一次"的标记放在 static 字段里（战斗中遗物会被克隆，需要跨实例共享），
/// 而 static 只会在 AfterObtained（获得遗物时）或战斗开始/结束钩子里复位。玩家在战斗中存档后
/// 退回主菜单再读档时，既不会重新获得遗物、战斗开始钩子也不触发，于是 static 残留为"已用过"，
/// 导致遗物重进游戏后一直不可用。
///
/// 解法：订阅 RunManager.RunStarted —— 读档（NGame.LoadRun）和新开跑都会经过
/// RunManager.Launch() 触发它 —— 在这里把所有"本场战斗"级别的静态标记清回默认值，
/// 即允许玩家 SL。由 [SavedProperty] 保存的跨战斗进度仍会在之后从存档恢复，不受影响。
/// </summary>
internal static class RunLifecycle
{
    internal static void Initialize()
    {
        RunManager.Instance.RunStarted += _ => ResetCombatScopedState();
    }

    // 把所有"本场战斗"级别的静态状态清回默认值
    private static void ResetCombatScopedState()
    {
        Omnitrix.ResetCombatScopedState();
        DavesSeeds.ResetCombatScopedState();
        TimeClothRelic.ResetCombatScopedState();
        WhatIfPhoneBooth.ResetCombatScopedState();
        BambooCopter.ResetCombatScopedState();
        SporeSac.ResetCombatScopedState();
        Entry.Logger.Info("[RunLifecycle] 已重置本场战斗状态（读档/新开跑，允许 SL）");
    }
}
