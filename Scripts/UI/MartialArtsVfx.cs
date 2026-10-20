using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Settings;
using MegaCrit.Sts2.Core.TestSupport;
using OtherworldTreasures.Scripts.Powers;

namespace OtherworldTreasures.Scripts.UI;

// 七龙珠卡牌的纯表现层。所有节点都挂在战斗已有的节点树下（NCreature / CombatVfxContainer /
// NCombatRoom），在战斗结束事件和父节点释放时清理，不注册玩法内容。
// 不使用任何随机源：动画扰动全部来自时间的正弦函数，保证多人表现同步且不影响玩法状态。
internal static partial class MartialArtsVfx
{
    private static readonly Dictionary<(ulong Caster, int Bomb), SpiritBombOrb> Orbs = new();
    private static BattleScope? _scope;

    private static bool Instant => SaveManager.Instance.PrefsSave.FastMode == FastModeType.Instant;
    private static bool Fast => SaveManager.Instance.PrefsSave.FastMode != FastModeType.Normal;

    private static NCreature? CreatureNode(Creature creature) => NCombatRoom.Instance?.GetCreatureNode(creature);

    private static bool Available => !TestMode.IsOn && NCombatRoom.Instance != null && !CombatManager.Instance.IsOverOrEnding;

    private static void Track(Node node)
    {
        var room = NCombatRoom.Instance!;
        if (_scope == null || !GodotObject.IsInstanceValid(_scope) || _scope.IsQueuedForDeletion() || _scope.Room != room)
        {
            _scope = new BattleScope { Name = "OtherworldMartialVfxScope", Room = room };
            room.AddChild(_scope);
        }
        _scope.Track(node);
    }

    // Combat end may retain creature nodes or pause their processing. Explicitly clear all
    // visual-only nodes, including the caster-attached aura and pending co-op bombs.
    private sealed partial class BattleScope : Node
    {
        public NCombatRoom Room { get; init; } = null!;
        private readonly HashSet<Node> _nodes = new();
        public override void _Ready()
        {
            CombatManager.Instance.CombatWon += Clear;
            CombatManager.Instance.CombatEnded += Clear;
        }
        public void Track(Node node)
        {
            _nodes.Add(node);
            node.TreeExiting += () => _nodes.Remove(node);
        }
        private void Clear(CombatRoom _)
        {
            foreach (var node in _nodes.ToArray())
                if (GodotObject.IsInstanceValid(node)) node.QueueFree();
            _nodes.Clear();
            if (ReferenceEquals(_scope, this)) { Orbs.Clear(); _scope = null; }
            QueueFree();
        }
        public override void _ExitTree()
        {
            CombatManager.Instance.CombatWon -= Clear;
            CombatManager.Instance.CombatEnded -= Clear;
            if (ReferenceEquals(_scope, this)) { Orbs.Clear(); _scope = null; }
        }
    }

    private static readonly Dictionary<string, Texture2D> Textures = new();
    private static Texture2D Texture(string name)
    {
        if (!Textures.TryGetValue(name, out var texture))
            Textures[name] = texture = GD.Load<Texture2D>($"res://OtherworldTreasures/images/vfx/training/{name}.png");
        return texture;
    }

    // ---- 太阳拳：只保留一次白光，不附加放射线、圆环、眩光或震屏。----
    internal static async Task SolarFlare(Player caster, IReadOnlyList<Creature> enemies)
    {
        if (!Available || Instant) return;
        var flash = new ColorRect
        {
            Color = Colors.White, ZIndex = 0, MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = new Color(1, 1, 1, 0),
        };
        NCombatRoom.Instance!.CombatVfxContainer.AddChild(flash);
        Track(flash);
        flash.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var tween = flash.CreateTween();
        tween.TweenProperty(flash, "modulate:a", .85f, Fast ? .035f : .06f);
        tween.TweenProperty(flash, "modulate:a", 0f, Fast ? .12f : .2f);
        tween.Finished += flash.QueueFree;
        await Cmd.CustomScaledWait(.12f, .2f);
    }

