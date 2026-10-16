using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Combat;
using OtherworldTreasures.Scripts.Alien;
using OtherworldTreasures.Scripts.Powers;

namespace OtherworldTreasures.Scripts.UI;

// RitsuLib 为每个原版意图节点附加自己的子节点。每帧跟随该图标的浮动位置，
// 只添加红叉；不替换原版攻击数字、鼠标事件或意图列表。
public partial class GhostIntentSeal : Node2D
{
    private NIntent? _intentNode;
    private Sprite2D? _sprite;
    private NCreature? _creature;
    private bool _blocked;
    private bool _dispersing;
    private Texture2D? _disperseTexture;

    public override void _Ready()
    {
        _intentNode = GetParent() as NIntent;
        _sprite = _intentNode?.GetNodeOrNull<Sprite2D>("%Intent");
        for (Node? node = _intentNode; node != null; node = node.GetParent())
            if (node is NCreature creature) { _creature = creature; break; }
        ZIndex = 10;
    }

    public static bool ShouldCrossOut(AlienTerrorPower? fear, IntentType type)
        => fear?.Stage == 1 && type != IntentType.Defend;

    public override void _Process(double delta)
    {
        if (_sprite == null || _creature?.Entity?.Monster is not { } monster || _intentNode == null) return;
        var siblings = _intentNode.GetParent().GetChildren().OfType<NIntent>().ToList();
        int index = siblings.IndexOf(_intentNode);
        var intents = monster.NextMove.Intents;
        if (index < 0 || index >= intents.Count) return;
        bool dispersing = intents[index] is GhostfreakDisperseIntent;
        bool blocked = ShouldCrossOut(_creature.Entity.GetPower<AlienTerrorPower>(), intents[index].IntentType);
        // 父节点是 Control，同样通过 CanvasItem 的全局变换转换坐标。
        if (GetParent() is CanvasItem parent)
            Position = parent.GetGlobalTransform().AffineInverse() * _sprite.GlobalPosition;
        if (_dispersing && !dispersing) _sprite.Visible = true;
        if (dispersing)
        {
            _sprite.Visible = false;
            _disperseTexture ??= GD.Load<Texture2D>("res://OtherworldTreasures/images/powers/ghostfreak/disperse.svg");
        }
        if (_blocked != blocked || _dispersing != dispersing)
        {
            _blocked = blocked;
            _dispersing = dispersing;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        if (_dispersing && _disperseTexture != null)
            DrawTextureRect(_disperseTexture, new Rect2(-32, -32, 64, 64), false);
        if (!_blocked) return;
        var a = new Vector2(-24, -24);
        var b = new Vector2(24, 24);
        DrawLine(a, b, new Color("35090e"), 12, true);
        DrawLine(new Vector2(a.X, b.Y), new Vector2(b.X, a.Y), new Color("35090e"), 12, true);
        DrawLine(a, b, new Color("ff303f"), 7, true);
        DrawLine(new Vector2(a.X, b.Y), new Vector2(b.X, a.Y), new Color("ff303f"), 7, true);
    }
}
