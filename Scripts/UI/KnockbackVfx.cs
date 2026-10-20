using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace OtherworldTreasures.Scripts.UI;

/// <summary>Visual-only knockback movement. No combat state or damage values are changed here.</summary>
internal static partial class KnockbackVfx
{
    // Tunable collision timing: travel 0.10s, hold at collision 0.22s, return 0.12s.
    private const float FirstMoveSeconds = 0.10f;
    private const float CollisionHoldSeconds = 0.22f;
    private const float ReturnSeconds = 0.12f;
    private const float SecondRecoilPixels = 2f;
    private const float NormalRecoilPixels = 3f;
    private const float HeavyRecoilPixels = 4f;

    private static readonly Dictionary<Creature, MotionState> ActiveMotions = new();
    private static bool _warned;

    private sealed class MotionState
    {
        public required Node2D Body { get; init; }
        public required Vector2 StartGlobalPosition { get; init; }
        public required float StartRotation { get; init; }
        public TaskCompletionSource ImpactReached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Tween? Tween { get; set; }
        public Action? TreeExitingHandler { get; set; }
    }

    internal static void ShowSelfHit(Creature target, bool heavy, bool secondSegment = false)
    {
        Guard(() => StartShake(target, heavy, secondSegment));
    }

    /// <summary>Moves the struck unit to the rear enemy and completes when it reaches the collision point.</summary>
    internal static Task ShowPushHit(Creature mover, Creature directionReference, Creature collisionTarget)
    {
        try
        {
            return StartPush(mover, directionReference, collisionTarget);
        }
        catch (Exception exception)
        {
            WarnOnce(exception);
            return Task.CompletedTask;
        }
    }

    internal static void Cancel(Creature creature)
    {
        Guard(() => CancelMotion(creature));
    }

    private static Task StartPush(Creature mover, Creature directionReference, Creature collisionTarget)
    {
        var moverNode = GetCreatureNode(mover);
        var referenceNode = GetCreatureNode(directionReference);
        var collisionNode = GetCreatureNode(collisionTarget);
        var body = moverNode?.Body;
        if (body == null || !mover.IsAlive || referenceNode == null || collisionNode == null)
            return Task.CompletedTask;

        Vector2 direction = referenceNode.Hitbox.GetGlobalRect().GetCenter()
            - moverNode!.Hitbox.GetGlobalRect().GetCenter();
        if (direction.LengthSquared() < 0.001f)
            direction = Vector2.Left;
        direction = -direction.Normalized();

        var state = BeginMotion(mover, body);
        if (!GodotObject.IsInstanceValid(body) || !mover.IsAlive)
        {
            state.ImpactReached.TrySetResult();
            return state.ImpactReached.Task;
        }

        // Translate the body's origin by the hitbox-center delta so the two enemy positions meet.
        Vector2 collisionPosition = state.StartGlobalPosition
            + collisionNode.Hitbox.GetGlobalRect().GetCenter()
            - moverNode.Hitbox.GetGlobalRect().GetCenter();
        var tween = body.CreateTween();
        state.Tween = tween;
        tween.TweenProperty(body, "global_position", collisionPosition, FirstMoveSeconds)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        float angle = direction.X >= 0f ? -6f : 6f;
        tween.Parallel().TweenProperty(body, "rotation", state.StartRotation + Mathf.DegToRad(angle), FirstMoveSeconds)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenCallback(Callable.From(() => Guard(() => state.ImpactReached.TrySetResult())));
        tween.TweenInterval(CollisionHoldSeconds);
        tween.TweenProperty(body, "global_position", state.StartGlobalPosition, ReturnSeconds)
            .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(body, "rotation", state.StartRotation, ReturnSeconds)
            .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        tween.TweenCallback(Callable.From(() => Guard(() => FinishMotion(mover, state))));
        return state.ImpactReached.Task;
    }

    private static void StartShake(Creature creature, bool heavy, bool secondSegment)
    {
        var body = GetCreatureNode(creature)?.Body;
        if (body == null || !creature.IsAlive)
            return;

        var state = BeginMotion(creature, body);
        if (!GodotObject.IsInstanceValid(body) || !creature.IsAlive)
            return;

        var tween = body.CreateTween();
        state.Tween = tween;

        float offset = heavy ? HeavyRecoilPixels : secondSegment ? SecondRecoilPixels : NormalRecoilPixels;
        float step = heavy ? 0.06f : 0.04f;
        Vector2 axis = heavy ? Vector2.Up : Vector2.Right;
        tween.TweenProperty(body, "global_position", state.StartGlobalPosition + axis * offset, step);
        tween.TweenProperty(body, "global_position", state.StartGlobalPosition - axis * offset, step);
        tween.TweenProperty(body, "global_position", state.StartGlobalPosition + axis * (offset * 0.65f), step);
        tween.TweenProperty(body, "global_position", state.StartGlobalPosition, step);
        tween.TweenCallback(Callable.From(() => Guard(() => FinishMotion(creature, state))));
    }

    private static MotionState BeginMotion(Creature creature, Node2D body)
    {
        CancelMotion(creature);
        var state = new MotionState
        {
            Body = body,
            StartGlobalPosition = body.GlobalPosition,
            StartRotation = body.Rotation,
        };
        ActiveMotions[creature] = state;
        state.TreeExitingHandler = () => Guard(() => CancelMotion(creature));
        body.TreeExiting += state.TreeExitingHandler;
        return state;
    }

    private static void FinishMotion(Creature creature, MotionState state)
    {
        if (!ActiveMotions.TryGetValue(creature, out var active) || !ReferenceEquals(active, state))
            return;

        ActiveMotions.Remove(creature);
        state.ImpactReached.TrySetResult();
        RestoreAndDetach(state);
    }

    private static void CancelMotion(Creature creature)
    {
        if (!ActiveMotions.Remove(creature, out var state))
            return;

        if (state.Tween != null && GodotObject.IsInstanceValid(state.Tween))
            state.Tween.Kill();
        state.ImpactReached.TrySetResult();
        RestoreAndDetach(state);
    }

    private static void RestoreAndDetach(MotionState state)
    {
        if (!GodotObject.IsInstanceValid(state.Body))
            return;

        if (state.TreeExitingHandler != null)
            state.Body.TreeExiting -= state.TreeExitingHandler;
        state.Body.GlobalPosition = state.StartGlobalPosition;
        state.Body.Rotation = state.StartRotation;
    }

    private static NCreature? GetCreatureNode(Creature creature)
    {
        var room = NCombatRoom.Instance;
        return room?.GetCreatureNode(creature);
    }

    private static void Guard(Action action)
    {
        try { action(); }
        catch (Exception exception) { WarnOnce(exception); }
    }

    private static void WarnOnce(Exception exception)
    {
        foreach (Creature creature in ActiveMotions.Keys.ToArray())
        {
            try { CancelMotion(creature); }
            catch { /* Best-effort reset; visuals cannot interrupt combat. */ }
        }

        if (_warned)
            return;
        _warned = true;
        try { GD.PushWarning($"Knockback visuals were skipped after an error: {exception.Message}"); }
        catch { /* Rendering must never interrupt combat. */ }
    }
}
