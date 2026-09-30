using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using OtherworldTreasures.Scripts.Relics;

namespace OtherworldTreasures.Scripts.UI;

/// <summary>
/// 小破表变身选择界面。
/// 阶段1：播放 4 帧变身动画（0.2s + 0.1s + 0.1s + 0.1s）
/// 阶段2：显示英雄选择界面，左右箭头切换英雄，确认后变身
/// 流派不由玩家选：变身时随机决定（见 Omnitrix.Transform）
/// 鬼影模式（forcedHero）：动画后直接展示鬼影图并短暂停留，自动完成变身
/// 全部 UI 由代码动态创建，不依赖任何 tscn 场景。
/// </summary>
public partial class OmnitrixSelectScreen : Control
{
    // 变身动画帧停留时间（秒）：第一帧停留较久，其余快速闪过
    private static readonly float[] FrameDurations = { 0.2f, 0.1f, 0.1f, 0.1f };
    private static readonly string[] FramePaths =
    {
        "res://OtherworldTreasures/images/omnitrix/select_1.png",
        "res://OtherworldTreasures/images/omnitrix/select_2.png",
        "res://OtherworldTreasures/images/omnitrix/select_3.png",
        "res://OtherworldTreasures/images/omnitrix/select_4.png"
    };

    // 可选英雄（鬼影不在此列，它由极低概率随机替换）
    private static readonly AlienHero[] Heroes =
    {
        AlienHero.Heatblast,
        AlienHero.FourArms,
        AlienHero.Wildmutt
    };

    // 每个英雄的选择界面图
    private static readonly string[] ChoosePaths =
    {
        "res://OtherworldTreasures/images/omnitrix/choose_heatblast.png",
        "res://OtherworldTreasures/images/omnitrix/choose_fourarms.png",
        "res://OtherworldTreasures/images/omnitrix/choose_wildmutt.png"
    };

    // 鬼影展示图
    private const string GhostfreakPath = "res://OtherworldTreasures/images/omnitrix/choose_ghostfreak.png";

    // 图片显示区尺寸
    private static readonly Vector2 ImageSize = new(420, 420);

    private readonly AlienHero? _forcedHero;
    private TaskCompletionSource<HeroSelection?>? _tcs;

    private TextureRect _image = null!;
    private Button _leftArrow = null!;
    private Button _rightArrow = null!;
    private Button _confirmButton = null!;
    private Label _hintLabel = null!;

    private int _currentIndex;

    public OmnitrixSelectScreen(AlienHero? forcedHero = null)
    {
        _forcedHero = forcedHero;
    }

    /// <summary>
    /// 弹出界面并等待玩家选择。返回选中的英雄+流派，null 表示取消。
    /// </summary>
    public async Task<HeroSelection?> ShowAsync()
    {
        _tcs = new TaskCompletionSource<HeroSelection?>();

        BuildUi();

        var room = NCombatRoom.Instance;
        if (room != null)
        {
            room.AddChild(this);
        }
        else
        {
            GetTree().Root.AddChild(this);
        }

        // 阶段1：播放变身动画帧
        await PlayAnimationFrames();

        HeroSelection? result;
        if (_forcedHero.HasValue)
        {
            // 鬼影模式：展示鬼影图，短暂停留后自动完成（流派固定烈焰爆破）
            ShowImage(GhostfreakPath);
            await ToSignal(GetTree().CreateTimer(0.9f), "timeout");
            result = new HeroSelection(_forcedHero.Value, AlienSpecialization.Blast);
        }
        else
        {
            // 正常模式：进入选择界面，等待玩家确认
            EnterSelectMode();
            result = await _tcs.Task;
        }

        QueueFree();
        return result;
    }

