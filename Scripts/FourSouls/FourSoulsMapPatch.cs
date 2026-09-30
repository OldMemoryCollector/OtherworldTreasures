using System;
using System.Collections.Generic;
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
/// 另提供 RefreshGlows 用于"本层中途获得碎片"时补挂（见该方法注释）。
/// </summary>
[HarmonyPatch(typeof(NNormalMapPoint), "_Ready")]
public static class Patch_NNormalMapPoint_FourSoulsGlow
{
    // NMapPoint._runState（protected IRunState）：直接问地图节点要 run 状态，不绕 RunManager
    private static readonly FieldInfo? RunStateField = AccessTools.Field(typeof(NMapPoint), "_runState");

    // NMapScreen._mapPointDictionary（private）：地图节点表（coord → 节点），补挂光芒要遍历它
    private static readonly FieldInfo? MapPointDictField =
        AccessTools.Field(typeof(NMapScreen), "_mapPointDictionary");

    // 该节点已经挂过光芒的标记：避免重复挂（补挂与 _Ready 可能都碰到同一个节点）
    private const string GlowMetaKey = "otherworld_four_souls_glow";

    private static void Postfix(NNormalMapPoint __instance)
    {
        if (RunStateField?.GetValue(__instance) is IRunState runState)
        {
            TryAttach(__instance, runState);
        }
    }

    /// <summary>
    /// 给当前地图上所有"该有光芒但还没挂"的节点补挂一次。
    /// 地图节点只在每层生成（NMapScreen.SetMap）时创建，_Ready 也只在那一刻跑；
    /// 玩家在本层中途获得【四魂之玉碎片】时节点早已建好，光芒挂不上去，
    /// 必须在这里主动补一次，否则要退出重进（重建地图）才看得到四魂怪。
    /// </summary>
    internal static void RefreshGlows(IRunState runState)
    {
        try
        {
            var screen = NMapScreen.Instance;
            if (screen == null
                || MapPointDictField?.GetValue(screen) is not Dictionary<MapCoord, NMapPoint> dict)
            {
                return;
            }
            foreach (var node in dict.Values)
            {
                if (node is NNormalMapPoint point)
                {
                    TryAttach(point, runState);
                }
            }
        }
        catch (Exception e)
        {
            Entry.Logger.Warn($"[FourSouls] 补挂地图光芒失败：{e}");
        }
    }

    private static void TryAttach(NNormalMapPoint instance, IRunState runState)
    {
        try
        {
            var point = instance.Point;
            if (point == null
                || (point.PointType != MapPointType.Monster && point.PointType != MapPointType.Elite))
            {
                return;
            }
            // 已经挂过就跳过（补挂会被多次调用）
            if (instance.HasMeta(GlowMetaKey))
            {
                return;
            }

            var souls = FourSoulsSystem.DeriveRoomSouls(runState);
            if (!souls.TryGetValue(point.coord, out var kinds) || kinds.Length == 0)
            {
                return;
            }

            instance.SetMeta(GlowMetaKey, true);

            // 不论开关状态都挂上：光芒自己按开关显示/隐藏（开关是战斗中也不能切的运行期状态，
            // 若在这里按 IsActive 提前返回，玩家在地图开着时打开四魂之玉就看不到光芒出现）
            var glow = new FourSoulsGlow();
            instance.AddChildSafely(glow);
            // 光芒层是复制 %Outline 得到的，位置在 Setup 里自己对齐，这里不用再设 Position
            glow.Setup(kinds, runState, instance);

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
