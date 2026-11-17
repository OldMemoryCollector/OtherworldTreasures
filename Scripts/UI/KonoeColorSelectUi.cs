using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using STS2RitsuLib;
using STS2RitsuLib.RunData;
using STS2RitsuLib.Scaffolding.Godot.NodeAttachments;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using OtherworldTreasures.Scripts.Konoe;

namespace OtherworldTreasures.Scripts.UI;

// 所有开局大厅共用的配色控件：加载主人调整的 color_select.tscn。
// 人物动图由场景里的 AnimatedSprite2D + SpriteFrames + AnimationPlayer 播放
// （与 scenes/characters/nine/attacks/*.tscn 同一套结构，帧序列/时长/循环都在 .tscn 里可编辑）。
// C# 只做四件事：箭头/滚轮切换 01..24、字模数字、换配色时替换 SpriteFrames 的 10 张贴图、
// 把编号同步给同局玩家。没有任何逐帧切贴图的代码。
public partial class KonoeColorSelectUi : Control
{
    internal const string ScenePath = "res://OtherworldTreasures/scenes/ui/konoe/color_select.tscn";
    private const string DigitsPath = "res://OtherworldTreasures/images/ui/konoe/color_select/color_digits.png";
    private const float DigitWidth = 38f;   // 字模 512x64，0-9 按 38px 等距排列
    private const float FlashSeconds = 0.18f;
    private const double BaseFps = 12.0;    // 场景里 Animation 轨道按 12fps 编排
    // 奈茵角色在 ModelDb 里的稳定 Id（localization/characters.json 的键前缀）
    private const string KonoeCharacterEntry = "OTHERWORLD_TREASURES_CHARACTER_KONOE";

    private static readonly StringName PoseAnim = new("pose");
    private static PackedScene? _scene;
    private Control? _layer;
    private Control? _screen;
    private List<NCharacterSelectButton>? _characterButtons;
    private static readonly ConditionalWeakTable<Control, StartRunLobby> Lobbies = new();
    private SpriteFrames? _spriteFrames;
    private TextureRect? _tens;
    private TextureRect? _ones;
    private TextureRect? _up;
    private TextureRect? _down;
    private Texture2D? _digits;
    private readonly AtlasTexture?[] _digitCache = new AtlasTexture?[10];
    private int _index;
    private ulong _netId;
    private float _flash;
    private TextureRect? _flashNode;

    internal static void Register()
    {
        // The framework supplies the actual lobby for every mode, including daily lobbies
        // whose screen does not expose a public Lobby property. No private-field lookup.
        RitsuLibFramework.SubscribeLifecycle<RunSavedDataLobbyStagingEvent>(evt =>
        {
            if (evt.Lobby.LobbyListener is not Control screen) return;
            Lobbies.Remove(screen);
            Lobbies.Add(screen, evt.Lobby);
        });
        RegisterScreen<NCharacterSelectScreen>();
        RegisterScreen<MegaCrit.Sts2.Core.Nodes.Screens.CustomRun.NCustomRunScreen>();
        RegisterScreen<MegaCrit.Sts2.Core.Nodes.Screens.DailyRun.NDailyRunScreen>();
#if STS2_113
        RegisterScreen<MegaCrit.Sts2.Core.Nodes.Screens.Blitz.NBlitzScreen>();
        RegisterScreen<MegaCrit.Sts2.Core.Nodes.Screens.Dread.NDreadScreen>();
#endif
    }

