using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;

namespace OtherworldTreasures.Scripts.Events;

// === SCP-914 品质配色（2026-10-17 玩家确认） ===
// 原版 NInspectRelicScreen.SetRarityVisuals 里"罕见"和"商店"的标签色都是 StsColors.blue，
// 只有边框 HSV 不同，直接照抄会让两个档看起来一样。这里给商店单独一支青色，保证一眼可分。
internal static class Scp914Colors
{
    internal static readonly Color Common = new("#FFF6E2");
    internal static readonly Color Uncommon = new("#87CEEB");
    internal static readonly Color Rare = new("#EFC851");
    internal static readonly Color Shop = new("#4FD9C4");
    internal static readonly Color Event = new("#7FFF00");
    internal static readonly Color Ancient = new("#FF5555");
    internal static readonly Color Curse = new("#B48CFF");

    internal static Color Of(CardRarity rarity) => rarity switch
    {
        CardRarity.Uncommon => Uncommon,
        CardRarity.Rare => Rare,
        CardRarity.Ancient => Ancient,
        CardRarity.Curse or CardRarity.Status or CardRarity.Quest => Curse,
        _ => Common
    };

    internal static Color Of(RelicRarity rarity) => rarity switch
    {
        RelicRarity.Uncommon => Uncommon,
        RelicRarity.Rare => Rare,
        RelicRarity.Shop => Shop,
        RelicRarity.Event => Event,
        RelicRarity.Ancient => Ancient,
        _ => Common
    };

    /// <summary>按品质给文本上色（RichTextLabel 的 BBCode）。连【】一起染，避免括号保持无色。</summary>
    internal static string Tag(Color color, string text) =>
        $"[color=#{color.ToHtml(false)}]【{text}】[/color]";
}
