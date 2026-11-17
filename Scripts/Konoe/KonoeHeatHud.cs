using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;

namespace OtherworldTreasures.Scripts.Konoe;

// BBCF-style STOCK / Heat display. Layout, texture cutouts, scale, spin speed
// and fill direction stay editable in heat_hud.tscn; this class only binds the
// live secondary-resource amount to the fill, colour tier and digit atlas.
public partial class KonoeHeatHud : Control
{
    internal const string ScenePath = "res://OtherworldTreasures/scenes/ui/konoe/heat_hud.tscn";
    private const float FillWidth = 607f;
    private const int SpellIconCell = 64;
    private static PackedScene? _scene;
    private Control? _layer;
    private Player? _player;
    private TextureRect[] _fills = [];
    private TextureRect[] _digits = [];
    private TextureRect? _rune;
    private TextureRect? _currentIcon;
    private TextureRect? _storedIcon;
    private int _lastHeat = -1;
    private int _lastPacked = -1;
    private int _lastStored = -1;
    private float _spin;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _scene ??= GD.Load<PackedScene>(ScenePath);
        _layer = _scene.Instantiate<Control>();
        AddChild(_layer);
        _fills =
        [
            _layer.GetNode<TextureRect>("FillClip/Blue"),
            _layer.GetNode<TextureRect>("FillClip/Green"),
            _layer.GetNode<TextureRect>("FillClip/Red"),
            _layer.GetNode<TextureRect>("FillClip/Gold"),
        ];
        _digits =
        [
            _layer.GetNode<TextureRect>("Digits/D0"),
            _layer.GetNode<TextureRect>("Digits/D1"),
            _layer.GetNode<TextureRect>("Digits/D2"),
        ];
        _rune = _layer.GetNode<TextureRect>("OrbRune");
        _currentIcon = _layer.GetNode<TextureRect>("OrbIconCurrent");
        _storedIcon = _layer.GetNode<TextureRect>("OrbIconStored");

