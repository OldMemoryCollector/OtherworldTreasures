using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Screens.InspectScreens;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.PotionLab;
using MegaCrit.Sts2.Core.Nodes.Screens.RelicCollection;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Potions;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models.Monsters;
using OtherworldTreasures.Scripts.Monsters;
using OtherworldTreasures.Scripts.Potions;
using OtherworldTreasures.Scripts.Relics;
using OtherworldTreasures.Scripts.TimeCloth;
using STS2RitsuLib.Interactions.RightClick;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts;

/// <summary>
/// Harmony patches for 库拉的骰子.
/// 策略：
/// - Description (private): 覆盖文本（战斗内/外/已失效切换）
/// - DynamicDescription: 只打日志，不覆盖！原版会自动调用 Description + 加 DynamicVars
/// - Title / Flavor / EventDescription: 覆盖
/// </summary>
public static class GamePatches
{
    public static readonly HashSet<CardModel> ForceGlowGoldCards = new();
    internal static bool FirstTimeLogged;
}

/// <summary>
/// 金苹果 / 附魔金苹果：暂时隐藏。
/// 隐藏后不会出现在：先古之民（旧忆收藏家）、商店、药水图鉴，也不进随机药水池。
/// 以后要放出来，把 <see cref="GoldenApplesHidden"/> 改回 false 即可。
/// </summary>
internal static class HiddenPotions
{
    public const bool GoldenApplesHidden = true;

    public static bool IsHidden(PotionModel potion)
        => GoldenApplesHidden && potion is GoldenApple or EnchantedGoldenApple;
}

// === Patch: CardModel.ShouldGlowGold getter ===
[HarmonyPatch(typeof(CardModel), "ShouldGlowGold", MethodType.Getter)]
public static class Patch_CardModel_ShouldGlowGold
{
    static void Postfix(CardModel __instance, ref bool __result)
    {
        if (GamePatches.ForceGlowGoldCards.Contains(__instance))
            __result = true;
    }
}

// === Patch: NPotionContainer.GrowPotionHolders（补齐"减少药水栏"方向） ===
// 原版只按 for (i = _holders.Count; i < newMax; i++) 补齐，MaxPotionCount 变小时不会移除多余药水格，
// 导致原素瓶 -1 药水栏后顶栏 UI 不变。Postfix 在 max 变小时摘掉多余 holder。
// 必须同时从私有 _holders 列表中移除：否则该列表长度仍偏大，后续放药水会放进已隐藏的格子。
[HarmonyPatch(typeof(NPotionContainer), "GrowPotionHolders")]
public static class Patch_NPotionContainer_GrowPotionHolders
{
    static void Postfix(NPotionContainer __instance, int newMaxPotionSlots)
    {
        try
        {
            var holdersField = AccessTools.Field(typeof(NPotionContainer), "_holders");
            if (holdersField?.GetValue(__instance) is not IList holders) return;
            while (holders.Count > newMaxPotionSlots)
            {
                int last = holders.Count - 1;
                if (holders[last] is NPotionHolder holder)
                {
                    holder.QueueFreeSafely();
                }
                holders.RemoveAt(last);
                Entry.Logger.Info($"[PotionUi] removed holder → count={holders.Count} (max={newMaxPotionSlots})");
            }
        }
        catch (Exception e)
        {
            Entry.Logger.Info($"[PotionUi] shrink error: {e.Message}");
        }
    }
}

/// <summary>构造我们遗物的 LocString，和原版 RelicModel 写法完全一致</summary>
internal static class KurasDiceL10n
{
    private const string Key = "OTHERWORLD_TREASURES_RELIC_KURAS_DICE";
    public static LocString Title => new LocString("relics", Key + ".title");
    public static LocString Flavor => new LocString("relics", Key + ".flavor");
    public static LocString BaseDesc => new LocString("relics", Key + ".description");
    public static LocString Broken => new LocString("relics", Key + ".desc.broken");
    public static LocString FaceDesc(int face) => new LocString("relics", $"{Key}.face_{face}.desc");

    /// <summary>当前状态应该显示的描述文本（不含 DynamicVars）</summary>
    public static LocString CurrentText()
    {
        // 局外（图鉴/收藏）只显示基础描述，不显示运行中的点数与"已失效"状态，
        // 否则上一局的骰子状态会被带进图鉴。
        if (!(RunManager.Instance?.IsInProgress ?? false)) return BaseDesc;
        if (KurasDice.GlobalIsBroken) return Broken;
        if (KurasDice.LastRolledValue > 0)
        {
            int face = KurasDice.LastRolledValue;
            bool used = KurasDice.GlobalUsedFaces.Contains(face);
            var ls = FaceDesc(face);
            ls.Add("Status", used ? "(已失效)" : "");
            return ls;
        }
        return BaseDesc;
    }
}

// === Patch 1: RelicModel.Title (public virtual) ===
[HarmonyPatch(typeof(RelicModel), "Title", MethodType.Getter)]
public static class Patch_RelicModel_Title
{
    static void Postfix(RelicModel __instance, ref LocString __result)
    {
        if (__instance is not KurasDice dice) return;
        if (!GamePatches.FirstTimeLogged)
        {
            GamePatches.FirstTimeLogged = true;
            Entry.Logger.Info($"[KurasDice DEBUG] Id.Entry='{dice.Id.Entry}'");
        }
        __result = KurasDiceL10n.Title;
    }
}

// === Patch 2: RelicModel.Flavor (public) ===
[HarmonyPatch(typeof(RelicModel), "Flavor", MethodType.Getter)]
public static class Patch_RelicModel_Flavor
{
    static void Postfix(RelicModel __instance, ref LocString __result)
    {
        if (__instance is not KurasDice) return;
        __result = KurasDiceL10n.Flavor;
    }
}

