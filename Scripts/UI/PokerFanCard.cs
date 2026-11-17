using Godot;

namespace OtherworldTreasures.Scripts.UI;

// A physical card in the fan; no opaque menu tile behind its artwork.
public partial class PokerFanCard : TextureButton
{
    private Vector2 _restPosition;
    private float _restRotation;
    private bool _selected, _hovered;
    private StyleBoxFlat _selectionOutline = null!;

    public override void _Ready()
    {
        FocusMode = FocusModeEnum.All;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        PivotOffset = new Vector2(Size.X / 2f, Size.Y * .94f);
        _selectionOutline = new StyleBoxFlat {
            DrawCenter = false, BgColor = Colors.Transparent, BorderColor = new Color("f4d690"),
            BorderWidthLeft = 3, BorderWidthTop = 3, BorderWidthRight = 3, BorderWidthBottom = 3,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            ShadowColor = Colors.Transparent, ShadowSize = 0 };
        // Keep the card artwork opaque and the selection border hollow;
        // per-card shadows otherwise cover neighbouring cards in the fan.
        MouseEntered += () => Hover(true);
        MouseExited += () => Hover(false);
        FocusEntered += QueueRedraw;
        FocusExited += QueueRedraw;
    }
    public void SetRestPose(Vector2 position, float rotation, bool immediate)
    {
        _restPosition = position; _restRotation = rotation;
        if (immediate) { Position = position; Rotation = rotation; }
    }
    public void SetSelected(bool selected)
    {
        if (_selected == selected) return;
        _selected = selected; QueueRedraw();
    }
    private void Hover(bool value)
    {
        if (_hovered == value) return;
        _hovered = value; QueueRedraw();
    }
    public override void _Process(double delta)
    {
        float weight = 1f - Mathf.Exp(-18f * (float)delta);
        // Only selected cards lift; hover never changes card size or the fan stacking order.
        var aim = _restPosition - new Vector2(0, _selected ? 46 : 0);
        Position = Position.Lerp(aim, weight);
        Rotation = Mathf.LerpAngle(Rotation, _restRotation, weight);
    }
    public override void _Draw()
    {
        if ((_selected || _hovered || HasFocus()) && _selectionOutline != null)
            DrawStyleBox(_selectionOutline, new Rect2(new Vector2(2, 2), Size - new Vector2(4, 4)));
    }
}
