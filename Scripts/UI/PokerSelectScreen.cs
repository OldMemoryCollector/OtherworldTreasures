using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using OtherworldTreasures.Scripts.Poker;

namespace OtherworldTreasures.Scripts.UI;

public partial class PokerSelectScreen : Control, IOverlayScreen
{
    private const int CardsPerFan = 18;
    private static readonly Vector2 CardSize = new(176, 248);
    private static readonly Vector2 StageSize = new(1540, 760);
    private readonly TaskCompletionSource<PokerSelection> _completion = new();
    private readonly HashSet<int> _selected = [];
    private readonly Dictionary<int, PokerFanCard> _cardViews = [];
    private IReadOnlyList<PokerFace> _cards = [];
    private int[] _displaySlots = [];
    private bool _readOnly, _finished;
    private Control _stage = null!, _fan = null!;
    private Label _summary = null!, _selectionCount = null!;
    private Button _confirm = null!, _clear = null!, _all = null!;
    public Task<PokerSelection> Completion => _completion.Task;
    public NetScreenType ScreenType => _readOnly ? NetScreenType.CardPile : NetScreenType.CardSelection;
    public bool UseSharedBackstop => false;
    public Control? DefaultFocusedControl => _confirm;

    internal static PokerSelectScreen Open(IReadOnlyList<PokerFace> cards, bool readOnly)
    {
        var screen = new PokerSelectScreen { Name = "DeathPokerSelection", _cards = cards.ToArray(), _readOnly = readOnly };
        var stack = NOverlayStack.Instance ?? throw new InvalidOperationException("Poker selection requires the native overlay stack");
        stack.Push(screen);
        return screen;
    }
    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        // The stage is transparent. The live combat scene remains visible through the cards.
        _stage = new Control { Name = "PokerHand", Size = StageSize, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_stage);
        Text("死亡扑克", 32, new Vector2(24, 0), new Vector2(700, 46), new Color("efd597"));
        Text($"储备扑克  {_cards.Count} / 54", 22, new Vector2(26, 50), new Vector2(700, 34), new Color("e5e9e6"));
        _fan = new Control { Name = "FannedCards", Position = new Vector2(24, 110), Size = new Vector2(1080, 560), MouseFilter = MouseFilterEnum.Ignore };
        _stage.AddChild(_fan);
        _displaySlots = Enumerable.Range(0, _cards.Count)
            .OrderBy(i => _cards[i].IsJoker ? 100 + _cards[i].Value : _cards[i].SequenceRank)
            .ThenBy(i => _cards[i].Suit).ThenBy(i => i).ToArray();
        // Creation order is the permanent fan order, including pointer hit testing.
        // Selection, hover and keyboard focus must never move a card in front of its neighbours.
        foreach (int index in _displaySlots)
        {
            int slot = index;
            var view = new PokerFanCard {
                Name = $"Poker_{slot}", Size = CardSize,
                TextureNormal = GD.Load<Texture2D>(_cards[slot].ImagePath),
                IgnoreTextureSize = true, StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered };
            view.Pressed += () => { if (!_selected.Add(slot)) _selected.Remove(slot); Refresh(); };
            _fan.AddChild(view);
            _cardViews.Add(slot, view);
        }
        Text("本次效果", 25, new Vector2(1152, 64), new Vector2(360, 40), new Color("efd597"));
        _selectionCount = Text("", 21, new Vector2(1152, 106), new Vector2(360, 32), new Color("e5e9e6"));
        var summaryScroll = new ScrollContainer {
            Name = "SelectedEffects", Position = new Vector2(1152, 154), Size = new Vector2(360, 492),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true };
        _stage.AddChild(summaryScroll);
        _summary = new Label { CustomMinimumSize = new Vector2(338, 0), AutowrapMode = TextServer.AutowrapMode.WordSmart };
        StyleText(_summary, 22, new Color("f1f1e9"));
        summaryScroll.AddChild(_summary);
        summaryScroll.GetVScrollBar().AddThemeStyleboxOverride("scroll", new StyleBoxEmpty());
        _clear = Action("清空选择", 24, 190, () => { _selected.Clear(); Refresh(); });
        _all = Action("全选", 230, 160, () => { _selected.UnionWith(Enumerable.Range(0, _cards.Count)); Refresh(); });
        _confirm = Action(_readOnly ? "收起扑克" : "打出所选扑克", 1152, 360, () => Finish(Current()));
        LayoutFans(); Refresh(); Reposition();
    }
    private static void StyleText(Label label, int font, Color color)
    {
        label.AddThemeFontSizeOverride("font_size", font);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, .92f));
        label.AddThemeConstantOverride("outline_size", 6);
        label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, .75f));
        label.AddThemeConstantOverride("shadow_offset_x", 2);
        label.AddThemeConstantOverride("shadow_offset_y", 3);
    }
    private Label Text(string text, int font, Vector2 position, Vector2 size, Color color)
    {
        var label = new Label { Text = text, Position = position, Size = size, MouseFilter = MouseFilterEnum.Ignore };
        StyleText(label, font, color); _stage.AddChild(label); return label;
    }
    private Button Action(string text, float x, float width, Action action)
    {
        var button = new Button { Text = text, Position = new Vector2(x, 696), Size = new Vector2(width, 52) };
        button.AddThemeFontSizeOverride("font_size", 24);
        button.AddThemeColorOverride("font_color", new Color("efd597"));
        button.AddThemeColorOverride("font_hover_color", Colors.White);
        button.AddThemeColorOverride("font_outline_color", Colors.Black);
        button.AddThemeConstantOverride("outline_size", 6);
        button.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        button.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
        button.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
        button.Pressed += action; _stage.AddChild(button); return button;
    }
    private void LayoutFans()
    {
        int rows = Math.Max(1, (_cards.Count + CardsPerFan - 1) / CardsPerFan);
        float rowStep = rows == 1 ? 0 : Math.Min(150, (560f - CardSize.Y - 92f) / (rows - 1));
        for (int row = 0; row < rows; row++)
        {
            var slots = _displaySlots.Skip(row * CardsPerFan).Take(CardsPerFan).ToArray();
            float spread = slots.Length <= 1 ? 0 : Math.Min(96, (1080f - CardSize.X - 40) / (slots.Length - 1));
            float width = CardSize.X + spread * Math.Max(0, slots.Length - 1);
            float startX = (1080f - width) / 2;
            float y = rows == 1 ? 138 : 60 + row * rowStep;
            for (int n = 0; n < slots.Length; n++)
            {
                float arc = slots.Length <= 1 ? 0 : (2f * n / (slots.Length - 1) - 1f);
                _cardViews[slots[n]].SetRestPose(new Vector2(startX + n * spread, y + arc * arc * 24), arc * .15f, true);
            }
        }
    }
    private PokerSelection Current() => PokerRules.AutoGroup(_cards, _selected);
    private void Refresh()
    {
        foreach (var (index, view) in _cardViews) view.SetSelected(_selected.Contains(index));
        var choice = Current(); var all = choice.AllIndexes;
        _selectionCount.Text = all.Length == 0 ? "点击扑克自由选择" : $"已选 {all.Length} 张";
        var lines = new List<string>();
        foreach (var group in choice.Groups) lines.Add(PokerEffects.Summary(group, _cards));
        int scatter = choice.Scatter.Count(i => !_cards[i].IsJoker);
        if (scatter > 0) lines.Add($"散牌：造成{scatter * 3}点伤害");
        int small = all.Count(i => _cards[i].Value == 52), big = all.Count(i => _cards[i].Value == 53), others = Math.Max(0, all.Length - 1);
        if (small > 0) lines.Add($"小王 ×{small}：每张施加{others}次3层负面状态，并造成{others}次1点伤害");
        if (big > 0) lines.Add($"大王 ×{big}：每张获得{others}次3层正面状态，再对所有敌人造成{others}点伤害");
        if (small > 0 && big > 0) lines.Add("双王共鸣：额外造成60点无视格挡的群体伤害");
        _summary.Text = lines.Count == 0 ? "选中的扑克会直接决定本次效果。" : string.Join("\n\n", lines);
        _clear.Disabled = _selected.Count == 0;
        _all.Disabled = _selected.Count == _cards.Count;
        _confirm.Disabled = !_readOnly && all.Length == 0;
    }
    private void Reposition()
    {
        if (_stage == null) return;
        var size = Size.X > 0 && Size.Y > 0 ? Size : GetViewportRect().Size;
        float top = 120;
        if (NRun.Instance?.GlobalUi?.RelicInventory is { } inventory && inventory.GetParent() is CanvasItem parent)
        {
            var bottom = GetGlobalTransformWithCanvas().AffineInverse() * (parent.GetGlobalTransformWithCanvas() * inventory.GetBottomOfInventory());
            top = Math.Max(top, bottom.Y + 24);
        }
        top = Math.Min(top, Math.Max(0, size.Y - 100));
        float available = Math.Max(1, size.Y - top - 24);
        float scale = Math.Min(1, Math.Min(Math.Max(1, size.X - 48) / StageSize.X, available / StageSize.Y));
        _stage.Scale = Vector2.One * scale;
        _stage.Position = new Vector2((size.X - StageSize.X * scale) / 2, top + (available - StageSize.Y * scale) / 2);
    }
    public override void _Process(double delta)
    {
        Reposition();
        if (!CombatManager.Instance.IsInProgress && !_finished) Finish(PokerSelection.Empty);
    }
    private void Finish(PokerSelection selection)
    {
        if (_finished) return;
        _finished = true; NOverlayStack.Instance?.Remove(this);
        _completion.TrySetResult(_readOnly ? PokerSelection.Empty : selection);
    }
    public override void _UnhandledKeyInput(InputEvent e)
    {
        if (Visible && e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        { Finish(PokerSelection.Empty); GetViewport().SetInputAsHandled(); }
    }
    public override void _ExitTree() => _completion.TrySetResult(PokerSelection.Empty);
    public void AfterOverlayOpened() { }
    public void AfterOverlayClosed() { if (!_finished) _completion.TrySetResult(PokerSelection.Empty); QueueFree(); }
    public void AfterOverlayShown() { Show(); _confirm?.GrabFocus(); }
    public void AfterOverlayHidden() => Hide();
}
