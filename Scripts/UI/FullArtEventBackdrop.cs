using Godot;

namespace OtherworldTreasures.Scripts.UI;

// The native layout assigns/replaces Portrait.Texture after Ready. Observe that
// reference and available size so initial load, page changes and resizing all fit.
public partial class FullArtEventBackdrop : Node
{
    private Control _viewport = null!;
    private TextureRect _portrait = null!;
    private Texture2D? _lastTexture;
    private Vector2 _lastSize;

    public override void _Ready()
    {
        _viewport = GetParent<Control>();
        _portrait = _viewport.GetNode<TextureRect>("Portrait");
        UpdatePlacement();
    }

    public override void _Process(double delta) => UpdatePlacement();

    internal static Rect2 CoverLeft(Vector2 imageSize, Vector2 available)
    {
        if (imageSize.X <= 0 || imageSize.Y <= 0 || available.X <= 0 || available.Y <= 0) return new Rect2();
        float scale = Mathf.Max(available.X / imageSize.X, available.Y / imageSize.Y);
        var size = imageSize * scale;
        return new Rect2(new Vector2(0, (available.Y - size.Y) * .5f), size);
    }

    private void UpdatePlacement()
    {
        if (_portrait.Texture == null || (_lastTexture == _portrait.Texture && _lastSize == _viewport.Size)) return;
        _lastTexture = _portrait.Texture;
        _lastSize = _viewport.Size;
        var rect = CoverLeft(_lastTexture.GetSize(), _lastSize);
        _portrait.Position = rect.Position;
        _portrait.Size = rect.Size;
    }
}
