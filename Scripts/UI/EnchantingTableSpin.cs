using Godot;
using MegaCrit.Sts2.Core.Nodes.Relics;
using OtherworldTreasures.Scripts.Relics;
using STS2RitsuLib.Scaffolding.Godot.NodeAttachments;

namespace OtherworldTreasures.Scripts.UI;

public partial class EnchantingTableSpin : Node
{
    public override void _Ready()
    {
        Callable.From(Initialize).CallDeferred();
    }

    private void Initialize()
    {
        try
        {
            if (GetParent() is not NRelic relic || relic.GetParent() is not NRelicInventoryHolder) { QueueFree(); return; }
            if (relic.Model is not EnchantingTable) { QueueFree(); return; }
            var frames = new SpriteFrames();
            frames.AddAnimation("spin");
            frames.SetAnimationSpeed("spin", 12f);
            frames.SetAnimationLoop("spin", true);
            for (int i = 1; i <= 32; i++)
            {
                string path = $"res://OtherworldTreasures/images/relics/EnchantingTable_anim/{i:00}.png";
                if (!ResourceLoader.Exists(path)) return;
                frames.AddFrame("spin", ResourceLoader.Load<Texture2D>(path));
            }
            var icon = relic.Icon;
            if (icon == null) return;
            var sprite = new AnimatedSprite2D { SpriteFrames = frames, Animation = "spin", Centered = true, Scale = Vector2.One * 0.25f, Position = icon.Size / 2f, TextureFilter = CanvasItem.TextureFilterEnum.Linear };
            sprite.Play();
            icon.AddChild(sprite);
            icon.SelfModulate = Colors.Transparent;
            relic.Outline.Visible = false;
        }
        catch
        {
            QueueFree();
        }
    }
}