// === Patch 3: RelicModel.Description (private!) ===
// 原版写法：new LocString("relics", base.Id.Entry + ".description")
// 这是 DynamicDescription / HoverTip 等所有描述的根，覆盖它就能统一
[HarmonyPatch(typeof(RelicModel), "Description", MethodType.Getter)]
public static class Patch_RelicModel_Description
{
    static void Postfix(RelicModel __instance, ref LocString __result)
    {
        if (__instance is not KurasDice) return;
        
        int roll = KurasDice.LastRolledValue;
        bool used = roll > 0 && KurasDice.GlobalUsedFaces.Contains(roll);
        Entry.Logger.Info($"[DescriptionPatch] Roll={roll}, Used={used}, Faces=[{string.Join(",", KurasDice.GlobalUsedFaces)}]");
        
        __result = KurasDiceL10n.CurrentText();
    }
}

// === Patch 4: RelicModel.EventDescription (protected) ===
[HarmonyPatch(typeof(RelicModel), "EventDescription", MethodType.Getter)]
public static class Patch_RelicModel_EventDescription
{
    static void Postfix(RelicModel __instance, ref LocString __result)
    {
        if (__instance is not KurasDice) return;
        __result = KurasDiceL10n.BaseDesc;
    }
}

// === Patch 4b: ICombatCompactRelic 战斗紧凑描述 ===
// 实现该接口的遗物，战斗中主描述换成 {Id.Entry}.combatDescription（没配这个 key 就仍用 .description）。
// Description 是 DynamicDescription / HoverTip / 详情大窗共用的唯一取文本源头，改这一处全部生效。
[HarmonyPatch(typeof(RelicModel), "Description", MethodType.Getter)]
public static class Patch_RelicModel_CombatCompactDescription
{
    static void Postfix(RelicModel __instance, ref LocString __result)
    {
        if (__instance is not ICombatCompactRelic compact) return;
        if (!CombatManager.Instance.IsInProgress) return;
        // 只隐藏额外提示、主描述保持完整的遗物（如惊奇套牌）在此直接返回
        if (!compact.UseCompactDescriptionInCombat) return;
        var combatDesc = LocString.GetIfExists("relics", __instance.Id.Entry + ".combatDescription");
        if (combatDesc != null)
        {
            __result = combatDesc;
        }
    }
}

// === Patch 4c: 战斗中隐藏紧凑遗物的额外悬浮提示（注火/穿越等非战斗机制说明）===
// 必须 patch ModRelicTemplate 的 override：mod 遗物的 ExtraHoverTips 走的是 RitsuLib 派生实现，
// patch RelicModel 基类的虚方法拦不到。HoverTips（顶栏悬停）和 HoverTipsExcludingRelic
// （点击大窗/事件选项）都从它取，所以这一处同时覆盖悬停框和大窗。
[HarmonyPatch(typeof(ModRelicTemplate), "ExtraHoverTips", MethodType.Getter)]
public static class Patch_ModRelic_CombatCompactExtraTips
{
    static void Postfix(RelicModel __instance, ref IEnumerable<IHoverTip> __result)
    {
        if (__instance is not ICombatCompactRelic) return;
        if (CombatManager.Instance.IsInProgress)
        {
            __result = Array.Empty<IHoverTip>();
        }
    }
}

// === Patch 5: AmplifyPower → 使用自定义「三点强化」图标 ===
// PowerModel.PackedIconPath 不是 virtual，用 Harmony Postfix 改 __result。
// 注意：原版力量图标的真实文件名是 strength_power.tres / strength_power.png，
// 这里直接指向本模组的自定义图标（64x64 小图 / 256x256 大图）。
[HarmonyPatch(typeof(PowerModel), "PackedIconPath", MethodType.Getter)]
public static class Patch_PowerModel_PackedIconPath
{
    static void Postfix(PowerModel __instance, ref string __result)
    {
        if (__instance is OtherworldTreasures.Scripts.Powers.AmplifyPower)
            __result = "res://OtherworldTreasures/images/powers/Amplify_Power.png";
    }
}

[HarmonyPatch(typeof(PowerModel), "IconPath", MethodType.Getter)]
public static class Patch_PowerModel_IconPath
{
    static void Postfix(PowerModel __instance, ref string __result)
    {
        if (__instance is OtherworldTreasures.Scripts.Powers.AmplifyPower)
            __result = "res://OtherworldTreasures/images/powers/Amplify_Power.png";
    }
}

[HarmonyPatch(typeof(PowerModel), "BigIconPath", MethodType.Getter)]
public static class Patch_PowerModel_BigIconPath
{
    static void Postfix(PowerModel __instance, ref string __result)
    {
        if (__instance is OtherworldTreasures.Scripts.Powers.AmplifyPower)
            __result = "res://OtherworldTreasures/images/powers/Amplify_Power_Big.png";
    }
}

// === Patch 6: BambooCopterFlightPower → 自定义「起飞」图标 ===
[HarmonyPatch(typeof(PowerModel), "PackedIconPath", MethodType.Getter)]
public static class Patch_PowerModel_PackedIconPath_BambooCopter
{
    static void Postfix(PowerModel __instance, ref string __result)
    {
        if (__instance is OtherworldTreasures.Scripts.Powers.BambooCopterFlightPower)
            __result = "res://OtherworldTreasures/images/powers/Bamboo_Copter_Flight.jpg";
    }
}

[HarmonyPatch(typeof(PowerModel), "IconPath", MethodType.Getter)]
public static class Patch_PowerModel_IconPath_BambooCopter
{
    static void Postfix(PowerModel __instance, ref string __result)
    {
        if (__instance is OtherworldTreasures.Scripts.Powers.BambooCopterFlightPower)
            __result = "res://OtherworldTreasures/images/powers/Bamboo_Copter_Flight.jpg";
    }
}

[HarmonyPatch(typeof(PowerModel), "BigIconPath", MethodType.Getter)]
public static class Patch_PowerModel_BigIconPath_BambooCopter
{
    static void Postfix(PowerModel __instance, ref string __result)
    {
        if (__instance is OtherworldTreasures.Scripts.Powers.BambooCopterFlightPower)
            __result = "res://OtherworldTreasures/images/powers/Bamboo_Copter_Flight_Big.jpg";
    }
}