    // ---- 魔贯光杀炮 ----
    internal static (Vector2 Start, Vector2 End) StraightBeamLine(Vector2 origin, IReadOnlyList<Rect2> enemyBounds)
    {
        if (enemyBounds.Count == 0) return (origin, origin);
        bool right = enemyBounds.Average(r => r.GetCenter().X) >= origin.X;
        float top = enemyBounds.Max(r => r.Position.Y);
        float bottom = enemyBounds.Min(r => r.End.Y);
        // A shared horizontal lane crosses each enemy's silhouette, even when their
        // individual VFX anchors are at different heights. Never create a fan of beams.
        float y = origin.Y;
        if (bottom > top)
        {
            float inset = Mathf.Min(12f, (bottom - top) / 3f);
            y = Mathf.Clamp(y, top + inset, bottom - inset);
        }
        float endX = right ? enemyBounds.Max(r => r.End.X) + 90f : enemyBounds.Min(r => r.Position.X) - 90f;
        return (new Vector2(origin.X, y), new Vector2(endX, y));
    }

    internal static async Task SpecialBeamCannon(Player caster, IReadOnlyList<Creature> enemies)
    {
        if (!Available) return;
        var node = CreatureNode(caster.Creature);
        if (node == null || enemies.Count == 0) return;
        var container = NCombatRoom.Instance!.CombatVfxContainer;
        if (!Instant)
        {
            var bounds = enemies.Where(e => e.IsAlive).Select(CreatureNode).OfType<NCreature>()
                .Select(n => n.Hitbox.GetGlobalRect()).Where(r => r.Size.X > 0 && r.Size.Y > 0).ToArray();
            if (bounds.Length == 0) return;
            var line = StraightBeamLine(node.VfxSpawnPosition, bounds);
            // 短蓄力：角色前方紫金色光点呼吸聚拢。
            var charge = new ChargeGlow { ZIndex = 0 };
            container.AddChildSafely(charge);
            charge.GlobalPosition = line.Start;
            Track(charge);
            await Cmd.CustomScaledWait(.22f, .45f);
            if (!Available || !GodotObject.IsInstanceValid(charge)) return;
            charge.EndCharge();
            var origin = charge.GlobalPosition;
            var muzzle = new MuzzleFlash { ZIndex = 0 };
            container.AddChildSafely(muzzle);
            muzzle.GlobalPosition = origin;
            Track(muzzle);
            var beam = new PiercingBeam(line.End) { ZIndex = 0 };
            container.AddChildSafely(beam);
            beam.GlobalPosition = origin;
            // 同一特效容器内以树序叠放炮口与光束，不提升全局层级。
            container.MoveChild(muzzle, container.GetChildCount() - 1);
            Track(beam);
            // One continuous piercing beam serves the existing all-enemy attack command.
            await Cmd.CustomScaledWait(.12f, .24f);
            if (Available) NGame.Instance?.ScreenShake(ShakeStrength.Medium, ShakeDuration.Short);
        }
    }

    // ---- 界王拳 ----
    internal static void EnsureKaiokenAura(Creature owner)
    {
        if (!Available || Instant) return;
        var node = CreatureNode(owner);
        if (node == null || node.GetNodeOrNull("OtherworldKaiokenAura") != null) return;
        var aura = new KaiokenAura(owner) { Name = "OtherworldKaiokenAura", ZIndex = 0 };
        node.AddChild(aura);
        node.MoveChild(aura, node.Visuals.GetIndex());
        Track(aura);
    }

    internal static void RemoveKaiokenAura(Creature owner)
    {
        var aura = CreatureNode(owner)?.GetNodeOrNull("OtherworldKaiokenAura");
        if (aura != null && GodotObject.IsInstanceValid(aura)) aura.QueueFree();
    }

    // ---- 元气弹 ----
    internal static void BombBegin(Player caster, int bombId, int energy = 0)
    {
        if (!Available || Instant) return;
        if (Orbs.TryGetValue((caster.NetId, bombId), out var existing) && GodotObject.IsInstanceValid(existing) && !existing.IsQueuedForDeletion()) return;
        var node = CreatureNode(caster.Creature);
        if (node == null) return;
        var orb = new SpiritBombOrb(caster.Creature) { Name = $"OtherworldSpiritBomb_{bombId}", ZIndex = 0 };
        node.AddChild(orb);
        orb.GlobalPosition = node.VfxSpawnPosition + new Vector2(0f, -170f);
        Orbs[(caster.NetId, bombId)] = orb;
        orb.AddEnergy(energy);
        Track(orb);
        orb.TreeExiting += () =>
        {
            if (Orbs.TryGetValue((caster.NetId, bombId), out var current) && ReferenceEquals(current, orb))
                Orbs.Remove((caster.NetId, bombId));
        };
    }

