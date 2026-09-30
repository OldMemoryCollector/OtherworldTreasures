using System;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;

namespace OtherworldTreasures.Scripts.FourSouls;

/// <summary>
/// 地图节点上的四魂表现：
/// - 在节点图标上叠一层光芒，单魂固定该魂颜色，多魂在各魂颜色间循环
/// - 节点悬浮文字显示该房间持有的四魂
/// 挂点选在 NNormalMapPoint._Ready（节点每次重建都会跑，读档/重开地图都会重新挂）。
/// </summary>
[HarmonyPatch(typeof(NNormalMapPoint), "_Ready")]
public static class Patch_NNormalMapPoint_FourSoulsGlow
{
    // NMapPoint._runState（protected IRunState）：直接问地图节点要 run 状态，不绕 RunManager
    private static readonly FieldInfo? RunStateField = AccessTools.Field(typeof(NMapPoint), "_runState");

    private static void Postfix(NNormalMapPoint __instance)
    {
        try
        {
            var point = __instance.Point;
            if (point == null
                || (point.PointType != MapPointType.Monster && point.PointType != MapPointType.Elite))
            {
                return;
            }

            if (RunStateField?.GetValue(__instance) is not IRunState runState)
            {
                return;
            }

            var souls = FourSoulsSystem.DeriveRoomSouls(runState);
            if (!souls.TryGetValue(point.coord, out var kinds) || kinds.Length == 0)
            {
                return;
            }

            // 不论开关状态都挂上：光芒自己按开关显示/隐藏（开关是战斗中也不能切的运行期状态，
            // 若在这里按 IsActive 提前返回，玩家在地图开着时打开四魂之玉就看不到光芒出现）
            var glow = new FourSoulsGlow();
            __instance.AddChildSafely(glow);
            // 光芒层是复制 %Outline 得到的，位置在 Setup 里自己对齐，这里不用再设 Position
            glow.Setup(kinds, runState, __instance);

            Entry.Logger.Info(
                $"[FourSouls] 地图光芒已挂载 ({point.coord.col},{point.coord.row}) " +
                $"souls={string.Join(",", kinds)} 开启={FourSoulsSystem.IsActive(runState)}");
        }
        catch (Exception e)
        {
            Entry.Logger.Warn($"[FourSouls] 地图光芒挂载失败：{e}");
        }
    }
}