// === 临时诊断：右键遗物"输入→分发"链路（排查原素瓶战斗中无法使用） ===
// 1) 右键事件是否到达顶栏遗物格；2) 分发是否被守卫条件拦截；3) TryDispatch 结果
public static class DebugRightClickPatches
{
    // Prefix：挂在 RitsuLib internal ModRightClickRelicPatch.TryHandle（静态）
    public static void RelicHolderTryHandlePrefix(NRelicInventoryHolder holder)
    {
        try
        {
            var model = holder.Relic?.Model;
            bool inputHandled = ((Node)holder).GetViewport().IsInputHandled();
            bool inSelection = NTargetManager.Instance.IsInSelection;
            Entry.Logger.Info(
                $"[RC-DEBUG] holder right-click reached: relic={model?.GetType().Name} " +
                $"id={model?.Id} inputHandled={inputHandled} inSelection={inSelection}");
        }
        catch (Exception e)
        {
            Entry.Logger.Info($"[RC-DEBUG] holder prefix error: {e.Message}");
        }
    }

    // Postfix：挂在 public ModRightClickRegistry.TryDispatch
    public static void DispatchPostfix(ModRightClickContext context, ref bool __result)
    {
        Entry.Logger.Info(
            $"[RC-DEBUG] TryDispatch: model={context.Model?.GetType().Name} " +
            $"id={context.Model?.Id} dispatched={__result}");
    }

    // Postfix：挂在 private TryRequestSyncedModelAction——区分"无身份令牌"与"战斗队列忙"
    public static void RequestSyncedPostfix(ModRightClickContext context, bool __result)
    {
        bool inCombat = false;
        int combatState = -1;
        try
        {
            inCombat = MegaCrit.Sts2.Core.Combat.CombatManager.Instance.IsInProgress;
            var runMgr = MegaCrit.Sts2.Core.Runs.RunManager.Instance;
            var syncProp = runMgr?.GetType().GetProperty("ActionQueueSynchronizer");
            var sync = syncProp?.GetValue(runMgr);
            if (sync != null)
            {
                var csProp = sync.GetType().GetProperty("CombatState");
                combatState = Convert.ToInt32(csProp?.GetValue(sync));
            }
        }
        catch (Exception e)
        {
            Entry.Logger.Info($"[RC-DEBUG] combatState reflect error: {e.Message}");
        }

        bool hasToken = false;
        bool sameRefInInventory = false;
        try
        {
            var regType = AccessTools.TypeByName(
                "STS2RitsuLib.Interactions.RightClick.ModModelIdentityRegistry");
            var m = AccessTools.Method(regType, "TryGetToken");
            object[] args = { context.Model, null };
            hasToken = (bool)(m?.Invoke(null, args) ?? false);

            if (context.Model is MegaCrit.Sts2.Core.Models.RelicModel rm && context.Player != null)
            {
                foreach (var r in context.Player.Relics)
                {
                    if (ReferenceEquals(r, rm)) { sameRefInInventory = true; break; }
                }
            }
        }
        catch (Exception e)
        {
            Entry.Logger.Info($"[RC-DEBUG] reflect error: {e.Message}");
        }

        Entry.Logger.Info(
            $"[RC-DEBUG] RequestSynced result={__result} inCombat={inCombat} " +
            $"combatState={combatState}(need 2) hasIdentityToken={hasToken} " +
            $"holderModelIsInventoryInstance={sameRefInInventory}");
    }
}

// === Patch: MapTravel.GetTravelablePointsFrom → 任意门（自由飞行） ===
// 原版「自由旅行」只放开到下一行（GetPointsInRow(row + 1)），否则就是当前点的 Children，
// 且完全不检查目标是否已通过 —— 正常游玩玩家永远在最前沿所以没事；任意门允许回退/跨行后，
// 当前点的 Children 可能包含已通过的汇合节点，原版会把它们当成可前往目标（UI 点亮 → 点击无效）。
// 持有任意门时分两种情况：
//   - 自由传送已开启且有次数：可飞往任意未通过节点（跨行、回头、直飞）；
//     剩余 ≥2 次不做限制（飞错了还有下一次救场），最后 1 次启用死路检测（沿未访问下游到不了 Boss 的不选）。
//   - 自由传送关闭中（含次数用尽）：在原版集合上只做减法 —— 已通过节点永远剔除；死路节点同样剔除。
[HarmonyPatch(typeof(MapTravel), nameof(MapTravel.GetTravelablePointsFrom))]
public static class Patch_MapTravel_AnywhereDoor
{
    static void Postfix(IRunState runState, MapPoint currentPoint, ref IEnumerable<MapPoint> __result)
    {
        try
        {
            if (runState is not RunState concreteState)
            {
                return;
            }
            if (!AnywhereDoor.HasRelic(concreteState))
            {
                return;
            }

            if (AnywhereDoor.HasUsableFreeFlight(concreteState))
            {
                List<MapPoint> freeList = BuildFreeTravelList(concreteState);
                __result = freeList;
                Entry.Logger.Info(
                    $"[AnywhereDoor-Map] 自由传送开启：返回 {freeList.Count} 个未通过节点" +
                    $"(lastChance={AnywhereDoor.IsOnLastChance})，当前行={currentPoint?.coord.row}");
                return;
            }

            // 自由传送关闭中（仍有次数）或次数已用尽：在原版集合上只做减法。
            // 1) 已通过节点不可重进（回退/跨行后 Children 里可能有已访问的汇合节点）；
            // 2) 死路节点不可选（沿未访问下游所有路径都撞进已通过区域、到不了 Boss）。
            var visited = concreteState.VisitedMapCoords.ToHashSet();
            var safeCoords = ComputeReachablePoints(concreteState).safe
                .Select(p => p.coord)
                .ToHashSet();
            // Boss 是终点：连通性分析里 Boss 自身没有下游会被判死路，永远豁免
            safeCoords.Add(concreteState.Map.BossMapPoint.coord);
            if (concreteState.Map.SecondBossMapPoint != null)
            {
                safeCoords.Add(concreteState.Map.SecondBossMapPoint.coord);
            }

            int beforeCount = __result.Count();
            var filtered = __result
                .Where(p => !visited.Contains(p.coord) && safeCoords.Contains(p.coord))
                .ToList();
            __result = filtered;
            int removed = beforeCount - filtered.Count;
            if (removed > 0)
            {
                Entry.Logger.Info(
                    $"[AnywhereDoor-Map] 自由传送关闭：剔除 {removed} 个已通过/死路节点，" +
                    $"保留 {filtered.Count} 个，remaining={AnywhereDoor.RemainingUses}");
            }
        }
        catch (Exception e)
        {
            Entry.Logger.Info($"[AnywhereDoor-Map] EXCEPTION: {e}");
        }
    }

