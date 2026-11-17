using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Saves.Runs;
using OtherworldTreasures.Scripts.Poker;
using OtherworldTreasures.Scripts.Relics;
using OtherworldTreasures.Scripts.UI;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Cards;

[RegisterCard(typeof(TokenCardPool))]
public sealed class FlyingCards : ModCardTemplate
{
    [SavedProperty] public int ReturnOnTurn { get; set; }
    public FlyingCards():base(1,CardType.Attack,CardRarity.Token,TargetType.AnyEnemy, DevelopmentContent.Extras){}
    public override bool CanBeGeneratedInCombat=>false;
    public override bool CanBeGeneratedByModifiers=>false;
    public override int MaxUpgradeLevel=>0;
    public override IEnumerable<CardKeyword> CanonicalKeywords=>[CardKeyword.Retain];
    public override CardAssetProfile AssetProfile=>new(PortraitPath:DeathPoker.PileImage);
    protected override CardLocation GetResultLocationForCardPlay()=>IsDupe?base.GetResultLocationForCardPlay():new(Owner,Entry.PokerFlightPile,CardPilePosition.Bottom);
    protected override async Task OnPlay(PlayerChoiceContext context,CardPlay play)
    {
        var relic=Owner.Relics.OfType<DeathPoker>().FirstOrDefault();
        if(relic!=null && relic.Cards.Count>0) {
            var snapshot=relic.Cards.ToArray();
            var selection=await PokerChoice.Choose(context,Owner,snapshot);
            if(selection.AllIndexes.Length>0){relic.Consume(selection.AllIndexes);await PokerEffects.Resolve(context,play,snapshot,selection);}
        }
        ReturnOnTurn=(Owner.PlayerCombatState?.TurnNumber??0)+1;
    }
    // Same native lifecycle as Thrumming Hatchet; the hidden pile keeps it out of shuffle/recovery.
    public override async Task BeforeHandDraw(Player player,PlayerChoiceContext context,ICombatState combatState)
    {
        if(player==Owner && ReturnOnTurn>0 && player.PlayerCombatState?.TurnNumber>=ReturnOnTurn && Pile?.Type==Entry.PokerFlightPile) {
            await CardPileCmd.Add(this,PileType.Hand);
            if(Pile?.Type==PileType.Hand)ReturnOnTurn=0;
        }
    }
}
