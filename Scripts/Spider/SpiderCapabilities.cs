using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using OtherworldTreasures.Scripts.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models.Capabilities;

namespace OtherworldTreasures.Scripts.Spider;

[RegisterModelCapability]
public sealed class SpiderStrikeCapability : CardCapability, ICardDescriptionContributor
{
    public IEnumerable<CardDescriptionFragment> GetDescriptionFragments(CardDescriptionContext context) =>
        [new(new LocString("cards", "OTHERWORLD_TREASURES_SPIDER_STRIKE.extra"))];
    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
    {
        if (play.Card == Owner && play.Target is { IsAlive: true } target)
            await PowerCmd.Apply<WebbedPower>(context, target, 2, Owner.Owner.Creature, Owner);
    }
}

[RegisterModelCapability]
public sealed class SpiderDefendCapability : CardCapability, ICardDescriptionContributor
{
    public IEnumerable<CardDescriptionFragment> GetDescriptionFragments(CardDescriptionContext context) =>
        [new(new LocString("cards", "OTHERWORLD_TREASURES_SPIDER_DEFEND.extra"))];
    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
    {
        if (play.Card == Owner) await PowerCmd.Apply<DodgePower>(context, Owner.Owner.Creature, 2, Owner.Owner.Creature, Owner);
    }
}

[RegisterModelCapability]
public sealed class SpiderEtherealMarkCapability : UntilCombatEndCapability<CardModel>, ICardDescriptionContributor
{
    public IEnumerable<CardDescriptionFragment> GetDescriptionFragments(CardDescriptionContext context) =>
        [new(new LocString("cards", "OTHERWORLD_TREASURES_SPIDER_ETHEREAL_MARK.extra"))];
    public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
    {
        if (play.Card == Owner) RemoveFromOwner();
        return Task.CompletedTask;
    }
}

[RegisterModelCapability]
public sealed class SpiderEchoCopyCapability : UntilCombatEndCapability<CardModel>, ICardPlayResultContributor, ICardEnergyCostContributor, ICardStarCostContributor
{
    public PileType? GetResultPileTypeForCardPlay(CardModel card) => card == Owner ? PileType.None : null;
    public int ModifyEnergyCost(CardModel card, int currentCost, CostModifiers modifiers) => 0;
    public int ModifyStarCost(CardModel card, int currentCost) => 0;
}
