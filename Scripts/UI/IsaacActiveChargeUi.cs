using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using OtherworldTreasures.Scripts.Relics;

namespace OtherworldTreasures.Scripts.UI;

/// <summary>Shows the charge as a pre-rendered frame from the relic's own strip.</summary>
public partial class ActiveChargeMeter : Control
{
    private NRelic? _owner;
    private int _lastCharge = -1;
    private int _lastMax = -1;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = 0;
        _owner = GetParent() as NRelic;
        SetProcess(true);
        // 不在 _Ready 里刷新：此时 NRelic.Model 尚未赋值，读取会抛 InvalidOperationException。
    }

    public override void _Process(double delta)
    {
        if (_owner == null || !GodotObject.IsInstanceValid(_owner)) return;
        if (!TryGetCharge(out var charge))
        {
            Visible = false;
            return;
        }
        if (GetParent() != _owner.Icon) Reparent(_owner.Icon, keepGlobalTransform: false);
        UpdateLayout();
        if (charge.CurrentCharge != _lastCharge || charge.MaxCharge != _lastMax) Refresh();
    }

    private bool TryGetCharge(out IActiveChargeRelic charge)
    {
        charge = null!;
        if (_owner == null || !GodotObject.IsInstanceValid(_owner)) return false;
        if (_owner.GetParent() is not NRelicInventoryHolder) return false;
        try
        {
            if (_owner.Model is not IActiveChargeRelic model) return false;
            charge = model;
            return true;
        }
        catch (InvalidOperationException)
        {
            // NRelic.Model 尚未赋值（节点刚创建）。
            return false;
        }
    }

    private static Texture2D? TryLoad(string name)
    {
        string path = $"res://OtherworldTreasures/images/ui/{name}.png";
        return ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
    }

    private void Refresh()
    {
        if (!TryGetCharge(out var charge))
        {
            Visible = false;
            return;
        }
        string? sheetName = charge.MaxCharge switch
        {
            4 => "charge/d4_sheet",
            6 => "charge/d6_sheet",
            _ => null,
        };
        var sheet = sheetName == null ? null : TryLoad(sheetName);
        if (sheet == null)
        {
            Visible = false;
            return;
        }
        _lastCharge = charge.CurrentCharge;
        _lastMax = charge.MaxCharge;
        Visible = true;

        foreach (var child in GetChildren())
            if (child is Node node) { RemoveChild(node); node.QueueFree(); }

        int frame = Math.Clamp(charge.CurrentCharge, 0, charge.MaxCharge);
        AddLayer(sheet, new Rect2(frame * 32, 0, 32, 64));
    }

    private void UpdateLayout()
    {
        // Inherit the icon's movement and hover scale instead of caching a global transform.
        Vector2 iconSize = _owner!.Icon.Size;
        float height = iconSize.Y * 0.62f;
        Size = new Vector2(height * 0.5f, height);
        Position = new Vector2(iconSize.X - Size.X - iconSize.X * 0.025f,
            (iconSize.Y - Size.Y) * 0.5f);
        if (GetNodeOrNull<TextureRect>("Frame") is { } frame) frame.Size = Size;
    }

    private void AddLayer(Texture2D sheet, Rect2 region)
    {
        var layer = new TextureRect
        {
            Name = "Frame",
            Texture = new AtlasTexture { Atlas = sheet, Region = region },
            Position = Vector2.Zero,
            Size = Size,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
        };
        AddChild(layer);
    }
}

/// <summary>Shows ten compass relic choices in a fixed two-row grid.</summary>
internal static class CompassRelicSelectionLayout
{
    internal static bool Active;

    [HarmonyPatch(typeof(NChooseARelicSelection), nameof(NChooseARelicSelection._Ready))]
    private static class ChooseRelicScreenPatch
    {
        private static void Postfix(NChooseARelicSelection __instance)
        {
            if (!Active) return;
            try
            {
                // Let the vanilla picker create its holders, callbacks, focus links,
                // banner, skip button and overlay state. Reposition those same nodes
                // only after the native entrance tween has completed.
                var tween = __instance.CreateTween();
                tween.TweenInterval(0.55);
                tween.TweenCallback(Callable.From(() => TryLayoutGrid(__instance)));
            }
            catch (Exception ex)
            {
                Entry.Logger.Warn($"Compass relic grid layout failed; keeping the native picker. {ex}");
            }
        }
    }

    private static void TryLayoutGrid(NChooseARelicSelection screen)
    {
        try { LayoutGrid(screen); }
        catch (Exception ex) { Entry.Logger.Warn($"Compass relic grid layout failed; keeping the native picker. {ex}"); }
    }

    private static void LayoutGrid(NChooseARelicSelection screen)
    {
        if (!GodotObject.IsInstanceValid(screen) || !screen.IsInsideTree()) return;
        var row = screen.GetNode<Control>("RelicRow");
        var holders = row.GetChildren().OfType<NRelicBasicHolder>().ToList();
        if (holders.Count == 0) return;

        void SetGridPositions(bool animate)
        {
            // Holder positions are local to RelicRow, which is offset near the
            // bottom of the screen. Convert the screen center into that space;
            // using screen.Size directly shifts the grid off-screen.
            Vector2 screenCenter = screen.GetGlobalTransformWithCanvas() * (screen.Size * 0.5f);
            Vector2 center = row.GetGlobalTransformWithCanvas().AffineInverse() * screenCenter;
            var tween = animate ? screen.CreateTween().SetParallel() : null;
            for (int i = 0; i < holders.Count; i++)
            {
                var holder = holders[i];
                holder.Scale = Vector2.One * 1.45f;
                int rowIndex = i / 5;
                int col = i % 5;
                int rowStart = rowIndex * 5;
                int rowEnd = Math.Min(rowStart + 5, holders.Count) - 1;
                var destination = new Vector2(center.X + (col - 2) * 215f - 48f,
                    center.Y + (rowIndex == 0 ? -155f : 45f));
                if (tween == null) holder.Position = destination;
                else tween.TweenProperty(holder, "position", destination, 0.2)
                    .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
                holder.FocusNeighborLeft = holders[Math.Max(rowStart, i - 1)].GetPath();
                holder.FocusNeighborRight = holders[Math.Min(rowEnd, i + 1)].GetPath();
                holder.FocusNeighborTop = rowIndex == 1 ? holders[i - 5].GetPath() : holder.GetPath();
                holder.FocusNeighborBottom = rowIndex == 0 && i + 5 < holders.Count
                    ? holders[i + 5].GetPath() : holder.GetPath();
            }
            holders[Math.Min(2, holders.Count - 1)].TryGrabFocus();
        }

        SetGridPositions(animate: true);
    }
}