        // 本节点只作为铺满战斗 UI 的空容器；整块 HUD 的位置由
        // heat_hud.tscn 根节点 KonoeHeatHudLayer 的 offset_left / offset_top 决定，
        // 主人可直接在 Godot 场景里拖动，代码不再覆盖 Position。
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        TryBindLocalPlayer();
    }

    private void TryBindLocalPlayer()
    {
        try
        {
            Player? me = LocalContext.GetMe(RunManager.Instance.DebugOnlyGetState());
            if (me != null) Bind(me);
        }
        catch { /* 战斗状态尚未就绪，下一帧再试 */ }
    }

    public void Bind(Player player)
    {
        _player = player;
        _lastHeat = -1;
        _lastPacked = -1;
        _lastStored = -1;
    }

    public override void _Process(double delta)
    {
        if (_layer == null) return;
        if (_player == null) TryBindLocalPlayer();
        // 只在奈茵的战斗里显示，且不依赖 RitsuLib 的次级资源 UI 显示策略
        if (_player == null || _player.Character is not Characters.Konoe)
        {
            Visible = false;
            return;
        }
        Visible = true;
        int heat = Mathf.Clamp(KonoeHeat.Get(_player), 0, 100);
        if (heat != _lastHeat)
        {
            _lastHeat = heat;
            Refresh(heat);
        }
        RefreshSpellIcons();
        float speed = (float)_layer.GetMeta("orb_spin_speed", 0.55).AsDouble();
        if (speed != 0f && GodotObject.IsInstanceValid(_rune))
        {
            _spin += (float)delta * speed;
            _rune!.Rotation = _spin;
        }
    }

    // The two STOCK orbs show the spell produced by the live element combination
    // (large orb) and by the stored combination (small orb). No elements means
    // BlackStone, which has no icon, so the orb stays empty with its rune visible.
    private void RefreshSpellIcons()
    {
        if (_currentIcon == null || _storedIcon == null || _player == null) return;
        int packed = KonoeElements.Get(_player);
        int stored = _player.Creature.GetPower<KonoeStoragePower>()?.PackedElements ?? 0;
        if (packed == _lastPacked && stored == _lastStored) return;
        _lastPacked = packed;
        _lastStored = stored;
        ApplySpellIcon(_currentIcon, KonoeElementRules.Resolve(packed));
        ApplySpellIcon(_storedIcon, KonoeElementRules.Resolve(stored));
    }

    private static void ApplySpellIcon(TextureRect icon, KonoeSpellKind spell)
    {
        int index = SpellIconIndex(spell);
        icon.Visible = index >= 0;
        if (index >= 0 && icon.Texture is AtlasTexture atlas)
            atlas.Region = new Rect2((index % 3) * SpellIconCell, (index / 3) * SpellIconCell, SpellIconCell, SpellIconCell);
    }

    // spell_icons.png (konoe.png) is 192x448: 64x64 cells, left-to-right then
    // top-to-bottom. Cells 0..18 are the 19 element combinations in the order
    // given by the owner; BlackStone has no cell and hides the icon.
    private static int SpellIconIndex(KonoeSpellKind spell) => spell switch
    {
        KonoeSpellKind.Coral => 0,          // 1  生气的珊瑚石   火
        KonoeSpellKind.Lapis => 1,          // 2  悲伤的青金石   水
        KonoeSpellKind.Emerald => 2,        // 3  痛苦的绿宝石   风
        KonoeSpellKind.Garnet => 3,         // 4  愤怒的石榴石   火＋火
        KonoeSpellKind.Sapphire => 4,       // 5  悲叹的蓝宝石   水＋水
        KonoeSpellKind.Peridot => 5,        // 6  穷困的橄榄石   风＋风
        KonoeSpellKind.Amethyst => 6,       // 7  歼爆的紫水晶   水＋火
        KonoeSpellKind.Citrine => 7,        // 8  翻转的黄水晶   风＋火
        KonoeSpellKind.Chrysoberyl => 8,    // 9  服从的绿柱石   水＋风
        KonoeSpellKind.Rubellite => 9,      // 10 激昂的红碧玺   火＋火＋火
        KonoeSpellKind.Tanzanite => 10,     // 11 哀泣的坦桑石   水＋水＋水
        KonoeSpellKind.Malachite => 11,     // 12 凄惨的孔雀石   风＋风＋风
        KonoeSpellKind.Kunzite => 12,       // 13 狱碎的锂紫石   火＋火＋水
        KonoeSpellKind.Morganite => 13,     // 14 脉动的摩根石   火＋火＋风
        KonoeSpellKind.Taaffeite => 14,     // 15 幻惑的塔菲石   水＋水＋火
        KonoeSpellKind.Andradite => 15,     // 16 封缚的钙铁榴石 风＋风＋水
        KonoeSpellKind.Uvarovite => 16,     // 17 击灭的钙铬榴石 风＋风＋火
        KonoeSpellKind.Azurite => 17,       // 18 誓约的天青石   水＋水＋风
        KonoeSpellKind.Nephrite => 18,      // 19 终焉的软玉     水＋风＋火
        _ => -1,
    };

    private void Refresh(int heat)
    {
        bool fromRight = _layer!.GetMeta("fill_from_right", true).AsBool();
        float width = FillWidth * heat / 100f;
        float x = fromRight ? FillWidth - width : 0f;
        foreach (var fill in _fills)
        {
            fill.Position = new Vector2(x, 0f);
            fill.Size = new Vector2(width, 14f);
            fill.Visible = false;
        }
        _fills[3].Modulate = Colors.White;
        if (heat > 0)
        {
            if (heat >= 100)
            {
                _fills[3].Visible = true;
            }
            else if (heat >= 75)
            {
                // Orange-red tier: red block with a translucent gold overlay.
                _fills[2].Visible = true;
                _fills[3].Visible = true;
                _fills[3].Modulate = new Color(1, 1, 1, 0.45f);
            }
            else if (heat >= 50) _fills[2].Visible = true;
            else if (heat >= 25) _fills[1].Visible = true;
            else _fills[0].Visible = true;
        }

        bool leadingZero = _layer.GetMeta("digit_leading_zero", true).AsBool();
        string text = heat.ToString("D3");
        if (!leadingZero)
        {
            string trimmed = text.TrimStart('0');
            text = (trimmed.Length == 0 ? "0" : trimmed).PadLeft(3, ' ');
        }
        for (int i = 0; i < _digits.Length; i++)
        {
            char c = text[i];
            _digits[i].Visible = c != ' ';
            if (c != ' ' && _digits[i].Texture is AtlasTexture atlas)
                atlas.Region = new Rect2((c - '0') * 41, 0, 41, 64);
        }
    }
}
