using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;
using MegaCrit.Sts2.Core.TestSupport;

namespace OtherworldTreasures.Scripts.UI;

internal static class TerrariaRelicVfx
{
    private static readonly Dictionary<string, Texture2D> Textures = new();
    internal static Texture2D Texture(string name)
    {
        if (!Textures.TryGetValue(name, out var image))
            Textures[name] = image = GD.Load<Texture2D>($"res://OtherworldTreasures/images/vfx/terraria/{name}.png");
        return image;
    }
    internal static void ReleaseSpores(Creature owner, int amount)
    {
        if (TestMode.IsOn) return;
        NCombatRoom.Instance?.GetCreatureNode(owner)?.GetChildren().OfType<TerrariaSporeGarden>().FirstOrDefault()?.Release(amount);
    }
    internal static async Task FlyBee(Creature owner, Creature target)
    {
        if (TestMode.IsOn || NCombatRoom.Instance is not { } room) return;
        var from = room.GetCreatureNode(owner); var to = room.GetCreatureNode(target);
        if (from == null || to == null) return;
        float duration = SaveManager.Instance.PrefsSave.FastMode == FastModeType.Normal ? .66f : .28f;
        var bee = new TerrariaBeeFlight(from.VfxSpawnPosition, to.VfxSpawnPosition, duration);
        room.CombatVfxContainer.AddChildSafely(bee);
        await bee.Finished;
    }
}

internal enum TerrariaBeeFlightStyle { Curved, RushAndReturn }

