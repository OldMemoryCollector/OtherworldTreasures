using MegaCrit.Sts2.Core.Models;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.HoverTips;
using OtherworldTreasures.Scripts.Events;
using OtherworldTreasures.Scripts.Potions;

namespace OtherworldTreasures.Scripts.UI;

public partial class Scp294BrewPreviewIcon : TextureRect
{
    private NEventOptionButton? _button;
    private Scp294? _event;
    private PotionModel? _brew;
    private bool _rainbow;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
        Callable.From(Initialize).CallDeferred();
    }

    private void Initialize()
    {
        if (GetParent() is not NEventOptionButton button || button.Event is not Scp294 eventModel ||
            !eventModel.TryGetBrewPreview(button.Option, out var brew))
        {
            QueueFree();
            return;
        }
        _button = button;
        _event = eventModel;
        _brew = brew;
        Texture = brew.Image;
        ExpandMode = ExpandModeEnum.IgnoreSize;
        StretchMode = StretchModeEnum.KeepAspectCentered;
        Size = new Vector2(44, 44);
        var text = button.GetNode<MegaRichTextLabel>("%Text");
        Vector2 start = text.Position;
        text.OffsetLeft += 52;
        Position = start + new Vector2(0, (text.Size.Y - Size.Y) * 0.5f);
        Visible = true;
        RefreshColors();
    }

    public override void _Process(double delta)
    {
        if (_brew != null && _rainbow != Scp294Settings.RainbowValues) RefreshColors();
    }

    private void RefreshColors()
    {
        _rainbow = Scp294Settings.RainbowValues;
        if (_button == null || _event == null || _brew == null) return;
        _button.Option.HoverTips = new IHoverTip[] { Scp294.BrewPreviewHoverTip(_brew, _button.Option.TextKey) };
    }
}
