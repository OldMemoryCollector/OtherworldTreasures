using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace OtherworldTreasures.Scripts.Konoe;

public static class KonoeElementDamageText
{
    // Work on the final formatted text, retaining native numeric preview colours,
    // dynamic spell text, conditional clauses and every separate damage line.
    private static readonly Regex DamageClause = new(@"(?<lead>造成[^。！？\r\n]*?点)(?<qualifier>[^。！？\r\n]*?)伤害", RegexOptions.CultureInvariant);
    // 卡面允许直接写死「风元素」这类字样，改写前先剥掉，避免出现「风元素水元素」重复。
    private static readonly Regex ManualElementWord = new(@"\[color=[^\]]+\](?:水|风|火)元素\[/color\]", RegexOptions.CultureInvariant);
    private static readonly Regex BareElementWord = new(@"(?:水|风|火)(?:元素|属性)", RegexOptions.CultureInvariant);
    private static string Strip(string qualifier) => BareElementWord.Replace(ManualElementWord.Replace(qualifier, ""), "");
    public const string WaterColor = "#69BFFF";
    public const string WindColor = "#75F53A";
    public const string FireColor = "#FF7966";
    private static string RewriteCore(string text, string attributes)
        => DamageClause.Replace(text, match => match.Groups["lead"].Value + Strip(match.Groups["qualifier"].Value) + attributes + "伤害");
    public static string Rewrite(string text, string elements) => RewriteCore(text, elements + "元素");
    public static string ColorAttributes(string elements)
    {
        var names = elements.Split('／');
        string Color(string name, string label) => name switch
        {
            "水" => "[color=" + WaterColor + "]" + label + "[/color]",
            "风" => "[color=" + WindColor + "]" + label + "[/color]",
            "火" => "[color=" + FireColor + "]" + label + "[/color]",
            _ => label
        };
        return names.Length == 1 ? Color(names[0], names[0] + "元素")
            : string.Join("／", names.Select(name => Color(name, name))) + "元素";
    }
    public static string RewriteColored(string text, string elements) => RewriteCore(text, ColorAttributes(elements));
    public static string AttributeNames(KonoeDamageType types)
    {
        var names = new List<string>(3);
        if ((types & KonoeDamageType.Water) != 0) names.Add(new LocString("cards", "KONOE_ELEMENT_WATER").GetFormattedText());
        if ((types & KonoeDamageType.Wind) != 0) names.Add(new LocString("cards", "KONOE_ELEMENT_WIND").GetFormattedText());
        if ((types & KonoeDamageType.Fire) != 0) names.Add(new LocString("cards", "KONOE_ELEMENT_FIRE").GetFormattedText());
        return string.Join("／", names);
    }
    public static string ForCard(CardModel card, string text)
    {
        var types = KonoeDamage.ForCard(card);
        if (types == KonoeDamageType.None) return text;
        var elements = AttributeNames(types);
        var rewritten = RewriteColored(text, elements);
        // Dynamic spells without direct damage must not pretend to have a damage
        // amount. Expose only the attached damage attribute in that case.
        if (rewritten == text && KonoeDamage.AttachedTypes(card) != KonoeDamageType.None && !DamageClause.IsMatch(text))
        {
            var fallback = new LocString("cards", "KONOE_ELEMENT_DAMAGE_FALLBACK");
            fallback.Add("Attributes", ColorAttributes(elements));
            return text + "\n" + fallback.GetFormattedText();
        }
        return rewritten;
    }
    public static ValueProp DamageProps(ValueProp props, CardModel? source)
    {
        if (!props.IsCardOrMonsterMove() || KonoeDamage.FromProps(props) != KonoeDamageType.None) return props;
        var types = KonoeDamage.ForCard(source);
        return types == KonoeDamageType.None ? props : KonoeDamage.WithTypes(props, types);
    }
}

// Ritsu's description contributor supports BeforeBase/AfterBase, not replacement.
// Patch the shared final renderer once, covering hand, library and upgrade previews.
[HarmonyPatch]
internal static class KonoeElementDamageDescriptionPatch
{
    private static MethodBase TargetMethod() => AccessTools.GetDeclaredMethods(typeof(CardModel))
        .Single(method => method.Name == "GetDescriptionForPile" && method.GetParameters().Length == 3);
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(CardModel __instance, ref string __result)
        => __result = KonoeElementDamageText.ForCard(__instance, __result);
}

// Also cover attack cards using the native direct damage API instead of an
// AttackCommand. Explicit per-spell/per-hit snapshots always take precedence.
[HarmonyPatch]
internal static class KonoeElementDirectDamagePatch
{
    private static MethodBase TargetMethod() =>
        OtherworldTreasures.Scripts.Compatibility.NativeDamagePipeline.Method;
    private static void Prefix(ref ValueProp props, CardModel? cardSource)
        => props = KonoeElementDamageText.DamageProps(props, cardSource);
}

[HarmonyPatch(typeof(Hook), nameof(Hook.ModifyDamage))]
internal static class KonoeElementDamagePreviewPatch
{
    private static void Prefix(ref ValueProp props, CardModel? cardSource, Creature? target, Creature? dealer, CardPlay? cardPlay, ref decimal damage)
    {
        props = KonoeElementDamageText.DamageProps(props, cardSource);
        if ((KonoeDamage.FromProps(props) & KonoeDamageType.Wind) != 0)
            damage = KonoeElementEffects.WindBase(damage, target, dealer, cardSource, cardPlay);
    }
}