    internal static void BombContribute(ulong casterId, int bombId, Creature contributor, int energy)
    {
        if (!Available || !Orbs.TryGetValue((casterId, bombId), out var orb) || !GodotObject.IsInstanceValid(orb) || orb.IsQueuedForDeletion()) return;
        var fromNode = CreatureNode(contributor);
        var container = NCombatRoom.Instance?.CombatVfxContainer;
        if (energy > 0 && fromNode != null && container != null && !Instant)
        {
            var stream = new EnergyStream(orb) { ZIndex = 0 };
            container.AddChildSafely(stream);
            stream.GlobalPosition = fromNode.VfxSpawnPosition;
            stream.StartAtCurrentPosition();
            Track(stream);
        }
        orb.AddEnergy(energy);
    }

    internal static async Task BombRelease(Player caster, int bombId, int energy)
    {
        if (!Orbs.Remove((caster.NetId, bombId), out var orb) || !GodotObject.IsInstanceValid(orb)) return;
        if (energy <= 0)
        {
            orb.Fizzle();
            return;
        }
        var combat = caster.Creature.CombatState;
        var target = combat != null ? VfxCmd.GetSideCenter(CombatSide.Enemy, combat) : null;
        orb.Release(target);
        if (!Instant && target != null)
            await Cmd.CustomScaledWait(.18f, .35f);
    }

    internal static void BombCancel(Creature caster, int bombId)
    {
        if (Orbs.Remove((caster.Player?.NetId ?? 0, bombId), out var orb) && GodotObject.IsInstanceValid(orb)) orb.Fizzle();
    }

    internal static void BombCancelAll(Creature caster)
    {
        ulong netId = caster.Player?.NetId ?? 0;
        foreach (var key in Orbs.Keys.Where(k => k.Caster == netId).ToList())
            if (Orbs.Remove(key, out var orb) && GodotObject.IsInstanceValid(orb)) orb.Fizzle();
    }

    // 魔贯光杀炮蓄力光点。
    private sealed partial class ChargeGlow : Node2D
    {
        private double _t;
        private bool _ending;

        public override void _Process(double delta)
        {
            _t += delta;
            QueueRedraw();
            if (!_ending && _t > 2.0) QueueFree(); // 兜底：意外中断时不残留
        }

        public void EndCharge()
        {
            _ending = true;
            var tween = CreateTween();
            tween.TweenProperty(this, "modulate:a", 0f, .15f);
            tween.Finished += QueueFree;
        }

        public override void _Draw()
        {
            float pulse = .85f + .15f * Mathf.Sin((float)_t * 18f);
            float size = (18f + Mathf.Min(1f, (float)_t * 3f) * 40f) * pulse;
            DrawTextureRect(Texture("beam_muzzle"), new Rect2(-size * .5f, -size * .5f, size, size), false,
                new Color(1, 1, 1, .55f));
        }
    }

    private sealed partial class MuzzleFlash : Node2D
    {
        private double _t;
        public override void _Process(double delta)
        {
            _t += delta;
            QueueRedraw();
            if (_t >= (Fast ? .18 : .3)) QueueFree();
        }
        public override void _Draw()
        {
            float p = Mathf.Clamp((float)_t / (Fast ? .18f : .3f), 0f, 1f);
            float size = 80f + Mathf.Sin(p * Mathf.Pi * .7f) * 110f;
            DrawTextureRect(Texture("beam_muzzle"), new Rect2(-size * .5f, -size * .5f, size, size), false,
                new Color(1, 1, 1, 1f - p));
        }
    }

    // 紫金螺旋贯穿光束：从施法者指向单一敌人，命中端带扩散环。
    private sealed partial class PiercingBeam : Node2D
    {
        private readonly Vector2 _target;
        private double _t;
        private readonly float _duration;

        public PiercingBeam(Vector2 target)
        {
            _target = target;
            _duration = Fast ? .12f : .24f;
        }

        public override void _Process(double delta)
        {
            _t += delta;
            QueueRedraw();
            if (_t > _duration + .3f) QueueFree();
        }

