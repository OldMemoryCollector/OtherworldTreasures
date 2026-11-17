using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace OtherworldTreasures.Scripts.Konoe;

internal static class KonoeSpellTips
{
    private static IHoverTip Tip(string key) => new HoverTip(new LocString("static_hover_tips", key + ".title"), new LocString("static_hover_tips", key + ".description"));
    internal static IEnumerable<IHoverTip> Elements(KonoeDamageType types)
    {
        if (types == KonoeDamageType.None) yield break;
        yield return Tip("KONOE_ELEMENT_ATTACK");
        if ((types & KonoeDamageType.Water) != 0) { yield return Tip("KONOE_WATER_ATTRIBUTE"); yield return HoverTipFactory.FromPower<KonoeWetPower>(); }
        if ((types & KonoeDamageType.Wind) != 0) yield return Tip("KONOE_WIND_ATTRIBUTE");
        if ((types & KonoeDamageType.Fire) != 0) { yield return Tip("KONOE_FIRE_ATTRIBUTE"); yield return HoverTipFactory.FromPower<KonoeBurnPower>(); }
    }
    internal static IEnumerable<IHoverTip> For(KonoeSpellKind spell, int stacks = 0)
    {
        foreach (var tip in Elements(KonoeDamage.ForSpell(spell))) yield return tip;
        if (spell == KonoeSpellKind.BlackStone) yield return HoverTipFactory.FromKeyword(MyKeywords.Linkage);
        if (spell is KonoeSpellKind.Lapis or KonoeSpellKind.Sapphire or KonoeSpellKind.Tanzanite) yield return HoverTipFactory.FromPower<KonoeFrozenPower>();
        if (spell is KonoeSpellKind.Emerald or KonoeSpellKind.Peridot or KonoeSpellKind.Malachite or KonoeSpellKind.Amethyst or KonoeSpellKind.Azurite or KonoeSpellKind.Kunzite) yield return HoverTipFactory.FromPower<KonoeKnockdownPower>();
        if (spell is KonoeSpellKind.Coral or KonoeSpellKind.Garnet or KonoeSpellKind.Rubellite or KonoeSpellKind.Azurite or KonoeSpellKind.Kunzite) { yield return HoverTipFactory.FromPower<KonoeAirbornePower>(); yield return HoverTipFactory.FromKeyword(MyKeywords.Linkage); }
        // These two powers describe themselves with {Amount}; without the real value the tip reads "0".
        if (spell == KonoeSpellKind.Chrysoberyl) yield return HoverTipFactory.FromPower<KonoeAttackGuardPower>(stacks > 0 ? stacks : null);
        if (spell == KonoeSpellKind.Citrine) yield return HoverTipFactory.FromPower<KonoeCounterPower>(stacks > 0 ? stacks : null);
        if (spell == KonoeSpellKind.Azurite) yield return HoverTipFactory.FromPower<KonoeGravityFieldPower>();
    }
}
