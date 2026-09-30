using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;

namespace OtherworldTreasures.Scripts.FourSouls;

/// <summary>
/// 地图节点上的四魂光芒。
/// 做法：复制节点自带的图标贴图（%Icon），按中心放大成几层，作为 %Icon 的"画在父节点背后"的子节点
/// —— 层序是 [原版 %Outline 底板] → [光层] → [图标本体]，光能一直贴到图标边缘（不会被底板挖掉一圈），
/// 而图标本体（半透明）盖在光上，图标仍然看得清。最内层还叠一条扫过的亮带，形成流动感。
/// 混合方式用普通混合（blend_mix）而不是加法：加法是逐通道相加，叠到接近 1 就糊成白色，
/// 蓝/绿/紫尤其认不出来，红也只是稍微明显；普通混合才能保住魂本身的颜色，方便在地图上辨认。
/// 单魂固定该魂颜色；多魂时一种颜色保持一会儿再快速换下一种，避免一直在混色看不清是哪几种魂。
/// 开关（四魂之玉是否开启）由本节点自己复查：开启才显示，关闭立刻隐藏并还原悬浮文字。
/// </summary>
public partial class FourSoulsGlow : Node
{
    // 多魂时每种魂颜色占用的总时长、其中用来换到下一色的时间（秒）
    // 换色过程前半段旧色渐暗、后半段新色渐亮，纯色停留仍有 1.2 秒，够看清是哪一种
    private const float HoldPerColor = 2.0f;
    private const float ColorFade = 0.8f;

    // 换色时压到多暗（1 = 不压）。压得越低，换色相那一刻越不容易被看出来
    private const float FadeFloor = 0.3f;

    // 开关状态复查间隔（秒）：让"关掉四魂之玉后地图光芒立刻消失"
    private const float ActiveCheckInterval = 0.25f;

    // 光层：每层只剩"图标边缘往外的一圈"（shader 里已把图标本体抠掉），逐层放大、逐层变淡
    // 叠乘后往外的实际覆盖约 0.83 → 0.62 → 0.34，形成由内往外的渐隐
    // 流光带放在第二层：第一层紧贴图标、可露出的面积太小，带子扫过时看不明显
    private static readonly (float Scale, float Base, float Band)[] LayerSpecs =
    {
        (1.08f, 0.55f, 0.00f), // 贴着图标边缘的一圈：只做常亮底色
        (1.35f, 0.42f, 0.42f), // 流光带扫过的是这一圈
        (1.60f, 0.34f, 0.00f), // 最外圈：只做弥散
    };

    // 逐像素把图标贴图染成魂色 + 流动光带 + 呼吸。写成代码字符串，避免新增 shader 资源走导入流程
    private const string ShaderCode = @"
shader_type canvas_item;
render_mode blend_mix;

uniform vec4 soul_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform float base_alpha = 0.55;
uniform float band_alpha = 0.42;
uniform float base_brighten = 0.1;
uniform float band_brighten = 0.22;
uniform float flow_speed = 0.45;
uniform float band_sharpness = 6.0;
uniform float mask_cutoff = 0.2;
uniform float layer_scale = 1.0;
uniform float alpha_scale = 1.0;
uniform float phase = 0.0;

void fragment() {
    // 把图标贴图当剪影用：超过阈值的统一填成实心，这样不会把图标内部的花纹也带出来
    float cut0 = mask_cutoff * 0.1;
    float mask = smoothstep(cut0, mask_cutoff, texture(TEXTURE, UV).a);
    if (mask <= 0.001) {
        discard;
    }

    // 抠掉图标本体占的那块：本层的 UV 是放大之后的，把屏幕位置换算回未放大的图标 UV 再采一次，
    // 得到图标本体的剪影并减掉，于是每一层只剩图标边缘往外的一圈，光可以画在所有节点之上也不会糊住图标
    vec2 iconUv = (UV - vec2(0.5)) * layer_scale + vec2(0.5);
    // 换算后可能落到图标矩形之外，此时没有本体要抠；同时把采样夹回 0..1，避免采到图集里相邻的贴图
    vec4 inside = vec4(step(0.0, iconUv.x), step(iconUv.x, 1.0), step(0.0, iconUv.y), step(iconUv.y, 1.0));
    float inRange = inside.x * inside.y * inside.z * inside.w;
    float iconMask = inRange * smoothstep(cut0, mask_cutoff, texture(TEXTURE, clamp(iconUv, vec2(0.0), vec2(1.0))).a);
    float ring = mask * (1.0 - iconMask);
    if (ring <= 0.001) {
        discard;
    }

    // 斜向流动的亮带：三角波收窄成一条带子，沿轮廓滑动
    float t = fract(UV.x * 0.9 + UV.y * 0.55 - TIME * flow_speed + phase * 0.15);
    float tri = 1.0 - abs(2.0 * t - 1.0);
    float band = pow(tri, band_sharpness);

    float a = clamp(base_alpha + band_alpha * band, 0.0, 1.0) * ring * alpha_scale;
    // 光带处把颜色往亮里提一点（保色相，只提亮度），做出流光的观感而不是换一种颜色
    vec3 rgb = mix(soul_color.rgb, vec3(1.0), base_brighten + band_brighten * band);
    COLOR = vec4(rgb, a) * COLOR;
}
";

