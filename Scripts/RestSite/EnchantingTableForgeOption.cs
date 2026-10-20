using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using OtherworldTreasures.Scripts.Relics;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.RestSite;

public sealed class EnchantingTableForgeOption : ModRestSiteOptionTemplate
{
    public override string OptionId => "ENCHANTING_TABLE";
    public override RestSiteOptionAssetProfile AssetProfile { get; } = new("res://OtherworldTreasures/images/ui/rest_site/option_enchanting_table.png");
    public override LocString? CustomTitle => new("rest_site_ui", "OPTION_ENCHANTING_TABLE.name");
    private readonly EnchantingTable _table;
    public EnchantingTableForgeOption(Player owner, EnchantingTable table) : base(owner) => _table = table;
    public override async Task<bool> OnSelect()
    {
        var legal = Owner.Deck.Cards.Where(_table.HasAnyLegalEnchantment).ToList();
        if (legal.Count == 0 || _table.Level < EnchantingTable.PermanentCost) return false;
        var selected = (await CardSelectCmd.FromDeckGeneric(Owner,
            new CardSelectorPrefs(CardSelectorPrefs.EnchantSelectionPrompt, 1) { Cancelable = true },
            _table.HasAnyLegalEnchantment)).FirstOrDefault();
        if (selected == null) return false;
        if (!_table.EnchantForLevels(selected, EnchantingTable.PermanentCost)) return false;
        CardCmd.Upgrade(selected);
        // Reuse the game's card preview to show the actual enchanted card.
        CardCmd.Preview(selected, 1.5f);
        _table.RefreshUi();
        _table.PreserveRestSiteChoices();
        return true;
    }
}
