using System.Numerics;

namespace OtherworldTreasures.Scripts.Konoe;

public readonly record struct KonoeRibbonVertex(Vector2 Position, Vector2 Uv, Vector3 SurfacePosition, float Light);

// One complete, unchanged texture is wrapped around a stationary cylindrical band.
// Its transparent tail leaves an open arc. Only acquisition changes the phase.
public static class KonoeRibbonGeometry
{
    public const int SourceWidth = 512;
    public const int SourceHeight = 32;
    public const int Segments = 256;
    public const float ArrivalDuration = .24f;
    private const float ArrivalAngle = MathF.PI * 1.10f;
    private const float Radius = SourceWidth / MathF.Tau;
    private const float StartAngle = 3.55f;
    private const float DepthProjection = .28f;
    private const float Tilt = .30f;
    private static readonly Vector2 Center = new(-12, -237);
    private static readonly Vector2 LayerOffsetPixels = new(-26, 46);
    public static float CurveLength => SourceWidth;

    public static float ArrivalPhase(float elapsed)
    {
        float remaining = 1 - Math.Clamp(elapsed / ArrivalDuration, 0, 1);
        return ArrivalAngle * remaining * remaining * remaining;
    }

    public static KonoeRibbonVertex[] Build(float scale, int slot, float phase = 0)
    {
        if (scale <= 0) throw new ArgumentOutOfRangeException(nameof(scale));
        if (slot is < 0 or > 2) throw new ArgumentOutOfRangeException(nameof(slot));
        var vertices = new KonoeRibbonVertex[(Segments + 1) * 2];
        var anchor = Center + LayerOffsetPixels * (slot * scale);
        for (int i = 0; i <= Segments; i++)
        {
            float u = (float)i / Segments;
            float angle = StartAngle - u * MathF.Tau + phase;
            var point = new Vector3(Radius * MathF.Cos(angle), 0, Radius * MathF.Sin(angle));
            var a = (point - Vector3.UnitY * (SourceHeight * .5f)) * scale;
            var b = (point + Vector3.UnitY * (SourceHeight * .5f)) * scale;
            vertices[i * 2] = new(Project(a) + anchor, new(u, 0), a, 1);
            vertices[i * 2 + 1] = new(Project(b) + anchor, new(u, 1), b, 1);
        }
        return vertices;
    }

    public static int[] CreateIndices(float phase = 0)
    {
        var indices = new int[Segments * 6];
        int output = 0;
        // Rear surface first, front surface last, including while arriving.
        foreach (int i in Enumerable.Range(0, Segments).OrderBy(i =>
            MathF.Sin(StartAngle - (i + .5f) / Segments * MathF.Tau + phase)))
        {
            int a = i * 2;
            indices[output++] = a; indices[output++] = a + 1; indices[output++] = a + 2;
            indices[output++] = a + 1; indices[output++] = a + 3; indices[output++] = a + 2;
        }
        return indices;
    }

    public static Vector2 PointAtPixel(float sourceX, float scale, int slot, float phase = 0)
    {
        float angle = StartAngle - sourceX / SourceWidth * MathF.Tau + phase;
        var point = new Vector3(Radius * MathF.Cos(angle), 0, Radius * MathF.Sin(angle)) * scale;
        return Project(point) + Center + LayerOffsetPixels * (slot * scale);
    }

    private static Vector2 Project(Vector3 point)
    {
        float y = point.Y + point.Z * DepthProjection;
        float c = MathF.Cos(Tilt), s = MathF.Sin(Tilt);
        return new(point.X * c - y * s, point.X * s + y * c);
    }
}