        public override void _Draw()
        {
            float p = Mathf.Min(1f, (float)(_t / _duration));
            float fade = 1f - Mathf.Max(0f, (float)((_t - _duration) / .3f));
            Vector2 target = ToLocal(_target);
            float length = target.Length() * p;
            float height = 160f * (1f + .035f * Mathf.Sin((float)_t * 42f));
            DrawSetTransform(Vector2.Zero, target.Angle());
            var texture = Texture("special_beam");
            Vector2 source = texture.GetSize();
            float capScale = Mathf.Min(height / source.Y, length / (source.X * .26f));
            float left = source.X * .10f * capScale, right = source.X * .16f * capScale;
            var color = new Color(1, 1, 1, fade);
            // Stretch only the middle. Keep the rounded termination's original aspect
            // ratio so a long beam cannot flatten it back into a pointed arrow.
            DrawTextureRectRegion(texture, new Rect2(0, -height * .5f, left, height),
                new Rect2(0, 0, source.X * .10f, source.Y), color);
            DrawTextureRectRegion(texture, new Rect2(left, -height * .5f, Mathf.Max(0, length - left - right), height),
                new Rect2(source.X * .10f, 0, source.X * .74f, source.Y), color);
            DrawTextureRectRegion(texture, new Rect2(length - right, -height * .5f, right, height),
                new Rect2(source.X * .84f, 0, source.X * .16f, source.Y), color);
            DrawSetTransform(Vector2.Zero);
        }
    }

    // Generated transparent energy envelope, animated by shader and gentle scale.
    // Character stays visible through the texture's transparent center.
    private sealed partial class KaiokenAura : Node2D
    {
        private readonly Creature _owner;
        private double _t;
        private double _check;
        private Sprite2D? _sprite;
        private int _tier = -1;
        private float _activation;
        public KaiokenAura(Creature owner) => _owner = owner;
        public override void _Ready()
        {
            var texture = Texture("kaioken_aura");
            var used = texture.GetImage().GetUsedRect();
            _sprite = new Sprite2D { Texture = texture, Centered = false,
                Offset = new Vector2(-used.GetCenter().X, -used.End.Y), TextureFilter = TextureFilterEnum.Linear,
                Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://OtherworldTreasures/shaders/kaioken_aura_flow.gdshader") } };
            AddChild(_sprite);
            AlignToActorFeet();
        }
        private void AlignToActorFeet()
        {
            if (GetParent() is not NCreature actor) return;
            var bounds = actor.Visuals.Bounds.GetGlobalRect();
            Position = actor.GetGlobalTransform().AffineInverse() * new Vector2(bounds.GetCenter().X, bounds.End.Y);
        }
        public override void _Process(double delta)
        {
            _t += delta; _check += delta;
            var power = _owner.GetPower<KaiokenPower>();
            if (power == null || !_owner.IsAlive || (_check >= .25 && CombatManager.Instance.IsOverOrEnding))
            { QueueFree(); return; }
            if (_check >= .25) _check = 0;
            int tier = power.Amount >= 10000 ? 2 : power.Amount >= 1000 ? 1 : 0;
            if (tier != _tier) { _tier = tier; _activation = 0; }
            _activation += (float)delta;
            if (_sprite == null) return;
            float actorHeight = CreatureNode(_owner)?.Hitbox.Size.Y ?? 210f;
            float height = Mathf.Clamp(actorHeight, 130f, 330f) * (1.55f + tier * .16f) * 1.15f;
            float pulse = 1f + .025f * Mathf.Sin((float)_t * (2f + tier * .25f));
            float surge = 1f + .12f * Mathf.Exp(-_activation * 7f);
            _sprite.Scale = new Vector2(height * (2f / 3f) / _sprite.Texture.GetWidth() * pulse * surge,
                height / _sprite.Texture.GetHeight() * (1f + .025f * Mathf.Sin((float)_t * 2.6f)) * surge);
            // The opaque texture bottom stays planted at the actor feet as the aura pulses.
            _sprite.Position = Vector2.Zero;
            AlignToActorFeet();
            bool doom = _owner.GetPower<KaiokenDoomPower>() != null;
            _sprite.Modulate = doom ? new Color(.78f, .38f, .48f, .82f) : new Color(1, 1, 1, .78f + tier * .07f);
        }
    }

    // 元气弹光球：体积/亮度随供能增长；等待时脉动，释放时飞向敌阵并爆开。
    private sealed partial class SpiritBombOrb : Node2D
    {
        private readonly Creature _caster;
        private int _energy;
        private double _t;
        private bool _released;

