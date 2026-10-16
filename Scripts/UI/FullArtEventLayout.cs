using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Nodes.Events;

namespace OtherworldTreasures.Scripts.UI;

internal static class FullArtEventLayout
{
    private static PackedScene? _scene;
    internal static PackedScene Scene => _scene ??= Create();

    private static PackedScene Create()
    {
        // Keep native text and options. The background fills a clipping viewport,
        // preserves aspect ratio and aligns its left edge instead of centering it.
        var layout = PreloadManager.Cache.GetScene(NEventLayout.defaultScenePath).Instantiate<NEventLayout>();
        try
        {
            var portrait = layout.GetNode<TextureRect>("Portrait");
            int portraitIndex = portrait.GetIndex();
            var artViewport = new Control { Name = "OtherworldEventArtViewport", ClipContents = true,
                MouseFilter = Control.MouseFilterEnum.Ignore };
            layout.AddChild(artViewport);
            artViewport.Owner = layout;
            layout.MoveChild(artViewport, portraitIndex);
            artViewport.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            portrait.Reparent(artViewport);
            portrait.Scale = Vector2.One;
            portrait.PivotOffset = Vector2.Zero;
            portrait.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
            portrait.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            portrait.StretchMode = TextureRect.StretchModeEnum.Scale;
            var placement = new FullArtEventBackdrop { Name = "LeftAlignedArt" };
            artViewport.AddChild(placement);
            placement.Owner = layout;
            var scene = new PackedScene();
            var error = scene.Pack(layout);
            if (error != Error.Ok)
            {
                scene.Dispose();
                throw new InvalidOperationException($"Could not prepare the full-art event layout: {error}.");
            }
            return scene;
        }
        finally { layout.Free(); }
    }
}
