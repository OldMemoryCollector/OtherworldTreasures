using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using STS2RitsuLib;
using STS2RitsuLib.CardPiles;
using STS2RitsuLib.Interop;
using HarmonyLib;
using Logger = MegaCrit.Sts2.Core.Logging.Logger;

namespace OtherworldTreasures.Scripts;

[ModInitializer(nameof(Init))]
public class Entry
{
    public const string ModId = "OtherworldTreasures";
    public static readonly Logger Logger = RitsuLibFramework.CreateLogger(ModId);

    // 自定义牌堆：除外池（类似消耗堆，但卡牌无法通过常规手段取回）
    public static PileType ExclusionPile;

    public static void Init()
    {
        var assembly = Assembly.GetExecutingAssembly();
        RitsuLibFramework.EnsureGodotScriptsRegistered(assembly, Logger);
        ModTypeDiscoveryHub.RegisterModAssembly(ModId, assembly);

        // Harmony patches (ShouldGlowGold Postfix)
        var harmony = new Harmony("sts2.otherworldtreasures.patches");
        harmony.PatchAll();

        // 临时诊断：右键遗物链路（原素瓶战斗中无法使用，排查后移除）
        var relicPatchType = AccessTools.TypeByName(
            "STS2RitsuLib.Interactions.RightClick.Patches.ModRightClickRelicPatch");
        var tryHandleMethod = AccessTools.Method(relicPatchType, "TryHandle");
        harmony.Patch(tryHandleMethod, prefix: new HarmonyMethod(
            typeof(DebugRightClickPatches), nameof(DebugRightClickPatches.RelicHolderTryHandlePrefix)));
        var dispatchMethod = AccessTools.Method(
            typeof(STS2RitsuLib.Interactions.RightClick.ModRightClickRegistry), "TryDispatch");
        harmony.Patch(dispatchMethod, postfix: new HarmonyMethod(
            typeof(DebugRightClickPatches), nameof(DebugRightClickPatches.DispatchPostfix)));
        var requestMethod = AccessTools.Method(
            typeof(STS2RitsuLib.Interactions.RightClick.ModRightClickRegistry), "TryRequestSyncedModelAction");
        harmony.Patch(requestMethod, postfix: new HarmonyMethod(
            typeof(DebugRightClickPatches), nameof(DebugRightClickPatches.RequestSyncedPostfix)));

        // 第三方模组异常防护：给「皮皮倒带: Rewind」的 RewindButton.OnTurnStarted 挂 Finalizer 吞异常。
        // 它在战斗 UI 被重建后会持有已释放节点并抛 ObjectDisposedException，而该异常抛在回合循环内部，
        // 会把回合循环打死（原版注释：the combat is stuck until the room is restarted），表现为卡死。
        // 找不到该模组就跳过；挂载失败也不影响模组自身加载。
        try
        {
            var rewindButtonType = AccessTools.TypeByName("Rewind.Scripts.RewindButton");
            var rewindOnTurnStarted = rewindButtonType == null
                ? null
                : AccessTools.Method(rewindButtonType, "OnTurnStarted");
            if (rewindOnTurnStarted != null)
            {
                harmony.Patch(rewindOnTurnStarted, finalizer: new HarmonyMethod(
                    typeof(ThirdPartyGuards), nameof(ThirdPartyGuards.SwallowTurnLoopKiller)));
                Logger.Info("[Guard] 已为 Rewind.RewindButton.OnTurnStarted 挂上异常防护");
            }
            else
            {
                Logger.Info("[Guard] 未找到 Rewind 模组，跳过其异常防护");
            }
        }
        catch (Exception e)
        {
            Logger.Info($"[Guard] 挂载 Rewind 异常防护失败（已忽略）：{e}");
        }

        // 注册除外自定义牌堆
        var registry = ModCardPileRegistry.For(ModId);
        ExclusionPile = registry.RegisterOwned("exclusion_pile", new ModCardPileSpec
        {
            Scope = ModCardPileScope.CombatOnly,
            Style = ModCardPileUiStyle.BottomRight,
            // 放在消耗堆按钮正上方（Custom 模式，父控件局部坐标中心对齐）
            // 消耗堆按钮中心约 (1820, 975)，正上方约减 95px → (1820, 880)
            Anchor = ModCardPileAnchor.AtCenter(new Vector2(1820, 880)),
            IconPath = "res://OtherworldTreasures/images/piles/exclusion_pile.svg",
            OnOpen = ctx => ctx.ShowDefaultPileScreen(),
            // 只有有牌进入除外堆时才显示按钮（与消耗堆一致，空堆隐藏）
            VisibleWhen = ctx => ctx.Pile != null && ctx.Pile.Cards.Count > 0,
        }).PileType;

        // 附魔金苹果作为先古之民的选项之一，在 MemoryKeeper.GenerateInitialOptions 里与遗物一起抽取

        // 结束回合按钮自愈：玩家回合开始时延迟一帧兜底检查按钮是否被落在屏幕外
        // （【如果电话亭】重开战斗后按钮会间歇性消失，延迟一帧可避开正常的入场动画）
        CombatManager.Instance.TurnStarted += _ =>
            Callable.From(EndTurnButtonFixer.EnsureVisible).CallDeferred();

        Logger.Info("OtherworldTreasures mod initialized! ExclusionPile registered, Harmony patches applied.");
    }
}
