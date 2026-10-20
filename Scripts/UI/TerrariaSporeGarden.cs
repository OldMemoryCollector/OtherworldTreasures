using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Rooms;
using OtherworldTreasures.Scripts.Relics;

namespace OtherworldTreasures.Scripts.UI;

// Local presentation only. This RNG never enters the run's gameplay RNG streams.
public partial class TerrariaSporeGarden : Node2D
{
    private sealed class Spore(Vector2 position, Vector2 velocity, int image, float phase)
    {
        internal Vector2 Position = position, Velocity = velocity;
        internal float Age, Phase = phase;
        internal int Image = image;
    }
    private sealed class Cloud(Vector2 position, Vector2 velocity, int image)
    {
        internal Vector2 Position = position, Velocity = velocity;
        internal float Age;
        internal int Image = image;
    }
    private readonly List<Spore> _spores = new();
    private readonly List<Cloud> _clouds = new();
    private readonly System.Random _visualRandom = new(294201026);
    private float _spawnBudget, _time;
    internal int SporeCount => _spores.Count;
    internal int CloudCount => _clouds.Count;
    private float Between(float min, float max) => min + (float)_visualRandom.NextDouble() * (max - min);
    public override void _Ready()
    {
        TextureFilter = TextureFilterEnum.Nearest;
        CombatManager.Instance.CombatWon += EndCombat;
        CombatManager.Instance.CombatEnded += EndCombat;
        Callable.From(() =>
        {
            if (GetParent() is NCreature owner && owner.Visuals != null)
                owner.MoveChild(this, owner.Visuals.GetIndex() + 1);
        }).CallDeferred();
    }
    public override void _ExitTree()
    {
        CombatManager.Instance.CombatWon -= EndCombat;
        CombatManager.Instance.CombatEnded -= EndCombat;
    }
    private void EndCombat(CombatRoom room) { Clear(); Visible = false; }
    internal void Clear() { _spores.Clear(); _clouds.Clear(); _spawnBudget = 0; QueueRedraw(); }
    public override void _Process(double delta)
    {
        if (GetParent() is not NCreature node) return;
        var relic = node.Entity?.Player?.Relics.OfType<SporeSac>().FirstOrDefault();
        if (relic == null || node.Entity?.IsAlive != true || CombatManager.Instance.IsOverOrEnding)
        { Clear(); Visible = false; return; }
        Visible = true;
        if (node.Visuals?.Bounds is { } bounds)
            Position = node.GetGlobalTransform().AffineInverse() * bounds.GetGlobalRect().GetCenter() + new Vector2(0, -12);
        Advance(delta, relic.AvailablePoison);
    }
    // The native runtime and the visual verification harness use this same step.
    internal void Advance(double delta, int desired)
    {
        float step = (float)delta; _time += step;
        foreach (var spore in _spores) { spore.Age += step; spore.Position += spore.Velocity * step; }
        foreach (var cloud in _clouds) { cloud.Age += step; cloud.Position += cloud.Velocity * step; }
        _spores.RemoveAll(spore => spore.Age >= 3f);
        _clouds.RemoveAll(cloud => cloud.Age >= 1.5f);
        while (_spores.Count > desired) _spores.RemoveAt(_spores.Count - 1);
        _spawnBudget = Math.Min(_spawnBudget + step * 40f, Math.Max(4, desired));
        while (_spores.Count < desired && _spawnBudget >= 1f)
        {
            _spawnBudget--;
            _spores.Add(new Spore(new Vector2(Between(-165, 165), Between(-155, 55)),
                new Vector2(Between(-5, 5), Between(-4, 2)), _visualRandom.Next(2), Between(0, Mathf.Tau)));
        }
        QueueRedraw();
    }
    internal void Release(int amount)
    {
        for (int i = 0; i < amount; i++)
        {
            var position = i < _spores.Count ? _spores[i].Position : new Vector2(Between(-140, 140), Between(-130, 40));
            _clouds.Add(new Cloud(position, new Vector2(Between(-18, 18), Between(-18, 6)), _visualRandom.Next(3)));
        }
        _spores.Clear(); _spawnBudget = 0;
        QueueRedraw();
    }
    public override void _Draw()
    {
        foreach (var spore in _spores)
        {
            float alpha = Mathf.Clamp(Math.Min(spore.Age / .22f, (3f - spore.Age) / .45f), 0, 1);
            var offset = new Vector2(Mathf.Sin(_time * .9f + spore.Phase) * 7, Mathf.Cos(_time + spore.Phase) * 5);
            float size = 26f * (.75f + .25f * alpha);
            DrawTextureRect(TerrariaRelicVfx.Texture($"spore_{spore.Image + 1}"),
                new Rect2(spore.Position + offset - Vector2.One * size / 2, Vector2.One * size), false, new Color(1, 1, 1, alpha));
        }
        foreach (var cloud in _clouds)
        {
            float alpha = Mathf.Clamp((1.5f - cloud.Age) / .7f, 0, 1);
            float size = 56f + cloud.Age * 20f;
            int image = (cloud.Image + (int)(cloud.Age * 8f)) % 3 + 1;
            DrawTextureRect(TerrariaRelicVfx.Texture($"cloud_{image}"),
                new Rect2(cloud.Position - Vector2.One * size / 2, Vector2.One * size), false, new Color(1, 1, 1, alpha));
        }
    }
}