    // 自由飞行可去的节点列表：还有备用次数时=所有未通过节点；只剩最后一次时=再过死路过滤。
    // 供 GetTravelablePointsFrom 与 NMapScreen.RecalculateTravelability 两处共用。
    internal static List<MapPoint> BuildFreeTravelList(RunState state)
    {
        var visited = state.VisitedMapCoords.ToHashSet();
        if (AnywhereDoor.IsOnLastChance)
        {
            return ComputeReachablePoints(state).safe;
        }
        return state.Map.GetAllMapPoints()
            .Where(p => !visited.Contains(p.coord))
            .ToList();
    }

    // 死路检测（仅最后一次使用）：
    // 安全节点 = Boss/二阶段Boss，或存在某个「未访问」的下游子节点本身安全。
    // 已访问节点视为不可穿过的墙；递归记忆化标记整张图。
    internal static (List<MapPoint> safe, int deadCount) ComputeReachablePoints(RunState state)
    {
        ActMap map = state.Map;
        var visited = state.VisitedMapCoords.ToHashSet();
        List<MapPoint> all = map.GetAllMapPoints().ToList();
        var byCoord = all.ToDictionary(p => p.coord);

        var goals = new HashSet<MapCoord> { map.BossMapPoint.coord };
        if (map.SecondBossMapPoint != null)
        {
            goals.Add(map.SecondBossMapPoint.coord);
        }

        var memo = new Dictionary<MapCoord, bool>();
        bool IsSafe(MapPoint p, HashSet<MapCoord> stack)
        {
            if (memo.TryGetValue(p.coord, out bool cached))
            {
                return cached;
            }
            if (stack.Contains(p.coord)) // 环路保护（地图正常为 DAG）
            {
                return false;
            }
            stack.Add(p.coord);
            bool result = false;
            foreach (MapPoint child in p.Children)
            {
                if (goals.Contains(child.coord))
                {
                    result = true;
                    break;
                }
                if (visited.Contains(child.coord))
                {
                    continue; // 已到达节点是墙，不能穿过
                }
                if (byCoord.TryGetValue(child.coord, out MapPoint? childPoint)
                    && IsSafe(childPoint, stack))
                {
                    result = true;
                    break;
                }
            }
            stack.Remove(p.coord);
            memo[p.coord] = result;
            return result;
        }

        var safe = new List<MapPoint>();
        int deadCount = 0;
        foreach (MapPoint p in all)
        {
            if (visited.Contains(p.coord))
            {
                continue;
            }
            if (IsSafe(p, new HashSet<MapCoord>()))
            {
                safe.Add(p);
            }
            else
            {
                deadCount++;
            }
        }
        return (safe, deadCount);
    }
}

// === Patch: 结束回合按钮位置自愈 ===
// 原版 NEndTurnButton 只在 Hidden→Enabled 时播放入场动画（见其 SetState），
// 若状态机停在"已飞出屏幕"，之后就不会再飞回来：
//   - 停在 Hidden：按钮错过了本场战斗的回合开始事件
//   - 停在 Enabled/Disabled：图标已飞出屏幕但状态不是 Hidden，不会再播入场动画
// 【如果电话亭】重开战斗会间歇性撞上这两种情况，表现为回合按钮消失、无法结束回合。
// 兜底：玩家回合内若按钮仍在屏幕外（且没有正在进行的入场动画）→ 按状态补一次。
[HarmonyPatch(typeof(NEndTurnButton), nameof(NEndTurnButton.RefreshEnabled))]
public static class Patch_NEndTurnButton_Reshow
{
    static void Postfix(NEndTurnButton __instance) => EndTurnButtonFixer.EnsureVisible();
}

internal static class EndTurnButtonFixer
{
    private static readonly FieldInfo? StateField = AccessTools.Field(typeof(NEndTurnButton), "_state");
    private static readonly FieldInfo? ShowPosRatioField = AccessTools.Field(typeof(NEndTurnButton), "_showPosRatio");
    private static readonly FieldInfo? PositionTweenField = AccessTools.Field(typeof(NEndTurnButton), "_positionTween");
    private static readonly MethodInfo? AnimInMethod = AccessTools.Method(typeof(NEndTurnButton), "AnimIn");
    private static readonly MethodInfo? OnTurnStartedMethod = AccessTools.Method(typeof(NEndTurnButton), "OnTurnStarted");

    // 状态枚举：0 = Enabled，1 = Disabled，2 = Hidden
    private const int StateEnabled = 0;
    private const int StateHidden = 2;

    public static void EnsureVisible()
    {
        try
        {
            // 战斗收尾阶段按钮本就该飞出屏幕，不插手
            if (!CombatManager.Instance.IsInProgress)
            {
                return;
            }
            var combatState = CombatManager.Instance.DebugOnlyGetState();
            if (combatState == null || combatState.CurrentSide != CombatSide.Player)
            {
                return;
            }
            var button = NCombatRoom.Instance?.Ui?.EndTurnButton;
            if (button == null || StateField == null || ShowPosRatioField == null || AnimInMethod == null)
            {
                return;
            }
            var viewport = button.GetViewport();
            if (viewport == null)
            {
                return;
            }
            Vector2 showPos = (Vector2)ShowPosRatioField.GetValue(null)! * viewport.GetVisibleRect().Size;
            if (button.Position.Y <= showPos.Y + 1f)
            {
                return; // 已在屏幕内
            }
            // 入场动画正在播放时不打扰
            if (PositionTweenField?.GetValue(button) is Tween tween && tween.IsValid() && tween.IsRunning())
            {
                return;
            }

            int state = Convert.ToInt32(StateField.GetValue(button));
            if (state == StateHidden && OnTurnStartedMethod != null)
            {
                // 按钮停留在"未收到回合开始"的初始状态：补发一次原版回合开始处理
                OnTurnStartedMethod.Invoke(button, new object[] { combatState });
                Entry.Logger.Info("[EndTurnButtonFix] 按钮停留在屏幕外（Hidden），已补发回合开始");
            }
            else if (state == StateEnabled)
            {
                AnimInMethod.Invoke(button, null);
                Entry.Logger.Info($"[EndTurnButtonFix] 按钮停留在屏幕外（Enabled），已补播入场动画 (was={button.Position}, target={showPos})");
            }
        }
        catch (Exception e)
        {
            Entry.Logger.Info($"[EndTurnButtonFix] EXCEPTION: {e}");
        }
    }
}

