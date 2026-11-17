using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Runs;
using OtherworldTreasures.Scripts.Powers;
using OtherworldTreasures.Scripts.Spider;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models.Capabilities;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Relics;

[RegisterRelic(typeof(SharedRelicPool))]
public sealed class SpiderMutation : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Event;
    #if STS2_113
    protected override bool IsAllowedInternal(IRunState runState)
    #else
    public override bool IsAllowed(IRunState runState)
    #endif
        => DevelopmentContent.Spider;
    public override bool IsAllowedInShops => DevelopmentContent.Spider;
    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://images/powers/missing_power.png", IconOutlinePath: "res://images/powers/missing_power.png", BigIconPath: "res://images/powers/missing_power.png");
    public override async Task BeforeCombatStartLate()
    {
        var context = new ThrowingPlayerChoiceContext();
        await PowerCmd.Apply<StrengthPower>(context, Owner.Creature, 2, Owner.Creature, null);
        await PowerCmd.Apply<DexterityPower>(context, Owner.Creature, 2, Owner.Creature, null);
    }
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext context, Player player)
    {
        if (player != Owner || Owner.Creature.CombatState is not { } combat) return;
        int attackers = combat.Enemies.Count(enemy => enemy.IsAlive && enemy.Monster?.IntendsToAttack == true);
        int amount = SpiderRules.SenseStacks(attackers);
        if (amount > 0) { Flash(); await PowerCmd.Apply<DodgePower>(context, Owner.Creature, amount, Owner.Creature, null); }
    }
    internal static void MutateBasics(Player player)
    {
        foreach (var card in player.Deck.Cards.Where(card => card.Rarity == CardRarity.Basic))
        {
            // Preserve the original type, upgrade level, enchantments and Strike/Defend tags.
            // Registered capabilities persist on the deck and clone into its combat cards.
            if (card.Tags.Contains(CardTag.Strike)) card.GetOrCreateCapability<SpiderStrikeCapability>();
            if (card.Tags.Contains(CardTag.Defend)) card.GetOrCreateCapability<SpiderDefendCapability>();
        }
    }
}