    private void BuildUi()
    {
        // 全屏覆盖层
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        // 半透明黑底
        var bg = new ColorRect { Color = new Color(0, 0, 0, 0.82f) };
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        bg.MouseFilter = MouseFilterEnum.Stop;
        AddChild(bg);

        // 中央图片（展示动画帧 / 选择界面图）
        _image = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        Center(_image, ImageSize);
        AddChild(_image);

        // 左右切换箭头
        _leftArrow = CreateArrowButton("◀", -1);
        _rightArrow = CreateArrowButton("▶", 1);

        // 确认按钮
        _confirmButton = new Button
        {
            Text = Loc("OTHERWORLD_TREASURES_UI_OMNITRIX_CONFIRM"),
            Visible = false,
        };
        Center(_confirmButton, new Vector2(140, 54), new Vector2(0, 268));
        _confirmButton.Pressed += OnConfirmPressed;
        AddChild(_confirmButton);

        // 底部提示
        _hintLabel = new Label
        {
            Text = Loc("OTHERWORLD_TREASURES_UI_OMNITRIX_HINT"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Visible = false,
        };
        Center(_hintLabel, new Vector2(560, 30), new Vector2(0, 320));
        AddChild(_hintLabel);
    }

    // 取本地化文本（跟随游戏语言设置）
    private static string Loc(string key)
        => new LocString("gameplay_ui", key).GetFormattedText();

    // 把控件以屏幕中心为基准定位
    private static void Center(Control control, Vector2 size, Vector2 offset = default)
    {
        control.AnchorLeft = 0.5f;
        control.AnchorTop = 0.5f;
        control.AnchorRight = 0.5f;
        control.AnchorBottom = 0.5f;
        control.OffsetLeft = offset.X - size.X * 0.5f;
        control.OffsetTop = offset.Y - size.Y * 0.5f;
        control.OffsetRight = offset.X + size.X * 0.5f;
        control.OffsetBottom = offset.Y + size.Y * 0.5f;
    }

    private Button CreateArrowButton(string text, int direction)
    {
        var btn = new Button { Text = text, Visible = false };
        var x = direction < 0 ? -300f : 300f;
        Center(btn, new Vector2(64, 64), new Vector2(x, 0));
        btn.Pressed += () => SwitchHero(direction);
        AddChild(btn);
        return btn;
    }

    // 阶段1：依次播放 4 帧动画
    private async Task PlayAnimationFrames()
    {
        for (int i = 0; i < FramePaths.Length; i++)
        {
            ShowImage(FramePaths[i]);
            await ToSignal(GetTree().CreateTimer(FrameDurations[i]), "timeout");
        }
    }

    private void ShowImage(string path)
    {
        var tex = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
        if (tex != null)
        {
            _image.Texture = tex;
        }
    }

    // 阶段2：进入选择模式
    private void EnterSelectMode()
    {
        _currentIndex = 0;
        UpdateSelectDisplay();
        _leftArrow.Visible = true;
        _rightArrow.Visible = true;
        _confirmButton.Visible = true;
        _hintLabel.Visible = true;
    }

    private void SwitchHero(int direction)
    {
        _currentIndex = (_currentIndex + direction + Heroes.Length) % Heroes.Length;
        UpdateSelectDisplay();
        // 切换音（左右箭头 / 左右方向键共用这里）
        ModAudio.PlayOneShot(Omnitrix.SwitchSfxPath);
    }

    private void UpdateSelectDisplay()
    {
        ShowImage(ChoosePaths[_currentIndex]);
    }

    private void OnConfirmPressed()
    {
        // 流派不再由玩家选：变身时随机决定（见 Omnitrix.Transform），这里统一给默认值
        _tcs?.TrySetResult(new HeroSelection(Heroes[_currentIndex], AlienSpecialization.Blast));
    }

    public override void _Input(InputEvent @event)
    {
        if (_tcs == null || _forcedHero.HasValue || !_confirmButton.Visible) return;

        if (@event is InputEventKey { Pressed: true, Echo: false } key)
        {
            switch (key.Keycode)
            {
                case Key.Left:
                    SwitchHero(-1);
                    break;
                case Key.Right:
                    SwitchHero(1);
                    break;
                case Key.Enter or Key.Space or Key.KpEnter:
                    OnConfirmPressed();
                    break;
                case Key.Escape:
                    _tcs.TrySetResult(null);
                    break;
            }
        }
    }
}