// === 第三方模组异常防护 ===
// 有些模组会把自己挂到原版回合事件（如 CombatManager.TurnStarted）上，却不处理"战斗 UI 被重建"
// 的情况（换房间、【如果电话亭】重开战斗都会重建战斗 UI），于是持有已释放的节点。
// 它们抛出的异常会沿着事件/Hook 冒泡进回合循环内部 —— 原版对此的处理是
// 记录 "the combat is stuck until the room is restarted" 并终止回合循环，表现为：点结束回合没反应、只能用重开房间救回。
// 这里对已知会出问题的第三方处理器挂 Finalizer：只吞掉异常、不改其逻辑，保证回合循环不被第三方异常打死。
internal static class ThirdPartyGuards
{
    // Finalizer 返回 null 表示吞掉异常
    internal static Exception? SwallowTurnLoopKiller(Exception __exception)
    {
        if (__exception != null)
        {
            Entry.Logger.Info(
                $"[Guard] 已拦截第三方模组异常，避免打死回合循环：{__exception.GetType().Name}: {__exception.Message}");
        }
        return null;
    }
}

// === Patch: 目标选择期间点击遗物格不打开遗物介绍 ===
// 原版 NRelicInventory.OnRelicClicked 会在点击顶部遗物格时打开遗物介绍界面。
// 【时光布】的目标选择允许把遗物当目标（点击遗物格 = 选目标），此时必须拦掉这个打开行为，
// 否则选目标的同时会弹出遗物介绍。拦截只在我们自己的目标选择期间生效，不影响正常点击。
[HarmonyPatch(typeof(NRelicInventory), "OnRelicClicked")]
public static class Patch_NRelicInventory_NoInspectWhileTargeting
{
    static bool Prefix()
    {
        if (!TimeClothState.IsTargeting)
        {
            return true;
        }
        Entry.Logger.Info("[TimeCloth] 目标选择中，已拦截遗物介绍界面");
        return false;
    }
}

// === Patch: 地图节点可旅行性重算后，补上任意门的自由飞行目标 ===
// 原版 NMapScreen.RecalculateTravelability 有两种提前返回：
//   - 当前所在行是地图最后一行（Boss 前那一行）→ 只点亮 Boss
//   - 已经在二阶段 Boss 点 → 只点亮二阶段 Boss
// 这两种情况下根本不会调用 GetTravelablePointsFrom，上面那条自由飞行补丁就永远没机会生效，
// 表现为"用任意门飞到 Boss 前那一行之后，地图上就只剩 Boss 能点，其他节点再也点不动"。
// 这里在重算之后补一次：持有且还有次数的【任意门】时，把可去的未通过节点重新点亮。
[HarmonyPatch(typeof(NMapScreen), "RecalculateTravelability")]
public static class Patch_NMapScreen_FreeTravelPoints
{
    private static readonly FieldInfo? RunStateField = AccessTools.Field(typeof(NMapScreen), "_runState");
    private static readonly FieldInfo? MapPointDictField = AccessTools.Field(typeof(NMapScreen), "_mapPointDictionary");

    // 原版重算可前往节点是私有的：右键开关自由传送时要在"地图正开着"的情况下立刻生效，
    // 否则玩家得关掉地图再打开一次才看得到变化。
    private static readonly MethodInfo? RecalculateMethod = AccessTools.Method(typeof(NMapScreen), "RecalculateTravelability");

    // 兜底纯淡入淡出时长：只有角色专属转场材质缺失时（如 mod 角色）才用，短一点不拖沓
    private const float FallbackFadeSeconds = 0.35f;

    // 角色专属贴图擦除转场时长，完全照原版"继续游戏 / 选择角色开始"的 0.8s 节奏
    private const float CharacterTransitionSeconds = 0.8f;

    /// <summary>
    /// 取当前存档角色的专属转场（与"继续游戏、选择角色开始"是同一套贴图擦除转场 + wipe 音效）。
    /// 材质不存在（如 mod 角色）时返回 null，调用方退回纯淡入淡出。
    /// </summary>
    private static (string Path, string Sfx)? GetCharacterTransition(NMapScreen mapScreen)
    {
        if (RunStateField?.GetValue(mapScreen) is not RunState runState || runState.Players.Count == 0)
        {
            return null;
        }
        var character = runState.Players[0].Character;
        string path = character.CharacterSelectTransitionPath;
        if (!ResourceLoader.Exists(path))
        {
            return null;
        }
        return (path, character.CharacterTransitionSfx);
    }