    // 共享的 shader：所有节点共用，只编译一次
    private static Shader? s_shader;

    private readonly List<TextureRect> _layers = new();
    private readonly List<ShaderMaterial> _materials = new();
    private readonly List<float> _specScales = new();

    private SoulKind[] _souls = Array.Empty<SoulKind>();
    private IRunState? _runState;
    private NNormalMapPoint? _point;
    private TextureRect? _icon;
    private string _soulTooltip = "";
    private string _baseTooltip = "";
    private float _phase;
    private float _time;
    private float _checkTimer;
    private bool _active;

    public void Setup(SoulKind[] souls, IRunState runState, NNormalMapPoint point)
    {
        _souls = souls ?? Array.Empty<SoulKind>();
        _runState = runState;
        _point = point;
        // 原版该节点的悬浮文字（关闭时还原回去）；用插值读，兼容 string / StringName 两种属性类型
        _baseTooltip = $"{point.TooltipText}";
        _soulTooltip = string.Join("  ", _souls.Select(k => $"{Souls.Name(k)} ×1"));
        // 每个房间给一个固定相位，避免整张地图的光带、呼吸完全同步
        _phase = point.Point.coord.col * 0.31f + point.Point.coord.row * 0.63f;

        BuildLayers(point);
        UpdateVisuals();
        // 先按当前开关刷一次，避免挂上来的第一帧还带着错误状态
        Refresh();
    }

    /// <summary>
    /// 复制图标贴图做发光层。挂在 %Icon 自己名下并置 show_behind_parent，
    /// 于是绘制顺序是 [%Outline 底板] → [光层] → [图标本体]。
    /// </summary>
    private void BuildLayers(NNormalMapPoint point)
    {
        var icon = point.GetNodeOrNull<TextureRect>("%Icon");
        if (icon?.Texture == null)
        {
            Entry.Logger.Warn("[FourSouls] 找不到地图节点的 %Icon，四魂光芒无法显示");
            return;
        }
        _icon = icon;

        foreach (var (scale, baseAlpha, bandAlpha) in LayerSpecs)
        {
            var layer = (TextureRect)icon.Duplicate();
            layer.Name = $"FourSoulsGlowLayer{_layers.Count}";
            layer.UniqueNameInOwner = false;
            layer.Modulate = Colors.White;
            // 原版图标的 self_modulate 是半透明（未探索节点 0.5），照抄会把光整体压暗一半
            layer.SelfModulate = Colors.White;
            // 画在 %Icon 自己后面：这样在 %Outline 底板之上、图标本体之下
            layer.ShowBehindParent = true;
            // 但绘制顺序用绝对 z 提到所有地图节点之上：地图节点是挨着排的，如果按树序绘制，
            // 相邻节点的 %Outline 底板会把先画节点的光"切"掉一块，切口露底色 = 看着发黑。
            // 提到上面之后就不会被任何节点遮挡，而 shader 已经把图标本体那块抠掉了，也不会糊住图标。
            layer.ZAsRelative = false;
            layer.ZIndex = 1;
            layer.Visible = false;
            layer.MouseFilter = Control.MouseFilterEnum.Ignore;

            // Duplicate 会把 %Icon 的子节点（原版那层不透明的 %Outline 底板）一起复制过来。
            // 那些底板是按各自缩放画的不透明层，会把先画好的内层光整个盖掉——之前"完全看不见光"就是它。
            var copiedChildren = new List<Node>();
            foreach (var child in layer.GetChildren())
            {
                copiedChildren.Add(child);
            }
            foreach (var child in copiedChildren)
            {
                layer.RemoveChild(child);
                child.Free();
            }

            layer.PivotOffset = icon.PivotOffset;
            layer.Scale = Vector2.One * scale;

            var material = new ShaderMaterial { Shader = GetShader() };
            material.SetShaderParameter("soul_color", Colors.White);
            material.SetShaderParameter("base_alpha", baseAlpha);
            material.SetShaderParameter("band_alpha", bandAlpha);
            // 这层放大到多少倍：shader 用它把图标本体那块从本层里抠掉
            material.SetShaderParameter("layer_scale", scale);
            material.SetShaderParameter("phase", _phase);
            layer.Material = material;

            icon.AddChildSafely(layer);

            _layers.Add(layer);
            _materials.Add(material);
            _specScales.Add(scale);
        }
    }