        public SpiritBombOrb(Creature caster) => _caster = caster;

        public void AddEnergy(int energy) => _energy += Mathf.Max(energy, 0);

        public override void _Process(double delta)
        {
            _t += delta;
            QueueRedraw();
            if (!_released && (!_caster.IsAlive ||
                (_caster.CombatState != null && CombatManager.Instance.IsOverOrEnding)))
                QueueFree();
        }

        public void Release(Vector2? enemyCenter)
        {
            if (_released) return;
            _released = true;
            if (enemyCenter == null)
            {
                Fizzle();
                return;
            }
            // 脱离角色节点挂到战斗特效容器，避免飞行中跟随角色位移。
            var container = NCombatRoom.Instance?.CombatVfxContainer;
            if (container == null) { Fizzle(); return; }
            var target = container.GetGlobalTransform().AffineInverse() * enemyCenter.Value;
            var parent = GetParent();
            if (parent != null)
            {
                var global = GlobalPosition;
                parent.RemoveChild(this);
                container.AddChild(this);
                GlobalPosition = global;
            }
            var tween = CreateTween();
            tween.TweenProperty(this, "position", target, Fast ? .18f : .35f);
            tween.TweenCallback(Callable.From(Explode));
        }

        public void Fizzle()
        {
            _released = true;
            var tween = CreateTween();
            tween.TweenProperty(this, "modulate:a", 0f, .3f);
            tween.Finished += QueueFree;
        }

        private void Explode()
        {
            float radius = Radius() * 2.2f;
            var tween = CreateTween().SetParallel();
            tween.TweenProperty(this, "modulate:a", 0f, .35f);
            tween.TweenMethod(Callable.From<float>(ExplosionRing), 0f, radius, .35f);
            NGame.Instance?.ScreenShake(_energy >= 20 ? ShakeStrength.Strong : ShakeStrength.Medium, ShakeDuration.Short);
            tween.Finished += QueueFree;
        }

        private float Radius() => Mathf.Min(125f, 32f + Mathf.Sqrt(_energy) * 12f);

        private void ExplosionRing(float radius) { _explosionRadius = radius; QueueRedraw(); }
        private float _explosionRadius = -1f;

        public override void _Draw()
        {
            float pulse = 1f + .035f * Mathf.Sin((float)_t * 4.5f);
            float radius = _explosionRadius >= 0f ? Radius() + _explosionRadius : Radius() * pulse;
            // The white core and cyan corona are painted into the transparent texture.
            float size = radius * 3f;
            DrawTextureRect(Texture("spirit_bomb"), new Rect2(-size * .5f, -size * .5f, size, size), false);
        }
    }

    // 供能轨迹：从供能队友飞向元气弹光球的小光点。
    private sealed partial class EnergyStream : Node2D
    {
        private readonly SpiritBombOrb _orb;
        private double _t;
        private Vector2 _start;

        public EnergyStream(SpiritBombOrb orb) => _orb = orb;
        public void StartAtCurrentPosition() => _start = Position;

        public override void _Process(double delta)
        {
            _t += delta;
            if (!GodotObject.IsInstanceValid(_orb)) { QueueFree(); return; }
            float duration = Fast ? .18f : .35f;
            float p = (float)Mathf.Min(1.0, _t / duration);
            if (_orb.IsQueuedForDeletion()) { QueueFree(); return; }
            // Both endpoints belong to the stream parent's space, never to the moving
            // stream itself. This remains correct under parent translation and scaling.
            var parent = (CanvasItem)GetParent();
            Vector2 target = parent.GetGlobalTransformWithCanvas().AffineInverse() * _orb.GetGlobalTransformWithCanvas().Origin;
            Position = _start.Lerp(target, p) + new Vector2(0, -Mathf.Sin(p * Mathf.Pi) * 45f);
            QueueRedraw();
            if (p >= 1f)
            {
                var tween = CreateTween();
                tween.TweenProperty(this, "modulate:a", 0f, .12f);
                tween.Finished += QueueFree;
                SetProcess(false);
            }
        }

        public override void _Draw()
        {
            const float size = 24f;
            DrawTextureRect(Texture("spirit_bomb"), new Rect2(-size * .5f, -size * .5f, size, size), false);
        }
    }
}
