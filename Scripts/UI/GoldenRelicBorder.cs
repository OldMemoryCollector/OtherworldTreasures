using Godot;
using MegaCrit.Sts2.Core.Nodes.Relics;
using OtherworldTreasures.Scripts.Events;

namespace OtherworldTreasures.Scripts.UI;

public partial class GoldenRelicBorder : Node
{
    private TextureRect? _coating;
    private TextureRect? _halo;
    private ShaderMaterial? _material;
    private ShaderMaterial? _haloMaterial;
    private static Shader? _shader;
    private static Shader? _haloShader;
    private static Texture2D? _foil;
    private static readonly string[] NativeParameters = ["pulse", "is_used", "is_wax"];
    private double _elapsed;

    public override void _Ready()
    {
        SetProcess(true);
        RefreshCoating();
    }

    public override void _Process(double delta)
    {
        _elapsed += delta;
        if (_elapsed < 0.1) return;
        _elapsed = 0;
        RefreshCoating();
    }

    private void RefreshCoating()
    {
        if (GetParent() is not NRelic node) return;
        bool golden = GoldenRelics.IsGolden(node.Model);
        bool silver = SilverRelics.IsSilver(node.Model);
        if ((!golden && !silver) || node.Icon?.Texture == null)
        {
            if (GodotObject.IsInstanceValid(_coating)) _coating!.Visible = false;
            if (GodotObject.IsInstanceValid(_halo)) _halo!.Visible = false;
            return;
        }

        var icon = node.Icon;
        if (!GodotObject.IsInstanceValid(_coating))
        {
            _shader ??= GD.Load<Shader>("res://OtherworldTreasures/shaders/golden_relic_coating.gdshader");
            _foil ??= GD.Load<Texture2D>("res://OtherworldTreasures/images/ui/golden_relic_coating.png");
            _material = new ShaderMaterial { Shader = _shader };
            _material.SetShaderParameter("gold_foil", _foil);
            _coating = new TextureRect
            {
                Name = "OtherworldGoldenCoating",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                Material = _material,
            };
            // Use exactly the same texture and layout as the original icon.
            // Its alpha clips even an opaque coating to the relic silhouette.
            icon.AddChild(_coating);
            _coating.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            _haloShader ??= GD.Load<Shader>("res://OtherworldTreasures/shaders/true_excalibur_glow.gdshader");
            _haloMaterial = new ShaderMaterial { Shader = _haloShader };
            _halo = new TextureRect
            {
                Name = "OtherworldMetalGlow",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                ShowBehindParent = true,
                Material = _haloMaterial,
            };
            icon.AddChild(_halo);
            _halo.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        }

        _coating!.Visible = true;
        _coating.Texture = icon.Texture;
        _coating.StretchMode = icon.StretchMode;
        _coating.TextureFilter = icon.TextureFilter;
        _coating.FlipH = icon.FlipH;
        _coating.FlipV = icon.FlipV;
        _coating.SelfModulate = icon.SelfModulate;
        _material!.SetShaderParameter("silver_mode", silver ? 1f : 0f);
        _halo!.Visible = true;
        _halo.Texture = icon.Texture;
        _halo.StretchMode = icon.StretchMode;
        _halo.TextureFilter = icon.TextureFilter;
        _halo.FlipH = icon.FlipH;
        _halo.FlipV = icon.FlipV;
        _halo.SelfModulate = icon.SelfModulate;
        TrueExcaliburGlow.SetTextureRegion(_haloMaterial!, icon.Texture);
        var margin = icon.Size * 0.14f;
        _halo.OffsetLeft = -margin.X;
        _halo.OffsetTop = -margin.Y;
        _halo.OffsetRight = margin.X;
        _halo.OffsetBottom = margin.Y;
        _haloMaterial!.SetShaderParameter("glow_color", silver ? new Color(0.75f, 0.86f, 1f) : new Color(1f, 0.83f, 0.4f));
        _haloMaterial.SetShaderParameter("intensity", 0.28f);
        _haloMaterial.SetShaderParameter("amplitude", 0.035f);

        // Preserve native disabled, active and wax presentation without replacing
        // the icon material used by the game or another mod.
        foreach (string parameter in NativeParameters)
        {
            var value = icon.Material is ShaderMaterial nativeMaterial
                ? nativeMaterial.GetShaderParameter(parameter) : default;
            _material!.SetShaderParameter(parameter, value.VariantType == Variant.Type.Nil ? Variant.From(0f) : value);
            if (parameter == "is_used") _haloMaterial.SetShaderParameter(parameter, value.VariantType == Variant.Type.Nil ? Variant.From(0f) : value);
        }
    }

    public override void _ExitTree()
    {
        if (GodotObject.IsInstanceValid(_coating)) _coating!.QueueFree();
        if (GodotObject.IsInstanceValid(_halo)) _halo!.QueueFree();
        _coating = null;
        _halo = null;
        _material = null;
        _haloMaterial = null;
    }
}
