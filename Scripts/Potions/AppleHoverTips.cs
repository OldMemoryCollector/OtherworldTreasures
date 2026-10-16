using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models.Powers;

namespace OtherworldTreasures.Scripts.Potions;

internal static class AppleHoverTips
{
    internal static IEnumerable<IHoverTip> For(bool enchanted)
    {
        yield return Tip("TEMPORARY_HEALTH");
        yield return HoverTipFactory.FromPower<RegenPower>();
        if (!enchanted) yield break;
        yield return Tip("APPLE_RESISTANCE");
        yield return Tip("APPLE_FIRE_RESISTANCE");
    }
    private static IHoverTip Tip(string key) => new HoverTip(
        new LocString("static_hover_tips", $"OTHERWORLD_TREASURES_{key}.title"),
        new LocString("static_hover_tips", $"OTHERWORLD_TREASURES_{key}.description"));
}
