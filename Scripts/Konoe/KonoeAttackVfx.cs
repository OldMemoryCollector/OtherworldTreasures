using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using STS2RitsuLib.Scaffolding.Visuals.Definition;

namespace OtherworldTreasures.Scripts.Konoe;

// Native scenes are the shared editor/runtime source for body poses, hand position,
// effect scale and emission timing. Combination magic never enters this path.
public static class KonoeAttackVfx
{
    public const string WaterPath = "res://OtherworldTreasures/scenes/vfx/konoe/water_hit.tscn";
    public const string WindPath = "res://OtherworldTreasures/scenes/vfx/konoe/wind_hit.tscn";
    public const string FirePath = "res://OtherworldTreasures/scenes/vfx/konoe/fire_hit.tscn";
    public const string BlackStonePath = "res://OtherworldTreasures/scenes/vfx/konoe/black_stone_hit.tscn";
    private const string ActionRoot = "res://OtherworldTreasures/scenes/characters/nine/attacks/";
    private static readonly Dictionary<string, PackedScene> Scenes = new();
    public static string ActionPath(string effectPath) => ActionRoot + (effectPath switch
    {
        WaterPath => "water", WindPath => "wind", FirePath => "fire", BlackStonePath => "black_stone",
        _ => throw new ArgumentOutOfRangeException(nameof(effectPath), effectPath, "Custom attacks require their own action scene.")
    }) + "_attack.tscn";
    public static string AnimName(string effectPath) => effectPath switch
    {
        WaterPath => "WaterAttack", WindPath => "WindAttack", FirePath => "FireAttack", _ => "BlackStoneAttack"
    };
    public static void Preload()
    {
        foreach (var path in new[] { WaterPath, WindPath, FirePath, BlackStonePath })
        {
            LoadScene(path);
            LoadScene(ActionPath(path));
        }
    }
    private static PackedScene LoadScene(string path)
    {
        if (!Scenes.TryGetValue(path, out var scene)) Scenes[path] = scene = GD.Load<PackedScene>(path);
        return scene;
    }
    private static int Track(Animation animation, string path)
        => animation.FindTrack(new NodePath(path), Animation.TrackType.Value);
    private static float EmissionDelay(Animation animation)
    {
        int track = Track(animation, "CastPoint/Effect:visible");
        for (int i = 0; i < animation.TrackGetKeyCount(track); i++)
            if (animation.TrackGetKeyValue(track, i).AsBool()) return (float)animation.TrackGetKeyTime(track, i);
        throw new InvalidOperationException("Attack scene has no effect emission key.");
    }
    public static float EmissionDelay(string effectPath)
    {
        var action = LoadScene(ActionPath(effectPath)).Instantiate<Node2D>();
        float delay = EmissionDelay(action.GetNode<AnimationPlayer>("AnimationPlayer").GetAnimation("attack"));
        action.Free();
        return delay;
    }
    public static void AddCue(VisualCueSetBuilder cues, string cueName, string effectPath)
    {
        var action = LoadScene(ActionPath(effectPath)).Instantiate<Node2D>();
        var pose = action.GetNode<AnimatedSprite2D>("Pose");
        var animation = action.GetNode<AnimationPlayer>("AnimationPlayer").GetAnimation("attack");
        int track = Track(animation, "Pose:frame"), visibleTrack = Track(animation, "Pose:visible");
        double end = animation.Length;
        for (int i = 0; i < animation.TrackGetKeyCount(visibleTrack); i++)
            if (!animation.TrackGetKeyValue(visibleTrack, i).AsBool()) { end = animation.TrackGetKeyTime(visibleTrack, i); break; }
        cues.Sequence(cueName, sequence =>
        {
            for (int i = 0; i < animation.TrackGetKeyCount(track); i++)
            {
                double start = animation.TrackGetKeyTime(track, i);
                double next = i + 1 < animation.TrackGetKeyCount(track) ? animation.TrackGetKeyTime(track, i + 1) : end;
                var texture = pose.SpriteFrames.GetFrameTexture(pose.Animation, animation.TrackGetKeyValue(track, i).AsInt32());
                sequence.Frame(texture.ResourcePath, (float)(next - start), new VisualNodeStyle
                {
                    Position = pose.Position, Scale = pose.Scale, Centered = pose.Centered, Offset = pose.Offset,
                    FlipH = pose.FlipH, FlipV = pose.FlipV, RotationRadians = pose.Rotation
                });
            }
        });
        action.Free();
    }
    public static Node2D? CreateAction(string primaryPath, Creature attacker, IReadOnlyList<string> effectPaths)
    {
        if (NCombatRoom.Instance?.GetCreatureNode(attacker) is not { } attackerNode) return null;
        var action = LoadScene(ActionPath(primaryPath)).Instantiate<Node2D>();
        // Preview body is driven by the same timeline, but the real body uses Ritsu cues.
        action.GetNode<AnimatedSprite2D>("Pose").SelfModulate = Colors.Transparent;
        var player = action.GetNode<AnimationPlayer>("AnimationPlayer");
        if (effectPaths.Count > 1)
        {
            var animation = (Animation)player.GetAnimation("attack").Duplicate();
            var library = new AnimationLibrary(); library.AddAnimation("attack", animation);
            player.RemoveAnimationLibrary(""); player.AddAnimationLibrary("", library);
            float emission = EmissionDelay(animation);
            int number = 0;
            foreach (var path in effectPaths.Where(p => p != primaryPath))
            {
                var extra = LoadScene(path).Instantiate<AnimatedSprite2D>();
                extra.Name = "ExtraEffect" + number++; extra.Autoplay = ""; extra.Visible = false;
                action.GetNode<Marker2D>("CastPoint").AddChild(extra);
                string node = "CastPoint/" + extra.Name;
                int visible = animation.AddTrack(Animation.TrackType.Value);
                animation.TrackSetPath(visible, new NodePath(node + ":visible"));
                animation.ValueTrackSetUpdateMode(visible, Animation.UpdateMode.Discrete);
                animation.TrackInsertKey(visible, 0, false); animation.TrackInsertKey(visible, emission, true);
                int frame = animation.AddTrack(Animation.TrackType.Value);
                animation.TrackSetPath(frame, new NodePath(node + ":frame"));
                animation.ValueTrackSetUpdateMode(frame, Animation.UpdateMode.Discrete);
                double time = emission;
                for (int i = 0; i < extra.SpriteFrames.GetFrameCount("hit"); i++)
                {
                    animation.TrackInsertKey(frame, time, i);
                    time += extra.SpriteFrames.GetFrameDuration("hit", i) / extra.SpriteFrames.GetAnimationSpeed("hit");
                }
                animation.TrackInsertKey(visible, time, false);
                animation.Length = (float)Math.Max(animation.Length, time + .02);
            }
        }
        action.Ready += () =>
        {
            action.GlobalTransform = attackerNode.Visuals.GlobalTransform * action.Transform;
            // Ritsu may reverse the body sprite independently of the visuals root.
            if (attackerNode.Body is Sprite2D { FlipH: true }) action.Scale *= new Vector2(-1, 1);
            player.AnimationFinished += _ => action.QueueFree();
            player.Play("attack"); player.Advance(0);
        };
        return action;
    }
}
