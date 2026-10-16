using Godot;
using System.Globalization;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using OtherworldTreasures.Scripts.Powers;
using OtherworldTreasures.Scripts.Relics;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace OtherworldTreasures.Scripts.UI;

public partial class ScouterReadout : Node2D
{
    private double _elapsed;
    private string _text = "";
    private static Texture2D? _hudTexture;
    private const float HudWidth = 320;
    private const float HudHeight = HudWidth * 887f / 1774f;
    // 相对角色中心的额外偏移：HUD 整体略微向右上移动。
    private static readonly Vector2 ReadoutOffset = new(30f, -36f);
    // Coordinates within Luna's transparent 1774x887 HUD: ring center and the
    // empty area immediately above the underline reserved for live digits.
    private static readonly Vector2 RingAnchor = new(.253f, .607f);
    // Scaled to two-thirds of the prior 52px display height. On the source HUD,
    // the underline glow begins near y=277/887; this placement keeps the full
    // dark outline above it while leaving a visible gap.
    private static readonly Rect2 NumberArea = new(.41f + 12f / HudWidth, .15f - 20f / HudHeight, .47f, (52f * 2f / 3f) / HudHeight);
    private static readonly byte[] DigitSegments = [0x3f, 0x06, 0x5b, 0x4f, 0x66, 0x6d, 0x7d, 0x07, 0x7f, 0x6f];
    private static readonly Color NumberColor = new("f4ff53");

    public override void _Ready()
    {
        Visible = false;
        ZIndex = 8;
        CombatManager.Instance.CombatWon += OnCombatEnded;
        CombatManager.Instance.CombatEnded += OnCombatEnded;
    }

    public override void _ExitTree()
    {
        CombatManager.Instance.CombatWon -= OnCombatEnded;
        CombatManager.Instance.CombatEnded -= OnCombatEnded;
    }

    private void OnCombatEnded(CombatRoom _)
    {
        // Also hide dormant/reused visuals whose parent no longer processes.
        Visible = false;
        _text = "";
        _elapsed = 0;
        QueueRedraw();
    }

    public static bool ShouldDisplay(Creature? creature)
    {
        // Creature.CombatState can survive the battle while its visual is reused
        // on the map. It is not evidence that a combat is still taking place.
        if (CombatManager.Instance.IsOverOrEnding || creature?.IsAlive != true ||
            creature.CombatState is not { } combat) return false;
        var viewer = combat.Players.FirstOrDefault(p => LocalContext.IsMe(p));
        return viewer?.Relics.OfType<CombatScouter>().Any(r => r.DisplayEnabled) == true;
    }
    public override void _Process(double delta)
    {
        var node = GetParent() as NCreature;
        var creature = node?.Entity;
        bool visible = ShouldDisplay(creature);
        Visible = visible;
        if (!visible || creature == null) return;
        _hudTexture ??= GD.Load<Texture2D>("res://OtherworldTreasures/images/ui/scouter_targeting_hud.png");
        if (node?.Visuals?.Bounds is { } bounds)
            Position = node.GetGlobalTransform().AffineInverse() * bounds.GetGlobalRect().GetCenter() + ReadoutOffset;
        _elapsed += delta;
        if (_elapsed < .2 && _text.Length > 0) return;
        _elapsed = 0;
        string text = decimal.Truncate(Estimate(creature)).ToString("0", CultureInfo.InvariantCulture);
        if (_text != text) { _text = text; QueueRedraw(); }
    }
    public static decimal Estimate(Creature creature)
    {
        decimal score = creature.CurrentHp + creature.MaxHp * .25m + creature.Block * 2m;
        score += Math.Max(0, creature.GetPower<StrengthPower>()?.Amount ?? 0) * 8m;
        score += Math.Max(0, creature.GetPower<DexterityPower>()?.Amount ?? 0) * 6m;
        score += Math.Max(0, creature.GetPower<IntangiblePower>()?.Amount ?? 0) * 50m;
        score += Math.Max(0, creature.GetPower<BufferPower>()?.Amount ?? 0) * 40m;
        if (creature.Monster?.NextMove is { } move)
            foreach (var intent in move.Intents.OfType<AttackIntent>())
                score += Math.Max(0, intent.DamageCalc?.Invoke() ?? 0) * Math.Max(1, intent.Repeats) * 4;
        return Math.Max(0, score * (1 + (creature.GetPower<KaiokenPower>()?.Amount ?? 0) / 100m));
    }
    public override void _Draw()
    {
        if (_hudTexture == null || _text.Length == 0) return;
        var size = new Vector2(HudWidth, HudWidth * _hudTexture.GetHeight() / _hudTexture.GetWidth());
        var origin = -RingAnchor * size;
        DrawTextureRect(_hudTexture, new Rect2(origin, size), false);
        DrawDigits(new Rect2(origin + NumberArea.Position * size, NumberArea.Size * size));
    }

    private void DrawDigits(Rect2 area)
    {
        float height = CalculateDigitHeight(area.Size.X, area.Size.Y, _text.Length);
        float width = height * .55f;
        float step = height * .70f;
        float stroke = Math.Max(1.4f, height * .105f);
        var start = area.Position + new Vector2(0, area.Size.Y - height);
        // Seven segment order: top, upper right, lower right, bottom, lower
        // left, upper left, center. Runtime digits remain independent of the art.
        Vector2[] points = [new(.12f, 0), new(.88f, 0), new(1, .08f), new(1, .44f),
            new(1, .56f), new(1, .92f), new(.12f, 1), new(.88f, 1),
            new(0, .56f), new(0, .92f), new(0, .08f), new(0, .44f), new(.12f, .5f), new(.88f, .5f)];
        for (int digit = 0; digit < _text.Length; digit++)
        {
            byte segments = DigitSegments[_text[digit] - '0'];
            var offset = start + new Vector2(digit * step, 0);
            for (int segment = 0; segment < 7; segment++)
            {
                if ((segments & (1 << segment)) == 0) continue;
                var from = offset + points[segment * 2] * new Vector2(width, height);
                var to = offset + points[segment * 2 + 1] * new Vector2(width, height);
                DrawLine(from, to, new Color(0.08f, .13f, .02f, .8f), stroke + 2, true);
                DrawLine(from, to, NumberColor, stroke, true);
            }
        }
    }

    private static float CalculateDigitHeight(float areaWidth, float areaHeight, int digitCount)
    {
        float digitRunWidth = .55f + (digitCount - 1) * .70f;
        return Math.Min(areaHeight, areaWidth / digitRunWidth);
    }
}
