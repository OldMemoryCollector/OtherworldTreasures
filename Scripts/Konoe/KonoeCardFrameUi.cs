using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace OtherworldTreasures.Scripts.Konoe;

// Decorative skin only. Native frame, rarity banner, energy, labels, highlights
// and ancient-card effects remain owned and rendered by NCard.
public partial class KonoeCardFrameUi : Node
{
    internal const string ScenePath = "res://OtherworldTreasures/scenes/ui/konoe/card_frame_overlay.tscn";
    private static PackedScene? _scene;
    private Control? _underlay;
    private Control? _ornaments;
    public override void _Process(double delta)
    {
        if (GetParent() is not NCard view) return;
        bool visible = view.Model is { Rarity: not CardRarity.Token } card
            && card.VisualCardPool is KonoeCardPool && view.Visibility == ModelVisibility.Visible;
        if (visible && _underlay == null)
        {
            // Ready attachments run before NCard receives its model. Mount lazily
            // under Body so movement, fan rotation, scaling and fade are inherited.
            _scene ??= GD.Load<PackedScene>(ScenePath);
            _underlay = _scene.Instantiate<Control>();
            _underlay.Name = "KonoeCardFrameUnderlay";
            _ornaments = _underlay.GetNode<Control>("Ornaments");
            view.Body.AddChild(_underlay);
            view.Body.MoveChild(_underlay, view.Body.GetNode("DescriptionLabel").GetIndex());
            // Seal and side flames sit under text and native portrait borders;
            // rails sit over banner but under title,
            // type plaque and energy. No native texture or material is replaced.
            _ornaments.Reparent(view.Body, false);
            _ornaments.Name = "KonoeCardOrnaments";
            view.Body.MoveChild(_ornaments, view.Body.GetNode("TitleLabel").GetIndex());
        }
        if (_underlay != null) _underlay.Visible = visible;
        if (_ornaments != null) _ornaments.Visible = visible;
    }
}
