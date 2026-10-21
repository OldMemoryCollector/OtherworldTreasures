using MegaCrit.Sts2.Core.Localization;

namespace OtherworldTreasures.Scripts.Potions;

public sealed partial class SCP294Potion
{
    private static string DescriptionTargetPrefix(BrewRecipient recipient, string language) => language switch
    {
            "eng" => recipient switch { BrewRecipient.SelectedEnemy => "Target: ", BrewRecipient.AllEnemies => "All enemies: ", BrewRecipient.AllAllies => "All allies: ", BrewRecipient.BothSides => "Both sides: ", BrewRecipient.SelfAndEnemy => "You and target: ", _ => "You: " },
            "jpn" => recipient switch { BrewRecipient.SelectedEnemy => "敵：", BrewRecipient.AllEnemies => "敵全体：", BrewRecipient.AllAllies => "味方全体：", BrewRecipient.BothSides => "敵味方全体：", BrewRecipient.SelfAndEnemy => "自分と敵1体：", _ => "自分：" },
            _ => recipient switch { BrewRecipient.SelectedEnemy => "敌方目标：", BrewRecipient.AllEnemies => "敌方全体：", BrewRecipient.AllAllies => "己方全体：", BrewRecipient.BothSides => "敌我双方：", BrewRecipient.SelfAndEnemy => "自身与敌方目标：", _ => "自身：" }
    };

    private static string GroupedDescription(IEnumerable<BrewIngredient> ingredients, bool monster = false)
    {
        string language = LocManager.Instance.Language;
        var groups = new List<(string? Prefix, List<string> Effects)>();
        foreach (var ingredient in ingredients)
        {
            string text = Describe(ingredient);
            string prefix = DescriptionTargetPrefix(ingredient.Recipient, language);
            // Some effects describe their own targets (e.g. a splash affecting both sides).
            // Preserve those sentences instead of assigning them a different target label.
            if (!text.StartsWith(prefix, StringComparison.Ordinal))
            {
                groups.Add((null, [text]));
                continue;
            }

            string label = monster ? (ingredient.Recipient, language) switch
            {
                (BrewRecipient.SelectedEnemy, "eng") => "Player: ",
                (BrewRecipient.SelectedEnemy, "jpn") => "プレイヤー：",
                (BrewRecipient.SelectedEnemy, _) => "玩家：",
                (BrewRecipient.Self, "eng") => "Self: ",
                _ => prefix
            } : prefix;
            int index = groups.FindIndex(group => group.Prefix == label);
            if (index < 0)
                groups.Add((label, [text[prefix.Length..]]));
            else
                groups[index].Effects.Add(text[prefix.Length..]);
        }

        string separator = language switch { "eng" => "; ", "jpn" => "。", _ => "；" };
        string ending = language == "eng" ? "." : "。";
        return string.Join("\n", groups.Select(group => group.Prefix == null
            ? group.Effects[0]
            : group.Prefix + (group.Effects.Count == 1 ? group.Effects[0]
                : string.Join(separator, group.Effects.Select(effect => effect.TrimEnd('。', '.'))) + ending)));
    }
}