    private static void RegisterScreen<TScreen>() where TScreen : Control, IStartRunLobbyListener =>
        ModNodeAttachmentRegistry.For(Entry.ModId).RegisterReadyChild<TScreen, KonoeColorSelectUi>(
            "konoe_color_select_" + typeof(TScreen).Name, static _ => new KonoeColorSelectUi(), new NodeAttachmentOptions
            {
                Name = "OtherworldKonoeColorSelect",
                DuplicatePolicy = NodeAttachmentDuplicatePolicy.ReuseExistingByName,
            });

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _screen = GetParent() as Control;
        // 先隐藏，等 _Process 确认当前选中的是奈茵再显示，避免切角色时闪一下
        Visible = false;
        _scene ??= GD.Load<PackedScene>(ScenePath);
        if (_scene == null)
        {
            Entry.Logger.Warn("[KonoeColor] color_select.tscn 加载失败");
            return;
        }
        _layer = _scene.Instantiate<Control>();
        AddChild(_layer);

        // 复制一份 SpriteFrames：换色贴图是运行时生成的，不污染场景资源本身。
        AnimatedSprite2D? pose = _layer.GetNodeOrNull<AnimatedSprite2D>("CharacterFrames/Pose");
        if (pose?.SpriteFrames != null)
        {
            _spriteFrames = (SpriteFrames)pose.SpriteFrames.Duplicate();
            pose.SpriteFrames = _spriteFrames;
        }
        // 帧率仍由场景 metadata 控制：只改 AnimationPlayer 的播放速度，不动关键帧。
        Control? framesNode = _layer.GetNodeOrNull<Control>("CharacterFrames");
        double fps = framesNode != null && framesNode.HasMeta("frame_fps")
            ? framesNode.GetMeta("frame_fps").AsDouble() : BaseFps;
        AnimationPlayer? player = _layer.GetNodeOrNull<AnimationPlayer>("CharacterFrames/AnimationPlayer");
        if (player != null) player.SpeedScale = Mathf.Max(0.05f, (float)(fps / BaseFps));

        _tens = _layer.GetNodeOrNull<TextureRect>("DigitTens");
        _ones = _layer.GetNodeOrNull<TextureRect>("DigitOnes");
        _up = _layer.GetNodeOrNull<TextureRect>("CursorUp");
        _down = _layer.GetNodeOrNull<TextureRect>("CursorDown");
        _digits = GD.Load<Texture2D>(DigitsPath);