    /// <summary>
    /// 让光层保持以图标中心为轴放大。
    /// 光层挂在 %Icon 名下，图标自身的缩放（悬停放大）会由父节点带下去，这里只加自己的那份。
    /// </summary>
    private void SyncLayerTransforms()
    {
        for (int i = 0; i < _layers.Count; i++)
        {
            var layer = _layers[i];
            if (!GodotObject.IsInstanceValid(layer))
            {
                continue;
            }
            layer.PivotOffset = layer.Size * 0.5f;
            layer.Scale = Vector2.One * _specScales[i];
        }
    }

    private static Shader GetShader()
    {
        s_shader ??= new Shader { Code = ShaderCode };
        return s_shader;
    }

    /// <summary>按当前开关状态刷新：显示/隐藏光芒，并把悬浮文字换成四魂列表或还原。</summary>
    private void Refresh()
    {
        bool active = _runState != null && FourSoulsSystem.IsActive(_runState);
        if (_active != active)
        {
            _active = active;
            foreach (var layer in _layers)
            {
                if (GodotObject.IsInstanceValid(layer))
                {
                    layer.Visible = active;
                }
            }
        }
        if (_point != null && GodotObject.IsInstanceValid(_point))
        {
            _point.TooltipText = active ? _soulTooltip : _baseTooltip;
        }
    }

    /// <summary>
    /// 更新所有光层的颜色与整体不透明度。
    /// 单魂固定颜色；多魂时一种颜色保持 HoldPerColor - ColorFade 秒，再用 ColorFade 秒换到下一色。
    /// 换色的做法是前半段保持旧色渐暗、后半段新色渐亮，在压到最暗的那一刻才换色相：
    /// 色相环上任意两色之间必然扫过第三种颜色（绿到紫要么经过蓝要么经过红），
    /// 直接按色相过渡就会闪出别的魂的颜色，压暗再换则不会，也不会像 RGB 直插那样经过灰。
    /// </summary>
    private void UpdateVisuals()
    {
        if (_souls.Length == 0)
        {
            return;
        }

        Color color;
        float alphaScale = 1f;
        if (_souls.Length == 1)
        {
            color = Souls.ColorOf(_souls[0]);
        }
        else
        {
            float slot = _time / HoldPerColor;
            float whole = Mathf.Floor(slot);
            int i = (((int)whole % _souls.Length) + _souls.Length) % _souls.Length;
            int j = (i + 1) % _souls.Length;

            float intoSlot = _time - whole * HoldPerColor;
            float fadeStart = Mathf.Max(HoldPerColor - ColorFade, 0f);
            float f = HoldPerColor <= ColorFade || ColorFade <= 0f
                ? 0f
                : Mathf.Clamp((intoSlot - fadeStart) / ColorFade, 0f, 1f);
            // 缓入缓出：两头不会有明显的起步/急停
            f = f * f * (3f - 2f * f);

            color = f < 0.5f ? Souls.ColorOf(_souls[i]) : Souls.ColorOf(_souls[j]);
            // 换色点最暗、两端最亮
            alphaScale = FadeFloor + (1f - FadeFloor) * Mathf.Abs(2f * f - 1f);
        }

        foreach (var material in _materials)
        {
            material.SetShaderParameter("soul_color", color);
            material.SetShaderParameter("alpha_scale", alphaScale);
        }
    }

    public override void _Process(double delta)
    {
        // 四魂之玉被关闭（或遗物没了）时立刻隐藏
        _checkTimer += (float)delta;
        if (_checkTimer >= ActiveCheckInterval)
        {
            _checkTimer = 0f;
            Refresh();
        }

        if (!_active || _souls.Length == 0)
        {
            return;
        }

        _time += (float)delta;
        UpdateVisuals();
        SyncLayerTransforms();
    }

    public override void _ExitTree()
    {
        // 光层挂在 %Icon 名下（不是本节点的子节点），这里要自己收掉
        foreach (var layer in _layers)
        {
            if (GodotObject.IsInstanceValid(layer))
            {
                layer.QueueFree();
            }
        }
        _layers.Clear();
        _materials.Clear();
    }
}