    /// <summary>
    /// 地图正开着时，就地重新点亮/取消可前往的节点。
    /// 转场与原版"继续游戏 / 选择角色开始"完全同款：先播角色专属 wipe 音效，
    /// 再用角色专属贴图遮罩擦除盖住屏幕（texture_transition 着色器），重算后用普通淡入揭幕。
    /// 角色材质缺失时退回原版默认的纯淡入淡出。
    /// 地图没开就什么都不做——下次打开时原版本来就会重算。
    /// </summary>
    public static async Task RefreshIfOpenAsync()
    {
        try
        {
            var mapScreen = NMapScreen.Instance;
            if (mapScreen == null || !mapScreen.IsOpen || RecalculateMethod == null)
            {
                return;
            }
            var transition = NGame.Instance?.Transition;
            var charTransition = GetCharacterTransition(mapScreen);
            if (transition != null)
            {
                if (charTransition is { } ct)
                {
                    // 与原版 NMainMenu.OnContinueButtonPressedAsync 同一套：wipe 音效 + 角色贴图擦除
                    SfxCmd.Play(ct.Sfx);
                    await transition.FadeOut(CharacterTransitionSeconds, ct.Path);
                }
                else
                {
                    await transition.FadeOut(FallbackFadeSeconds);
                }
            }
            try
            {
                RecalculateMethod.Invoke(mapScreen, null);
            }
            catch (Exception e)
            {
                Entry.Logger.Info($"[AnywhereDoor-Map] 就地重算失败：{e.Message}");
            }
            if (transition != null)
            {
                // 原版揭幕一律是普通淡入（FadeIn 默认 fade_transition_mat），不用贴图
                if (charTransition != null)
                {
                    await transition.FadeIn();
                }
                else
                {
                    await transition.FadeIn(FallbackFadeSeconds);
                }
            }
            Entry.Logger.Info($"[AnywhereDoor-Map] 地图开着：就地重算可前往节点（转场：{charTransition?.Path ?? "纯淡入淡出"}）");
        }
        catch (Exception e)
        {
            Entry.Logger.Info($"[AnywhereDoor-Map] RefreshIfOpen 失败（已忽略）：{e.Message}");
        }
    }

    static void Postfix(NMapScreen __instance)
    {
        try
        {
            if (RunStateField?.GetValue(__instance) is not RunState runState)
            {
                return;
            }
            // 没持有任意门就完全不插手，保持原版行为
            if (!AnywhereDoor.HasRelic(runState))
            {
                return;
            }
            if (MapPointDictField?.GetValue(__instance) is not Dictionary<MapCoord, NMapPoint> pointNodes)
            {
                return;
            }
            // 还没离开起点（新 run）时与原版 else 分支对齐，不插手
            IReadOnlyList<MapCoord> visitedCoords = runState.VisitedMapCoords;
            if (visitedCoords.Count == 0)
            {
                return;
            }

            var visited = visitedCoords.ToHashSet();
            var bossCoords = new HashSet<MapCoord> { runState.Map.BossMapPoint.coord };
            if (runState.Map.SecondBossMapPoint != null)
            {
                bossCoords.Add(runState.Map.SecondBossMapPoint.coord);
            }

            // (1) 已通过节点绝不允许点击。
            // 原版 RecalculateTravelability 先把 visited 设成 Traveled，随后又把当前点的 Children
            // 无条件覆盖成 Travelable —— 任意门回退/跨行后 Children 里可能有已通过的汇合节点，
            // 被覆盖点亮后能点击但投票移动无效。这里把它们一律恢复为 Traveled。
            foreach (MapCoord coord in visited)
            {
                if (pointNodes.TryGetValue(coord, out NMapPoint? traveledNode)
                    && traveledNode.State == MapPointState.Travelable)
                {
                    traveledNode.State = MapPointState.Traveled;
                }
            }

            // (2) 本状态下真正允许前往的坐标白名单。
            // 复用数据层补丁（MapTravel.GetTravelablePointsFrom 已按开启/关闭状态过滤好）。
            // 原版两种提前返回（Boss 前一行 / 已在二阶段 Boss 点）根本不会调用它，所以这里独立取一次；
            // Boss 点永远在白名单内。
            var allowed = new HashSet<MapCoord>(bossCoords);
            MapPoint? currentPoint = runState.Map.GetPoint(visitedCoords[visitedCoords.Count - 1]);
            if (currentPoint != null)
            {
                foreach (MapPoint point in MapTravel.GetTravelablePointsFrom(runState, currentPoint))
                {
                    allowed.Add(point.coord);
                }
            }

            // (3) 白名单校正：已通过节点保持 Traveled；其余节点允许的点亮、不允许的（死路等）灭灯。
            int lit = 0;
            int blocked = 0;
            foreach (var (coord, node) in pointNodes)
            {
                if (visited.Contains(coord))
                {
                    continue;
                }
                if (allowed.Contains(coord))
                {
                    if (node.State != MapPointState.Travelable)
                    {
                        node.State = MapPointState.Travelable;
                        lit++;
                    }
                }
                else if (node.State == MapPointState.Travelable)
                {
                    node.State = MapPointState.Untravelable;
                    blocked++;
                }
            }
            if (lit > 0 || blocked > 0)
            {
                Entry.Logger.Info(
                    $"[AnywhereDoor-Map] 节点校正：点亮 {lit} 个、拦截 {blocked} 个（已通过/死路），" +
                    $"freeFlight={AnywhereDoor.HasUsableFreeFlight(runState)}, remaining={AnywhereDoor.RemainingUses}");
            }
        }
        catch (Exception e)
        {
            Entry.Logger.Info($"[AnywhereDoor-Map] RecalculateTravelability patch EXCEPTION: {e}");
        }
    }
}

// === Patch: 让顶部遗物图标在"目标选择"期间可被悬停/点选（【时光布】的目标选择用） ===
// 原版只有 NCreature / NMultiplayerPlayerState / 商人 / 篝火角色会主动调用 NTargetManager.OnNodeHovered，
// 遗物图标不会；这里补上转发，使时光布的目标选择可以点到遗物。
[HarmonyPatch(typeof(NRelicInventoryHolder), "_Ready")]
public static class Patch_NRelicInventoryHolder_TargetingHover
{
    static void Postfix(NRelicInventoryHolder __instance)
    {
        __instance.MouseEntered += () => NTargetManager.Instance?.OnNodeHovered(__instance);
        __instance.MouseExited += () => NTargetManager.Instance?.OnNodeUnhovered(__instance);
    }
}

// === Patch 8: 遗物图鉴新增「哆啦A梦的道具」栏 ===
// 原版图鉴的分类是场景里固定的 7 个节点，按 RelicRarity 逐个填充。
// 这里在「事件」栏加载完成后动态插入一栏，用自定义稀有度筛出哆啦A梦道具。
[HarmonyPatch(typeof(NRelicCollectionCategory), nameof(NRelicCollectionCategory.LoadRelics))]
public static class Patch_NRelicCollectionCategory_AddDoraemonSection
{
    private const string SectionName = "OtherworldTreasuresDoraemon";