        _netId = KonoeColorState.LocalNetId;
        KonoeColorState.Changed += OnStateChanged;
        _index = KonoeColorState.Get(_netId);
        Apply();
    }

    public override void _ExitTree() => KonoeColorState.Changed -= OnStateChanged;

    // 用 _Input 而不是 _UnhandledInput：角色选择界面的背景 TextureRect 可能消费鼠标事件，
    // 走 unhandled 会让滚轮 / 点击收不到。只在命中本控件区域时才标记已处理。
    public override void _Input(InputEvent @event)
    {
        if (!IsVisibleInTree() || _layer == null || @event is not InputEventMouseButton mb || !mb.Pressed) return;
        // Resolve this click, not the position of a later mouse-motion event.
        Vector2 pos = GetCanvasTransform().AffineInverse() * mb.Position;
        if (!_layer.GetGlobalRect().Grow(24f).HasPoint(pos)) return;

        int delta = 0;
        if (mb.ButtonIndex == MouseButton.WheelUp) delta = 1;
        else if (mb.ButtonIndex == MouseButton.WheelDown) delta = -1;
        else if (mb.ButtonIndex == MouseButton.Left)
        {
            if (_up != null && _up.GetGlobalRect().HasPoint(pos)) delta = 1;
            else if (_down != null && _down.GetGlobalRect().HasPoint(pos)) delta = -1;
        }
        if (delta == 0) return;

        Change(delta);
        GetViewport()?.SetInputAsHandled();
    }

    public override void _Process(double delta)
    {
        RefreshVisibility();
        if (_flash <= 0f) return;
        _flash -= (float)delta;
        if (_flash > 0f || _flashNode == null) return;
        _flashNode.Modulate = new Color(1f, 1f, 1f, 1f);
        _flashNode = null;
    }

    // Screens are cached by the menu stack. Only the active screen may bind the local
    // player; an invisible old lobby must not overwrite a new lobby's network identity.
    private void RefreshVisibility()
    {
        bool show = false;
        if (_screen != null && _screen.IsVisibleInTree())
        {
            Lobbies.TryGetValue(_screen, out StartRunLobby? lobby);
            // Public native accessors also cover the brief interval before the first
            // framework staging event, or returning to a cached screen.
            if (_screen is ICharacterSelectButtonDelegate selection) lobby = selection.Lobby;
            if (lobby != null)
            {
                var local = lobby.LocalPlayer;
                if (local.id != 0UL)
                {
                    KonoeColorState.NoteLobbyNetId(local.id);
                    if (_netId != local.id)
                    {
                        _netId = local.id;
                        _index = KonoeColorState.Get(_netId);
                        Apply();
                    }
                }
                show = IsKonoe(local.character);
            }
            _characterButtons ??= FindCharacterButtons(_screen);
            foreach (NCharacterSelectButton button in _characterButtons)
            {
                if (!GodotObject.IsInstanceValid(button) || !button.IsSelected) continue;
                // The visible local selection takes priority over a lobby still updating.
                // Remote-player selection markers never count as a local selection.
                show = IsKonoe(button.Character) || (button.IsRandom && show);
                break;
            }
        }
        if (Visible != show) Visible = show;
    }

    private static bool IsKonoe(MegaCrit.Sts2.Core.Models.CharacterModel? character) =>
        character is Characters.Konoe ||
        string.Equals(character?.Id.Entry, KonoeCharacterEntry, StringComparison.Ordinal);

    private static List<NCharacterSelectButton> FindCharacterButtons(Node root)
    {
        var buttons = new List<NCharacterSelectButton>();
        void Visit(Node node)
        {
            if (node is NCharacterSelectButton button) buttons.Add(button);
            foreach (Node child in node.GetChildren()) Visit(child);
        }
        Visit(root);
        return buttons;
    }

    private void OnStateChanged(ulong netId, int index)
    {
        if (netId != _netId) return;
        _index = index;
        Apply();
    }

    private void Change(int delta)
    {
        _index = ((_index + delta) % KonoeColorPalette.Count + KonoeColorPalette.Count) % KonoeColorPalette.Count;
        // 本地立即反馈；SetLocal 同时把编号广播给同局其他端
        Apply();
        KonoeColorState.SetLocal(_index);
        if (_flashNode != null) _flashNode.Modulate = new Color(1f, 1f, 1f, 1f);
        _flashNode = delta > 0 ? _up : _down;
        if (_flashNode != null)
        {
            _flashNode.Modulate = new Color(1.7f, 1.7f, 1.7f, 1f);
            _flash = FlashSeconds;
        }
    }

    private void Apply()
    {
        // 显示 01..24，内部索引 0..23 对应 ph00_00..ph23_00
        int shown = _index + 1;
        if (_tens != null) _tens.Texture = Digit(shown / 10);
        if (_ones != null) _ones.Texture = Digit(shown % 10);

        // 只把当前配色的 10 张上色贴图塞进 SpriteFrames。
        // 播放推进由 AnimationPlayer 的 Pose:frame 轨道负责，这里不碰 frame / 时间轴。
        Texture2D[] frames = KonoeColorPalette.Get(_index);
        if (_spriteFrames == null || !_spriteFrames.HasAnimation(PoseAnim)) return;
        int n = Math.Min(frames.Length, _spriteFrames.GetFrameCount(PoseAnim));
        for (int i = 0; i < n; i++)
        {
            if (frames[i] != null) _spriteFrames.SetFrame(PoseAnim, i, frames[i], 1.0f);
        }
    }

    private AtlasTexture? Digit(int d)
    {
        if (_digits == null || d < 0 || d > 9) return null;
        return _digitCache[d] ??= new AtlasTexture
        {
            Atlas = _digits,
            Region = new Rect2(d * DigitWidth, 0f, DigitWidth, 64f),
        };
    }
}