internal partial class TerrariaBeeFlight(Vector2 from, Vector2 to, float duration) : Node2D
{
    private readonly TaskCompletionSource _finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
    // Local variation belongs to the animation and never advances the run RNG.
    private readonly float _bend = 100f + System.Random.Shared.NextSingle() * 65f;
    private readonly float _sway = 6f + System.Random.Shared.NextSingle() * 8f;
    private readonly TerrariaBeeFlightStyle _style = System.Random.Shared.Next(2) == 0
        ? TerrariaBeeFlightStyle.Curved : TerrariaBeeFlightStyle.RushAndReturn;
    private readonly float _phase = System.Random.Shared.NextSingle() * Mathf.Tau;
    private const int RouteSamples = 192;
    private readonly Vector2[] _route = new Vector2[RouteSamples + 1];
    private readonly float[] _travelTimes = new float[RouteSamples + 1];
    private float _routeTime;
    private Vector2 _rushTurn;
    private float _rushTurnProgress;
    private float _age;
    private float _facing = to.X < from.X ? -1f : 1f;
    internal Task Finished => _finished.Task;
    internal TerrariaBeeFlight(Vector2 from, Vector2 to, float duration, TerrariaBeeFlightStyle style)
        : this(from, to, duration) { _style = style; }
    public override void _Ready()
    {
        TextureFilter = TextureFilterEnum.Nearest;
        ZIndex = 4;
        GlobalPosition = from;
        BuildRoute();
    }
    public override void _ExitTree() => _finished.TrySetResult();
    private Vector2 PositionAt(float t)
    {
        var delta = to - from;
        if (_style == TerrariaBeeFlightStyle.RushAndReturn)
        {
            // Both legs have the same travel speed; reverse in place at the overshoot point.
            return t <= _rushTurnProgress
                ? from.Lerp(_rushTurn, _rushTurnProgress > .0001f ? t / _rushTurnProgress : 1f)
                : _rushTurn.Lerp(to, (t - _rushTurnProgress) / Mathf.Max(1f - _rushTurnProgress, .0001f));
        }
        float distance = delta.Length();
        var direction = distance > .001f ? delta / distance : Vector2.Right;
        var normal = new Vector2(direction.Y, -direction.X);
        float bend = Mathf.Min(_bend, distance * .42f);
        // Opposing control points form a broad S; the fading sway adds light drift.
        var first = from + delta * .23f + normal * bend;
        var second = from + delta * .70f - normal * bend * .62f;
        float inverse = 1f - t;
        var curve = inverse * inverse * inverse * from
            + 3f * inverse * inverse * t * first
            + 3f * inverse * t * t * second + t * t * t * to;
        float envelope = Mathf.Sin(Mathf.Pi * t);
        return curve + normal * (Mathf.Sin(Mathf.Tau * t + _phase)
            * Mathf.Min(_sway, distance * .09f) * envelope * envelope);
    }
    private void BuildRoute()
    {
        if (_style == TerrariaBeeFlightStyle.RushAndReturn)
        {
            float heading = to.X < from.X ? -1f : 1f;
            float overshoot = Mathf.Clamp(Mathf.Abs(to.X - from.X) * .16f, 35f, 82f);
            _rushTurn = to + new Vector2(heading * overshoot, -32f);
            float outbound = from.DistanceTo(_rushTurn), inbound = _rushTurn.DistanceTo(to);
            _rushTurnProgress = outbound / Mathf.Max(outbound + inbound, .001f);
            return;
        }
        var speeds = new float[RouteSamples + 1];
        for (int i = 0; i <= RouteSamples; i++) _route[i] = PositionAt((float)i / RouteSamples);
        for (int i = 0; i <= RouteSamples; i++)
        {
            int previous = Math.Max(0, i - 2), next = Math.Min(RouteSamples, i + 2);
            var entering = _route[i] - _route[previous];
            var leaving = _route[next] - _route[i];
            float distance = (entering.Length() + leaving.Length()) / 2f;
            float curvature = distance > .01f ? Mathf.Abs(entering.AngleTo(leaving)) / distance : 0f;
            speeds[i] = Mathf.Clamp(1f / (1f + curvature * 140f), .30f, 1f);
        }
        _travelTimes[0] = 0f;
        for (int i = 1; i <= RouteSamples; i++)
            _travelTimes[i] = _travelTimes[i - 1] + _route[i - 1].DistanceTo(_route[i]) / ((speeds[i - 1] + speeds[i]) / 2f);
        _routeTime = _travelTimes[RouteSamples];
    }
    private Vector2 SampleRoute(float elapsed)
    {
        if (_style == TerrariaBeeFlightStyle.RushAndReturn) return PositionAt(Mathf.Clamp(elapsed, 0f, 1f));
        if (elapsed <= 0f || _routeTime <= .001f) return from;
        if (elapsed >= 1f) return to;
        float time = elapsed * _routeTime;
        int index = Array.BinarySearch(_travelTimes, time);
        if (index >= 0) return _route[index];
        index = Math.Clamp(~index, 1, RouteSamples);
        float span = _travelTimes[index] - _travelTimes[index - 1];
        return _route[index - 1].Lerp(_route[index], span > .0001f ? (time - _travelTimes[index - 1]) / span : 1f);
    }
    public override void _Process(double delta)
    {
        _age += (float)delta;
        float elapsed = Mathf.Clamp(_age / Mathf.Max(duration, .01f), 0, 1);
        GlobalPosition = SampleRoute(elapsed);
        var tangent = SampleRoute(Mathf.Min(1f, elapsed + .002f)) - SampleRoute(Mathf.Max(0f, elapsed - .002f));
        if (_style == TerrariaBeeFlightStyle.RushAndReturn)
            _facing = (to.X < from.X ? -1f : 1f) * (elapsed >= _rushTurnProgress ? -1f : 1f);
        else if (Mathf.Abs(tangent.X) > .001f) _facing = tangent.X < 0f ? -1f : 1f;
        // Only the horizontal facing flips at the turn point; the sprite stays upright.
        Rotation = 0f;
        QueueRedraw();
        if (elapsed >= 1) { _finished.TrySetResult(); QueueFree(); }
    }
    public override void _Draw()
    {
        var image = TerrariaRelicVfx.Texture("bee");
        int frameWidth = image.GetHeight();
        int frames = Math.Max(1, image.GetWidth() / frameWidth);
        const float wingCycleSeconds = .1f;
        int frame = (int)(_age / wingCycleSeconds * frames) % frames;
        float width = _facing * 40f;
        DrawTextureRectRegion(image, new Rect2(new Vector2(-width / 2, -20), new Vector2(width, 40)),
            new Rect2(frame * frameWidth, 0, frameWidth, image.GetHeight()));
    }
}
