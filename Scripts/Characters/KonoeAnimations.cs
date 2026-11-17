// Generated from original HIP frame origins by the external Konoe asset tool.
using Godot;
using OtherworldTreasures.Scripts.Konoe;
using STS2RitsuLib.Scaffolding.Visuals;
using STS2RitsuLib.Scaffolding.Visuals.Definition;

namespace OtherworldTreasures.Scripts.Characters;

internal static class KonoeAnimations
{
    internal const float Scale = 0.68f;
    internal static VisualCueSet Create()
    {
        var cues = ModVisualCues.CueSet();
        cues.Sequence("idle", sequence =>
        {
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph000_00.png", 0.09f, new VisualNodeStyle { Position = new Vector2(-109.480f, -306.680f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph000_01.png", 0.09f, new VisualNodeStyle { Position = new Vector2(-106.760f, -306.680f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph000_02.png", 0.09f, new VisualNodeStyle { Position = new Vector2(-99.960f, -306.000f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph000_03.png", 0.09f, new VisualNodeStyle { Position = new Vector2(-100.640f, -304.640f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph000_04.png", 0.09f, new VisualNodeStyle { Position = new Vector2(-104.040f, -303.280f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph000_05.png", 0.09f, new VisualNodeStyle { Position = new Vector2(-90.440f, -303.280f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph000_06.png", 0.09f, new VisualNodeStyle { Position = new Vector2(-73.440f, -302.600f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph000_07.png", 0.09f, new VisualNodeStyle { Position = new Vector2(-74.800f, -303.280f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph000_08.png", 0.09f, new VisualNodeStyle { Position = new Vector2(-87.720f, -304.640f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph000_09.png", 0.09f, new VisualNodeStyle { Position = new Vector2(-102.000f, -306.680f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Loop();
        });
        KonoeAttackVfx.AddCue(cues, "attack", KonoeAttackVfx.WindPath);
        KonoeAttackVfx.AddCue(cues, "water_attack", KonoeAttackVfx.WaterPath);
        KonoeAttackVfx.AddCue(cues, "wind_attack", KonoeAttackVfx.WindPath);
        KonoeAttackVfx.AddCue(cues, "fire_attack", KonoeAttackVfx.FirePath);
        KonoeAttackVfx.AddCue(cues, "black_stone_attack", KonoeAttackVfx.BlackStonePath);
        cues.Sequence("cast", sequence =>
        {
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph310_00.png", 0.065f, new VisualNodeStyle { Position = new Vector2(-59.840f, -308.720f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph310_01.png", 0.065f, new VisualNodeStyle { Position = new Vector2(-21.080f, -301.920f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph310_02.png", 0.065f, new VisualNodeStyle { Position = new Vector2(-12.920f, -297.840f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph310_03.png", 0.065f, new VisualNodeStyle { Position = new Vector2(-12.240f, -282.880f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph310_04.png", 0.065f, new VisualNodeStyle { Position = new Vector2(-36.720f, -269.280f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph310_05.png", 0.065f, new VisualNodeStyle { Position = new Vector2(-51.680f, -252.960f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph310_06.png", 0.065f, new VisualNodeStyle { Position = new Vector2(-64.600f, -261.120f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph310_07.png", 0.065f, new VisualNodeStyle { Position = new Vector2(-53.040f, -297.160f), Scale = Vector2.One * Scale, Centered = false });
        });
        cues.Sequence("hit", sequence =>
        {
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph050_00.png", 0.05f, new VisualNodeStyle { Position = new Vector2(-117.640f, -304.640f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph050_01.png", 0.05f, new VisualNodeStyle { Position = new Vector2(-125.800f, -286.960f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph050_02.png", 0.21f, new VisualNodeStyle { Position = new Vector2(-144.840f, -282.200f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph050_01.png", 0.07f, new VisualNodeStyle { Position = new Vector2(-125.800f, -286.960f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph050_00.png", 0.06f, new VisualNodeStyle { Position = new Vector2(-117.640f, -304.640f), Scale = Vector2.One * Scale, Centered = false });
        });
        cues.Sequence("dead", sequence =>
        {
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph063_00.png", 0.075f, new VisualNodeStyle { Position = new Vector2(-104.720f, -276.080f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph063_01.png", 0.075f, new VisualNodeStyle { Position = new Vector2(-101.320f, -249.560f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph063_02.png", 0.075f, new VisualNodeStyle { Position = new Vector2(-44.200f, -335.240f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph063_03.png", 0.075f, new VisualNodeStyle { Position = new Vector2(-44.200f, -340.680f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph063_04.png", 0.075f, new VisualNodeStyle { Position = new Vector2(-33.320f, -217.600f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph063_05.png", 0.075f, new VisualNodeStyle { Position = new Vector2(-80.240f, -172.040f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph063_06.png", 0.075f, new VisualNodeStyle { Position = new Vector2(-126.480f, -110.160f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph063_07.png", 0.075f, new VisualNodeStyle { Position = new Vector2(-103.360f, -134.640f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph063_08.png", 0.075f, new VisualNodeStyle { Position = new Vector2(-125.120f, -129.880f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph063_09.png", 0.075f, new VisualNodeStyle { Position = new Vector2(-121.720f, -114.920f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph063_10.png", 0.075f, new VisualNodeStyle { Position = new Vector2(-126.480f, -91.120f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph063_11.png", 0.075f, new VisualNodeStyle { Position = new Vector2(-125.120f, -102.000f), Scale = Vector2.One * Scale, Centered = false });
        });
        cues.Sequence("relaxed", sequence =>
        {
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph601_00.png", 0.075f, new VisualNodeStyle { Position = new Vector2(-136.000f, -263.840f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph601_01.png", 0.075f, new VisualNodeStyle { Position = new Vector2(-164.560f, -284.920f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph601_02.png", 0.075f, new VisualNodeStyle { Position = new Vector2(-176.120f, -288.320f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph601_03.png", 0.075f, new VisualNodeStyle { Position = new Vector2(-165.920f, -286.280f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph601_04.png", 0.075f, new VisualNodeStyle { Position = new Vector2(-110.160f, -267.920f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph601_05.png", 0.075f, new VisualNodeStyle { Position = new Vector2(-107.440f, -301.920f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph601_06.png", 0.075f, new VisualNodeStyle { Position = new Vector2(-115.600f, -311.440f), Scale = Vector2.One * Scale, Centered = false });
        });
        cues.Sequence("heavy", sequence =>
        {
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph203_00.png", 0.045f, new VisualNodeStyle { Position = new Vector2(-116.960f, -309.400f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph203_01.png", 0.045f, new VisualNodeStyle { Position = new Vector2(-155.720f, -303.960f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph203_02.png", 0.045f, new VisualNodeStyle { Position = new Vector2(-156.400f, -303.280f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph203_03.png", 0.045f, new VisualNodeStyle { Position = new Vector2(-124.440f, -304.640f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph203_04.png", 0.045f, new VisualNodeStyle { Position = new Vector2(-153.680f, -297.840f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph203_05.png", 0.045f, new VisualNodeStyle { Position = new Vector2(-153.680f, -310.760f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph203_06.png", 0.045f, new VisualNodeStyle { Position = new Vector2(-68.000f, -323.680f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph203_07.png", 0.045f, new VisualNodeStyle { Position = new Vector2(-78.880f, -295.800f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph203_08.png", 0.14f, new VisualNodeStyle { Position = new Vector2(-159.120f, -388.960f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph203_09.png", 0.045f, new VisualNodeStyle { Position = new Vector2(-163.200f, -382.160f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph203_10.png", 0.045f, new VisualNodeStyle { Position = new Vector2(-177.480f, -386.240f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph203_11.png", 0.045f, new VisualNodeStyle { Position = new Vector2(-113.560f, -401.880f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph203_12.png", 0.045f, new VisualNodeStyle { Position = new Vector2(-88.400f, -306.680f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph203_13.png", 0.08f, new VisualNodeStyle { Position = new Vector2(-114.240f, -306.680f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph203_14.png", 0.045f, new VisualNodeStyle { Position = new Vector2(-136.000f, -309.400f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph203_15.png", 0.045f, new VisualNodeStyle { Position = new Vector2(-110.840f, -342.040f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph203_16.png", 0.045f, new VisualNodeStyle { Position = new Vector2(-106.080f, -306.000f), Scale = Vector2.One * Scale, Centered = false });
        });
        cues.Sequence("block", sequence =>
        {
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph040_00.png", 0.045f, new VisualNodeStyle { Position = new Vector2(-78.200f, -308.040f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph040_01.png", 0.045f, new VisualNodeStyle { Position = new Vector2(-45.560f, -308.720f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph040_02.png", 0.045f, new VisualNodeStyle { Position = new Vector2(-53.720f, -303.280f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph040_03.png", 0.045f, new VisualNodeStyle { Position = new Vector2(-48.960f, -306.000f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph040_04.png", 0.045f, new VisualNodeStyle { Position = new Vector2(-51.680f, -307.360f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph040_05.png", 0.045f, new VisualNodeStyle { Position = new Vector2(-109.480f, -286.280f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph040_04.png", 0.045f, new VisualNodeStyle { Position = new Vector2(-51.680f, -307.360f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph040_03.png", 0.045f, new VisualNodeStyle { Position = new Vector2(-48.960f, -306.000f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph040_02.png", 0.045f, new VisualNodeStyle { Position = new Vector2(-53.720f, -303.280f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph040_01.png", 0.045f, new VisualNodeStyle { Position = new Vector2(-45.560f, -308.720f), Scale = Vector2.One * Scale, Centered = false });
            sequence.Frame("res://OtherworldTreasures/images/characters/nine/frames/ph040_00.png", 0.045f, new VisualNodeStyle { Position = new Vector2(-78.200f, -308.040f), Scale = Vector2.One * Scale, Centered = false });
        });
        return cues.Build();
    }
}
