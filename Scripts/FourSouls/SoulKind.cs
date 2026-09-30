using Godot;
using MegaCrit.Sts2.Core.Localization;

namespace OtherworldTreasures.Scripts.FourSouls;

/// <summary>
/// 四魂之玉的四种碎片。
/// 荒魂=红、和魂=蓝、幸魂=绿、奇魂=紫。
/// </summary>
public enum SoulKind
{
    Wasteland = 0, // 荒魂：力量
    Harmony = 1,   // 和魂：覆甲
    Fortune = 2,   // 幸魂：再生
    Wonder = 3,    // 奇魂：生命上限
}

/// <summary>碎片颜色与名称。</summary>
public static class Souls
{
    public const int Count = 4;

    // 荒红 / 和蓝 / 幸绿 / 奇紫
    private static readonly Color[] Palette =
    {
        new(0.93f, 0.25f, 0.20f),
        new(0.26f, 0.56f, 1.00f),
        new(0.32f, 0.86f, 0.36f),
        new(0.68f, 0.36f, 0.96f),
    };

    public static Color ColorOf(SoulKind kind) => Palette[(int)kind];

    public static string NameKey(SoulKind kind)
        => "OTHERWORLD_TREASURES_SOUL_" + kind.ToString().ToUpperInvariant();

    public static string Name(SoulKind kind)
        => new LocString("gameplay_ui", NameKey(kind)).GetFormattedText();

    public static readonly SoulKind[] All =
    {
        SoulKind.Wasteland, SoulKind.Harmony, SoulKind.Fortune, SoulKind.Wonder,
    };
}
