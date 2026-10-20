using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Enchantments;

/// <summary>Uses the mod-owned asset tree for every custom enchantment icon.</summary>
public abstract class OtherworldEnchantmentBase : ModEnchantmentTemplate
{
    public override EnchantmentAssetProfile AssetProfile => new(
        $"res://OtherworldTreasures/images/enchantments/{Id.Entry.ToLowerInvariant()}.png");
}
