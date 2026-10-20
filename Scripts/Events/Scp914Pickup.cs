using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Relics;
using STS2RitsuLib.Interactions.RightClick;

namespace OtherworldTreasures.Scripts.Events;

// === 2026-10-17 SCP-914「遗物投料」改造（主动改造，不是故障） ===
// 旧做法：把遗物塞进原版"选择一件遗物"界面。遗物一多就单行溢出屏幕，而且取消会卡死。
// 新做法：不弹任何选择界面，玩家直接在顶部遗物栏点一件投入。
// 为此需要在"投料状态"下临时接管遗物栏：
//   - 左键：不再打开遗物详情，改成交给 SCP-914；
//   - 右键：吞掉，模组自带的右键效果（时光布/任意门/全能表/灰烬瓶……）一个都不触发；
//   - 悬停：放大更明显，提示玩家"这里可以点"；
//   - 品质：每件遗物套一圈品质色方框（呼吸），遗物栏下方再加一个上指箭头做引导。
// 状态只由 SCP_914 事件开关，离开投料页、取消、事件结束都会解除。
internal static partial class Scp914Pickup
{
    private sealed class Frame
    {
        public Control Node = null!;

        public Tween Tween = null!;
    }

    // 方框外扩留白与线宽（像素）。玩家 2026-10-17 要求"方形边框、可以粗一点"。
    private const float FrameMargin = 3f;

    private const float FrameThickness = 4f;

    private static readonly List<NRelicInventoryHolder> Holders = new();

    private static readonly Dictionary<NRelicInventoryHolder, Frame> Frames = new();

    private static Scp914? _armed;

    internal static bool IsActive => _armed != null;

    internal static void Track(NRelicInventoryHolder holder)
    {
        Holders.RemoveAll(h => !GodotObject.IsInstanceValid(h));
        if (!Holders.Contains(holder))
        {
            Holders.Add(holder);
        }
        if (IsActive)
        {
            Highlight(holder);
        }
    }

    internal static void Arm(Scp914 scp)
    {
        _armed = scp;
        try
        {
            for (int i = Holders.Count - 1; i >= 0; i--)
            {
                if (GodotObject.IsInstanceValid(Holders[i]))
                {
                    Highlight(Holders[i]);
                }
                else
                {
                    Holders.RemoveAt(i);
                }
            }
        }
        catch (Exception e)
        {
            Entry.Logger.Warn($"[SCP914-Pickup] 遗物栏引导创建失败（仅提示）：{e}");
        }
    }

    internal static void Disarm()
    {
        _armed = null;
        ClearHighlights();
    }

    internal static void TryPick(RelicModel relic) => _armed?.OnRelicPickedFromBar(relic);

    // 一件遗物的引导：图标外圈套一个品质色方框并呼吸。图标本身的颜色和缩放都不动。
    internal static void Highlight(NRelicInventoryHolder holder)
    {
        try
        {
            if (!GodotObject.IsInstanceValid(holder) || Frames.ContainsKey(holder))
            {
                return;
            }
            var icon = holder.Relic.Icon;
            if (icon == null)
            {
                return;
            }
            Color color = Scp914Colors.Of(holder.Relic.Model.Rarity);
            var node = new RarityFrame
            {
                LineColor = color,
                Thickness = FrameThickness,
                Position = icon.Position - Vector2.One * FrameMargin,
                Size = icon.Size + Vector2.One * (FrameMargin * 2f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            holder.AddChild(node);
            // 画在图标之上，方框正好压住图标边缘一圈。
            holder.MoveChild(node, icon.GetIndex() + 1);
            Tween tween = node.CreateTween().SetLoops();
            tween.TweenProperty(node, "modulate:a", 0.45f, 0.6f)
                .SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Sine);
            tween.TweenProperty(node, "modulate:a", 1f, 0.6f)
                .SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Sine);
            Frames[holder] = new Frame { Node = node, Tween = tween };
        }
        catch (Exception e)
        {
            Entry.Logger.Warn($"[SCP914-Pickup] 单件遗物方框失败：{e}");
        }
    }

    private static void ClearHighlights()
    {
        foreach (KeyValuePair<NRelicInventoryHolder, Frame> pair in Frames)
        {
            pair.Value.Tween?.Kill();
            if (GodotObject.IsInstanceValid(pair.Value.Node))
            {
                pair.Value.Node.QueueFree();
            }
        }
        Frames.Clear();
    }

    // 品质色方框：只描边不填充。
    private sealed partial class RarityFrame : Control
    {
        internal Color LineColor = Colors.White;

        internal float Thickness = 4f;

        public override void _Draw()
        {
            var rect = new Rect2(Vector2.Zero, Size).Grow(-Thickness * 0.5f);
            DrawRect(rect, LineColor, false, Thickness);
        }
    }
}

// 投入期间先于模组遗物自身的右键处理（它们优先级默认 0），把右键整个吞掉。
internal sealed class Scp914PickupRightClick : IModRightClickHandler
{
    internal static readonly Scp914PickupRightClick Instance = new();

    public int Priority => 1000;

    public bool TryHandle(ModRightClickContext context) => Scp914Pickup.IsActive;
}

// 左键接管：NRelicInventory.OnRelicClicked 是原版"点击遗物栏图标→打开遗物详情"的入口。
[HarmonyPatch(typeof(NRelicInventory), "OnRelicClicked")]
internal static class Patch_NRelicInventory_PickupForScp914
{
    static bool Prefix(RelicModel model)
    {
        if (!Scp914Pickup.IsActive) return true;
        Scp914Pickup.TryPick(model);
        // 投入状态下不论点到什么遗物，都不再弹出遗物详情界面。
        return false;
    }
}

// 悬停放大：原版 OnFocus 只放大到 1.25，投料时提高到 1.4 并照常弹出遗物提示。
[HarmonyPatch(typeof(NRelicInventoryHolder), "OnFocus")]
internal static class Patch_NRelicInventoryHolder_PickupHover
{
    private static bool s_warned;

    static bool Prefix(NRelicInventoryHolder __instance)
    {
        if (!Scp914Pickup.IsActive) return true;
        try
        {
            __instance.Relic.Icon.Scale = Vector2.One * 1.4f;
            NHoverTipSet.CreateAndShow(__instance, __instance.Relic.Model.HoverTips)
                ?.SetAlignmentForRelic(__instance.Relic);
        }
        catch (Exception e)
        {
            if (!s_warned)
            {
                s_warned = true;
                Entry.Logger.Warn($"[SCP914-Pickup] 遗物栏悬停放大失败（仅记一次）：{e}");
            }
        }
        return false;
    }
}

// 遗物栏格子建好时登记，投料中途新获得的遗物也能立刻带上框与呼吸引导。
[HarmonyPatch(typeof(NRelicInventoryHolder), "_Ready")]
internal static class Patch_NRelicInventoryHolder_PickupReady
{
    static void Postfix(NRelicInventoryHolder __instance) => Scp914Pickup.Track(__instance);
}
