using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Random;

namespace OtherworldTreasures.Scripts.Potions;

public sealed partial class SCP294Potion
{
    // Recipe names are shared game data. Keep the old Chinese naming draws on the
    // game RNG, then translate presentation without touching that RNG again.
    private const string RandomBrewNameKey = "random";
    private static string CanonicalNameText(string key) => key switch
    {
        "defaultName" => "异常药水",
        "special.perfect" => "完美药水",
        "special.weakening" => "衰弱药水",
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown generated brew name.")
    };

    internal static string DisplayName(BrewRecipe recipe)
    {
        // Old recipes and manually named drinks have no key. Their saved name is
        // literal content and must survive language changes and resaving intact.
        if (recipe.NameKey is "defaultName" or "special.perfect" or "special.weakening")
            return LocText(recipe.NameKey);
        if (recipe.NameKey != RandomBrewNameKey || LocManager.Instance.Language is not ("eng" or "jpn"))
            return recipe.Name;

        int seed = recipe.VisualSeed ?? BitConverter.ToInt32(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(recipe))), 0);
        var presentationRng = new Rng(unchecked((ulong)(uint)seed), "scp294-presentation-name");
        string[] words = recipe.Ingredients.Select(item => EffectName(item.Effect)).ToArray();
        for (int i = words.Length - 1; i > 0; --i)
        {
            int j = presentationRng.NextInt(i + 1);
            (words[i], words[j]) = (words[j], words[i]);
        }
        string stem = string.Concat(words.Where(word => word.Length > 0).Select(word => word[..1])).ToUpperInvariant();
        return stem + (LocManager.Instance.Language == "jpn" ? "調合薬" : " Brew");
    }
}
