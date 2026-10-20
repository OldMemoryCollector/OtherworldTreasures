using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Relics;
using OtherworldTreasures.Scripts.Relics;

namespace OtherworldTreasures.Scripts.UI;

public partial class TrueExcaliburGlow : Node
{
    private TextureRect? _glow;
    private ShaderMaterial? _material;
    private static Shader? _shader;
    private double _elapsed;

    public override void _Ready()
    {
        SetProcess(true);
        // NRelic attachments become ready before NRelicInventoryHolder assigns Model.
        // The first refresh runs on the next process tick, after the holder is initialized.
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;
        if (_elapsed < 0.1) return;
        _elapsed = 0;
        RefreshGlow();
    }

    private void RefreshGlow()
    {
        if (GetParent() is not NRelic node) return;
        RelicModel model;
        try { model = node.Model; }
        catch (InvalidOperationException) { return; }
        if (model is not TrueExcalibur || node.Icon?.Texture == null)
        {
            if (GodotObject.IsInstanceValid(_glow)) _glow!.Visible = false;
            return;
        }

        var icon = node.Icon;
        if (!GodotObject.IsInstanceValid(_glow))
        {
            _shader ??= GD.Load<Shader>("res://OtherworldTreasures/shaders/true_excalibur_glow.gdshader");
            _material = new ShaderMaterial { Shader = _shader };
            _glow = new TextureRect
            {
                Name = "OtherworldTrueExcaliburGlow",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                ShowBehindParent = true,
                Material = _material,
            };
            icon.AddChild(_glow);
            _glow.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        }

        _glow!.Visible = true;
        _glow.Texture = icon.Texture;
        _glow.StretchMode = icon.StretchMode;
        _glow.TextureFilter = icon.TextureFilter;
        _glow.FlipH = icon.FlipH;
        _glow.FlipV = icon.FlipV;
        _glow.SelfModulate = icon.SelfModulate;
        SetTextureRegion(_material!, icon.Texture);
        // Give the silhouette glow room outside the icon without changing layout.
        var margin = icon.Size * 0.14f;
        _glow.OffsetLeft = -margin.X;
        _glow.OffsetTop = -margin.Y;
        _glow.OffsetRight = margin.X;
        _glow.OffsetBottom = margin.Y;
        var used = icon.Material is ShaderMaterial native ? native.GetShaderParameter("is_used") : default;
        _material!.SetShaderParameter("is_used", used.VariantType == Variant.Type.Nil ? Variant.From(0f) : used);
    }

    internal static void SetTextureRegion(ShaderMaterial material, Texture2D texture)
    {
        // Native relics may share an atlas. UV expansion must stay relative to
        // this relic's region rather than sample a neighboring atlas icon.
        var region = new Vector4(0, 0, 1, 1);
        if (texture is AtlasTexture { Atlas: { } atlas } slice)
        {
            var rect = slice.Region;
            region = new Vector4(rect.Position.X / atlas.GetWidth(), rect.Position.Y / atlas.GetHeight(),
                rect.Size.X / atlas.GetWidth(), rect.Size.Y / atlas.GetHeight());
        }
        material.SetShaderParameter("texture_region", region);
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_glow)) _glow!.QueueFree();
        _glow = null;
        _material = null;
    }
}
