using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;

namespace OtherworldTreasures.Scripts.Enchantments;

internal static class EnchantingTableCompatibility
{
    // Native CanEnchant does not express these OnPlay/AfterCardPlayed requirements.
    internal static bool SupportsContext(EnchantmentModel enchantment, CardModel card) => enchantment switch
    {
        Inky => card.TargetType is TargetType.AnyEnemy or TargetType.AllEnemies,
        Goopy => card.DeckVersion == null || card.DeckVersion.Enchantment is Goopy,
        _ => true
    };

    internal static bool CanApply(EnchantmentModel enchantment, CardModel card) =>
        SupportsContext(enchantment, card) && enchantment.CanEnchant(card);

    internal static bool Repair(CardModel card)
    {
        if (card.Enchantment is not { } enchantment || SupportsContext(enchantment, card)) return false;
        string name = enchantment.Id.Entry;
        bool removeExhaust = enchantment is Goopy && card.DeckVersion is { } original
            && !original.GetKeywordsWithSources(KeywordSources.Local).Contains(CardKeyword.Exhaust);
        CardCmd.ClearEnchantment(card);
        if (removeExhaust) card.RemoveKeyword(CardKeyword.Exhaust);
        Entry.Logger.Warn($"[EnchantingTable] Removed incompatible {name} from {card.Id.Entry}; target={card.TargetType}.");
        return true;
    }
}
