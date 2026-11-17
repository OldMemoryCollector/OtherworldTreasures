using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace OtherworldTreasures.Scripts.Konoe;

// The ring is a stationary support surface. New elements travel counterclockwise
// once, then their mesh remains untouched until the element state changes.
public partial class KonoeElementRibbons : Node2D
{
    private readonly List<MeshInstance2D> _front = [];
    private readonly float[] _arrivalElapsed = [-1, -1, -1];
    private int _lastPacked = -1;
    private int _lastGainSequence;
    private Sprite2D? _fairy;
    private Label? _storage;
    private int _lastStored = -1;
    private float _time;
    public override void _Ready() => Callable.From(Attach).CallDeferred();
    private void Attach()
    {
        if (_front.Count != 0 || GetParent() is not NCreature creature || creature.Entity?.Player?.Character is not Characters.Konoe || creature.Visuals is not { } visuals) return;
        var material = GD.Load<Shader>("res://OtherworldTreasures/shaders/nine_element_ribbon.gdshader");
        for (int i = 0; i < 3; i++)
        {
            var front = new MeshInstance2D { Name = "KonoeElementFront" + i, Material = new ShaderMaterial { Shader = material }, Visible = false, Mesh = new ArrayMesh() };
            front.TextureFilter = TextureFilterEnum.Nearest;
            visuals.AddChild(front); visuals.MoveChild(front, visuals.Body.GetIndex() + 1);
            _front.Add(front);
            UpdateRibbon(i, 0);
        }
        _fairy = new Sprite2D
        {
            Name = "KonoeSummonedFairy", Texture = GD.Load<Texture2D>("res://OtherworldTreasures/images/characters/nine/elements/fairy.png"),
            RegionEnabled = true, RegionRect = new Rect2(0,0,128,128), Scale = Vector2.One * .55f,
            Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://OtherworldTreasures/shaders/nine_fairy.gdshader") }, Visible = false
        };
        visuals.AddChild(_fairy);
        _storage = new Label { Name = "KonoeElementStorage", Position = new Vector2(-84, -320), MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        _storage.AddThemeFontSizeOverride("font_size", 18);
        _storage.AddThemeColorOverride("font_color", KonoeTheme.Primary);
        visuals.AddChild(_storage);
    }
    public override void _Process(double delta)
    {
        if (_front.Count == 0 || GetParent() is not NCreature creature) return;
        _time += (float)delta;
        if (GodotObject.IsInstanceValid(_fairy))
        {
            _fairy!.Visible = creature.Entity.IsAlive && !CombatManager.Instance.IsOverOrEnding && creature.Entity.Powers.Any(p => p is KonoeFairyPower);
            _fairy.Position = new Vector2(-84 + Mathf.Sin(_time * 1.4f) * 8, -222 + Mathf.Sin(_time * 2.3f) * 6);
            _fairy.RegionRect = new Rect2(((int)(_time * 9) % 2) * 128, 0, 128, 128);
        }
        bool active = creature.Entity.IsAlive && !CombatManager.Instance.IsOverOrEnding;
        if (GodotObject.IsInstanceValid(_storage))
        {
            var slot = creature.Entity.GetPower<KonoeStoragePower>();
            _storage!.Visible = active && slot != null;
            int stored = slot?.PackedElements ?? -1;
            if (_lastStored != stored)
            {
                _lastStored = stored;
                _storage.Text = new MegaCrit.Sts2.Core.Localization.LocString("cards", "KONOE_STORAGE_LABEL").GetFormattedText() + (stored <= 0 ? new MegaCrit.Sts2.Core.Localization.LocString("cards", "KONOE_STORAGE_EMPTY").GetFormattedText() : KonoeBlueSupport.ElementText(stored));
            }
        }
        int packed = active ? KonoeElements.Get(creature.Entity.Player) : 0;
        int sequence = KonoeElements.GainSequence(creature.Entity.Player);
        if (_lastPacked != packed || _lastGainSequence != sequence)
        {
            var elements = KonoeElementRules.Decode(packed);
            int arriving = Math.Clamp(sequence - _lastGainSequence, 0, elements.Count);
            _lastPacked = packed; _lastGainSequence = sequence;
            for (int i = 0; i < 3; i++)
            {
                if (!GodotObject.IsInstanceValid(_front[i])) return;
                bool visible = i < elements.Count;
                _front[i].Visible = visible;
                _arrivalElapsed[i] = visible && i >= elements.Count - arriving ? 0 : -1;
                if (!visible) continue;
                string file = elements[i] switch { KonoeElement.Fire => "pheff_spell00", KonoeElement.Wind => "pheff_spell01", _ => "pheff_spell02" };
                _front[i].Texture = GD.Load<Texture2D>($"res://OtherworldTreasures/images/characters/nine/elements/{file}.png");
                UpdateRibbon(i, _arrivalElapsed[i] >= 0 ? KonoeRibbonGeometry.ArrivalPhase(0) : 0);
            }
        }
        for (int i = 0; i < _front.Count; i++)
        {
            if (_arrivalElapsed[i] < 0 || !_front[i].Visible) continue;
            _arrivalElapsed[i] = Math.Min(_arrivalElapsed[i] + (float)delta, KonoeRibbonGeometry.ArrivalDuration);
            UpdateRibbon(i, KonoeRibbonGeometry.ArrivalPhase(_arrivalElapsed[i]));
            if (_arrivalElapsed[i] >= KonoeRibbonGeometry.ArrivalDuration) _arrivalElapsed[i] = -1;
        }
    }
    public override void _ExitTree()
    {
        foreach (var mesh in _front) if (GodotObject.IsInstanceValid(mesh)) mesh.QueueFree();
        if (GodotObject.IsInstanceValid(_fairy)) _fairy!.QueueFree();
        if (GodotObject.IsInstanceValid(_storage)) _storage!.QueueFree();
        _storage = null; _fairy = null; _front.Clear();
    }
    private void UpdateRibbon(int slot, float phase)
    {
        var strip = KonoeRibbonGeometry.Build(Characters.KonoeAnimations.Scale, slot, phase);
        var vertices = new Vector3[strip.Length];
        var uv = new Vector2[strip.Length];
        var colors = new Color[strip.Length];
        for (int i = 0; i < strip.Length; i++)
        {
            vertices[i] = new Vector3(strip[i].Position.X, strip[i].Position.Y, 0);
            uv[i] = new Vector2(strip[i].Uv.X, strip[i].Uv.Y);
            colors[i] = new Color(strip[i].Light, strip[i].Light, strip[i].Light, 1);
        }
        var arrays = new Godot.Collections.Array(); arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.TexUV] = uv;
        arrays[(int)Mesh.ArrayType.Color] = colors;
        arrays[(int)Mesh.ArrayType.Index] = KonoeRibbonGeometry.CreateIndices(phase);
        var mesh = (ArrayMesh)_front[slot].Mesh;
        mesh.ClearSurfaces();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
    }
}