    static void Postfix(
        NRelicCollectionCategory __instance,
        RelicRarity relicRarity,
        NRelicCollection collection,
        HashSet<RelicModel> seenRelics,
        UnlockState unlockState,
        HashSet<RelicModel> allUnlockedRelics)
    {
        // 只在「事件」栏之后插入一次
        if (relicRarity != RelicRarity.Event)
        {
            return;
        }
        try
        {
            var parent = __instance.GetParent();
            if (parent == null)
            {
                return;
            }
            // LoadRelics 会被多次调用（图鉴刷新/重进），已插入过则跳过，避免重复出现两栏
            if (parent.GetChildren().OfType<NRelicCollectionCategory>().Any(c => c.Name == SectionName))
            {
                return;
            }
            var scene = PreloadManager.Cache.GetScene(NRelicCollectionCategory.scenePath);
            var section = scene.Instantiate<NRelicCollectionCategory>(PackedScene.GenEditState.Disabled);
            section.Name = SectionName;
            parent.AddChild(section);
            parent.MoveChild(section, __instance.GetIndex() + 1);
            section.LoadRelics(
                ModRelicRarity.Doraemon,
                collection,
                new LocString("relic_collection", "DORAEMON_ITEMS"),
                seenRelics,
                unlockState,
                allUnlockedRelics);
            Entry.Logger.Info("[Collection] Inserted Doraemon items section");
        }
        catch (Exception e)
        {
            Entry.Logger.Info($"[Collection] Insert Doraemon section failed: {e}");
        }
    }
}

// === Patch 9: 自定义稀有度「哆啦A梦」的配色 ===
// 原版 SetRarityVisuals 的 default 分支会 throw，必须拦掉。
// 稀有度文本由 gameplay_ui.RELIC_RARITY.99 提供，这里只把配色映射到「事件」的绿色。
[HarmonyPatch(typeof(NInspectRelicScreen), "SetRarityVisuals")]
public static class Patch_NInspectRelicScreen_SetRarityVisuals
{
    static void Prefix(ref RelicRarity rarity)
    {
        if (rarity == ModRelicRarity.Doraemon)
        {
            rarity = RelicRarity.Event;
        }
    }
}

// === Patch 10: 金苹果 / 附魔金苹果 —— 商店专属投放 ===
// 原版药水没有"商店专属"标志，所有来源都走 PotionFactory.GetPotionOptions；
// 这里把金苹果从通用池摘掉，再在商店库存里显式上架一瓶。
[HarmonyPatch(typeof(PotionFactory), nameof(PotionFactory.GetPotionOptions))]
public static class Patch_PotionFactory_ExcludeShopOnly
{
    static void Postfix(ref IEnumerable<PotionModel> __result)
    {
        __result = __result.Where(p => !HiddenPotions.IsHidden(p));
    }
}

[HarmonyPatch(typeof(MerchantInventory), "PopulatePotionEntries")]
public static class Patch_MerchantInventory_StockGoldenApple
{
    // 金苹果在商店的出现概率（它是商店专属，这里是唯一的投放渠道，所以别设太高）
    private const double StockChance = 0.3;

    static void Postfix(MerchantInventory __instance)
    {
        if (HiddenPotions.GoldenApplesHidden)
        {
            return;
        }
        try
        {
            var entries = __instance.PotionEntries;
            if (entries.Count == 0)
            {
                return;
            }
            var entry = entries[0];
            if (entry.Model is GoldenApple)
            {
                return;
            }

            // 按概率投放，否则每次进商店都会必有一瓶
            if (System.Random.Shared.NextDouble() >= StockChance)
            {
                return;
            }

            var canonical = ModelDb.Potion<GoldenApple>();
            var modelProperty = typeof(MerchantPotionEntry)
                .GetProperty("Model", BindingFlags.Instance | BindingFlags.Public);
            if (modelProperty == null)
            {
                return;
            }
            modelProperty.SetValue(entry, canonical.ToMutable());
            entry.CalcCost();
            Entry.Logger.Info("[GoldenApple] Stocked in merchant");
        }
        catch (Exception e)
        {
            Entry.Logger.Info($"[GoldenApple] Stock in merchant failed: {e.Message}");
        }
    }
}

// 金苹果定价 150（原版价格按稀有度写死：Rare 100 / Uncommon 75 / 其他 50）
[HarmonyPatch(typeof(MerchantEntry), "Cost", MethodType.Getter)]
public static class Patch_MerchantEntry_Cost_GoldenApple
{
    static void Postfix(MerchantEntry __instance, ref int __result)
    {
        if (__instance is MerchantPotionEntry entry && entry.Model is GoldenApple)
        {
            __result = 150;
        }
    }
}

// === Patch 12: 自定义药水稀有度「极稀有」的显示 ===
// 原版 PotionRarityExtensions.ToLocString 的 default 分支会抛 SwitchExpressionException，
// 必须拦掉，否则渲染药水稀有度时直接崩。
[HarmonyPatch(typeof(PotionRarityExtensions), nameof(PotionRarityExtensions.ToLocString))]
public static class Patch_PotionRarityExtensions_ToLocString
{
    static bool Prefix(PotionRarity potionRarity, ref LocString __result)
    {
        if (potionRarity != ModPotionRarity.VeryRare)
        {
            return true;
        }
        __result = new LocString("gameplay_ui", "POTION_RARITY.VERY_RARE");
        return false;
    }
}

// === Patch 13: 药水图鉴新增「极稀有」栏 ===
// 图鉴分类是场景里固定的 4 个节点（普通/罕见/稀有/特殊），自定义稀有度不属于任何一栏。
// 这里在「特殊」栏（Event + Token，四栏中最后加载）之后，用同一场景实例化并插入一栏。
[HarmonyPatch(typeof(NPotionLabCategory), nameof(NPotionLabCategory.LoadPotions))]
public static class Patch_NPotionLabCategory_AddVeryRareSection
{
    private const string SectionName = "OtherworldTreasuresVeryRare";

