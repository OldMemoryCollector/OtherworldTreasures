using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;
using OtherworldTreasures.Scripts.Konoe;

namespace OtherworldTreasures.Scripts.UI;

// 挂到 NCreature：进入游戏后，让本端显示的奈茵角色「本体」呈现所选配色。
// 同局所有端都按 KonoeColorState 的编号应用同一套配色（编号本身就是联机同步载荷）。
// 实现上只给本体 Sprite2D 挂一个 ShaderMaterial，不动 Texture —— 逐帧动画状态机每帧都要覆写 Texture，
// 但从不碰 Material，因此所有动作（idle/attack/cast/hit/dead/...）都能保持动画且同时换色。
public partial class KonoeColorVisual : Node2D
{
    private const string ShaderPath = "res://OtherworldTreasures/shaders/konoe_palette_swap.gdshader";
    private const string PaletteDir = "res://OtherworldTreasures/images/characters/nine/palette/palette_";

    private Player? _player;
    private Sprite2D? _body;
    private ShaderMaterial? _material;
    private int _last = -2;
    private int _attachTries;

    public override void _Ready() => Callable.From(Attach).CallDeferred();

    // NCreature.Entity 可能晚于 _Ready 才绑定；只试一次会让换色永久静默失效，这里重试到成功。
    public override void _Process(double delta)
    {
        if (_body != null || _attachTries >= 180) return;
        _attachTries++;
        if (_attachTries % 6 == 0) Attach();
        if (_attachTries == 180 && _body == null)
            Entry.Logger.Warn("[KonoeColor] 本体挂载超时：NCreature.Entity / Visuals.Body 一直不可用");
    }

    private void Attach()
    {
        if (_body != null) return;
        if (GetParent() is not NCreature creature ||
            creature.Entity?.Player is not { } player ||
            player.Character is not Characters.Konoe ||
            creature.Visuals is not { } visuals ||
            visuals.Body is not Sprite2D body) return;

        Shader? shader = GD.Load<Shader>(ShaderPath);
        Texture2D? defaultPalette = GD.Load<Texture2D>($"{PaletteDir}00.png");
        if (shader == null || defaultPalette == null)
        {
            Entry.Logger.Warn("[KonoeColor] 换色 shader / 默认调色板加载失败，角色本体保持默认配色");
            return;
        }

        _player = player;
        _body = body;
        _material = new ShaderMaterial { Shader = shader };
        _material.SetShaderParameter("default_palette", defaultPalette);
        KonoeColorState.Changed += OnStateChanged;
        Entry.Logger.Info($"[KonoeColor] 本体挂载成功 netId={player.NetId} index={KonoeColorState.Get(player)}");
        Refresh();
    }

    public override void _ExitTree()
    {
        KonoeColorState.Changed -= OnStateChanged;
        if (_body != null && GodotObject.IsInstanceValid(_body) && _body.Material == _material)
            _body.Material = null;
    }

    private void OnStateChanged(ulong netId, int index)
    {
        if (_player != null && netId == _player.NetId) Refresh();
    }

    private void Refresh()
    {
        if (_player == null || _body == null || _material == null) return;
        int index = KonoeColorState.Get(_player);
        if (index == _last) return;
        _last = index;
        Entry.Logger.Info($"[KonoeColor] 本体应用配色 netId={_player.NetId} index={index}");

        // 默认配色直接摘掉材质：零开销、零偏差，角色就是原版贴图
        if (index == KonoeColorState.DefaultIndex)
        {
            if (_body.Material == _material) _body.Material = null;
            return;
        }

        Texture2D? palette = GD.Load<Texture2D>($"{PaletteDir}{index:00}.png");
        if (palette == null)
        {
            Entry.Logger.Warn($"[KonoeColor] 第 {index + 1:00} 套调色板条缺失，角色本体保持默认配色");
            if (_body.Material == _material) _body.Material = null;
            return;
        }
        _material.SetShaderParameter("target_palette", palette);
        if (_body.Material != _material) _body.Material = _material;
    }
}