    static void Postfix(
        NPotionLabCategory __instance,
        PotionRarity potionRarity,
        HashSet<PotionModel> seenPotions,
        UnlockState unlockState,
        HashSet<PotionModel> allUnlockedPotions)
    {
        // 「特殊」栏是最后一个加载的分类，用它作为插入锚点
        if (potionRarity != PotionRarity.Event)
        {
            return;
        }
        // 「极稀有」栏目前只放金苹果/附魔金苹果这两瓶，它们被隐藏时整栏不显示（图鉴里看不到）
        if (HiddenPotions.GoldenApplesHidden)
        {
            return;
        }
        try
        {
            var parent = __instance.GetParent();
            if (parent == null)
            {
                return;
            }
            if (parent.GetChildren().OfType<NPotionLabCategory>().Any(c => c.Name == SectionName))
            {
                return;
            }

            var scenePath = __instance.SceneFilePath;
            if (string.IsNullOrEmpty(scenePath))
            {
                Entry.Logger.Info("[PotionLab] Insert 极稀有 section failed: empty SceneFilePath");
                return;
            }

            var section = PreloadManager.Cache.GetScene(scenePath)
                .Instantiate<NPotionLabCategory>(PackedScene.GenEditState.Disabled);
            section.Name = SectionName;
            parent.AddChild(section);
            parent.MoveChild(section, __instance.GetIndex() + 1);
            section.LoadPotions(
                ModPotionRarity.VeryRare,
                new LocString("potion_lab", "VERY_RARE"),
                seenPotions,
                unlockState,
                allUnlockedPotions);
            Entry.Logger.Info("[PotionLab] Inserted 极稀有 section");
        }
        catch (Exception e)
        {
            Entry.Logger.Info($"[PotionLab] Insert 极稀有 section failed: {e.Message}");
        }
    }
}

// === Patch 11: 我们药水的名称与描述（PotionModel.Title/Description 不是 virtual，只能拦 getter） ===
[HarmonyPatch(typeof(PotionModel), "Title", MethodType.Getter)]
public static class Patch_PotionModel_Title
{
    static void Postfix(PotionModel __instance, ref LocString __result)
    {
        if (__instance is GoldenApple)
        {
            __result = new LocString("potions", "OTHERWORLD_TREASURES_POTION_GOLDEN_APPLE.title");
        }
        else if (__instance is EnchantedGoldenApple)
        {
            __result = new LocString("potions", "OTHERWORLD_TREASURES_POTION_ENCHANTED_GOLDEN_APPLE.title");
        }
    }
}

[HarmonyPatch(typeof(PotionModel), "Description", MethodType.Getter)]
public static class Patch_PotionModel_Description
{
    static void Postfix(PotionModel __instance, ref LocString __result)
    {
        if (__instance is GoldenApple)
        {
            __result = new LocString("potions", "OTHERWORLD_TREASURES_POTION_GOLDEN_APPLE.description");
        }
        else if (__instance is EnchantedGoldenApple)
        {
            __result = new LocString("potions", "OTHERWORLD_TREASURES_POTION_ENCHANTED_GOLDEN_APPLE.description");
        }
    }
}

// === Patch 14: 我们植物的站位 ===
// 随从的站位其实在 NCombatRoom.AddCreature(Creature) 里：原版把"非骨手随从"统一摆在
//   位置 = 玩家位置 + (植物半宽 - 20, +10)
// 所以我们的植物会和玩家/骨手挤在一起。这里在它之后重新摆：
//   坚果墙 → 站在所有随从最前（有骨手就摆到骨手前面）  豌豆射手 / 向日葵 → 后移下移约半个身位，让骨手露出来
// 顺带把骨手血条的 ZIndex 提上去，避免被我们的植物盖住。
[HarmonyPatch(typeof(NCombatRoom), nameof(NCombatRoom.AddCreature))]
public static class Patch_NCombatRoom_PlantLayout
{
    // 骨手血条的 ZIndex：只要比我们的植物高一层就够了（植物形象与血条都是默认的 0）
    private const int OstyHealthBarZIndex = 1;

    static void Postfix(Creature creature)
    {
        try
        {
            if (creature.Monster is not PlantSummonBase plant)
            {
                return;
            }
            var room = NCombatRoom.Instance;
            var plantNode = room?.GetCreatureNode(creature);
            var owner = creature.PetOwner;
            var playerNode = owner == null ? null : room?.GetCreatureNode(owner.Creature);
            if (room == null || plantNode == null || playerNode == null)
            {
                return;
            }

            var ostyNode = room.CreatureNodes.FirstOrDefault(c => c.Entity.Monster is Osty);

            // 骨手的血条置顶（我们的植物后加入节点树，会盖住它）
            if (ostyNode?.GetNodeOrNull<Control>("%HealthBar") is { } ostyBar)
            {
                ostyBar.ZIndex = OstyHealthBarZIndex;
            }

            plantNode.Position = playerNode.Position + plant.LayoutOffset;

            Entry.Logger.Info(
                $"[Plant] 站位: {plant.GetType().Name} 偏移={plant.LayoutOffset} 玩家={playerNode.Position} 植物={plantNode.Position} 骨手={ostyNode?.Position.ToString() ?? "无"}");
        }
        catch (Exception e)
        {
            Entry.Logger.Info($"[Plant] 站位调整失败（已忽略）：{e.Message}");
        }
    }
}

// === Patch 14: 受击特效落点 ===
// 原版攻击的 HitVfx 打在"攻击目标"（玩家）身上，但这一击实际由我们的植物承受，
// 所以把受击特效的落点改到植物身上（玩家不再有受击表现；骨手保持原版表现）。
[HarmonyPatch(typeof(VfxCmd), nameof(VfxCmd.PlayOnCreature))]
public static class Patch_VfxCmd_PlayOnCreature_HitRedirect
{
    static void Prefix(ref Creature target, string path) => PlantSummonBase.RedirectHitVfx(ref target, path);
}

// PlayOnCreatures / PlayOnCreatureCenters 内部是循环调用下面这个单目标方法，所以只需挂这一处
[HarmonyPatch(typeof(VfxCmd), nameof(VfxCmd.PlayOnCreatureCenter))]
public static class Patch_VfxCmd_PlayOnCreatureCenter_HitRedirect
{
    static void Prefix(ref Creature target, string path) => PlantSummonBase.RedirectHitVfx(ref target, path);
}
