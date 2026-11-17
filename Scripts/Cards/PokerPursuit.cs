using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.CardPools;
using OtherworldTreasures.Scripts.Poker;
using OtherworldTreasures.Scripts.Relics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
namespace OtherworldTreasures.Scripts.Cards;

[RegisterCard(typeof(TokenCardPool))]
public sealed class PokerPursuit:ModCardTemplate
{
    public PokerPursuit():base(2,CardType.Attack,CardRarity.Token,TargetType.AllEnemies, DevelopmentContent.Extras){}
    public override bool CanBeGeneratedInCombat=>false;
    public override bool CanBeGeneratedByModifiers=>false;
    public override int MaxUpgradeLevel=>0;
    public override CardAssetProfile AssetProfile=>new(PortraitPath:DeathPoker.PileImage);
    protected override bool IsPlayable=> Owner.Relics.OfType<DeathPoker>().Any(r=>r.Cards.Count>=30);
    protected override async Task OnPlay(PlayerChoiceContext context,CardPlay play)
    {
        var relic=Owner.Relics.OfType<DeathPoker>().FirstOrDefault(r=>r.Cards.Count>=30);if(relic==null)return;
        var cards=relic.Consume(relic.RandomIndexes(30));
        foreach(var face in cards) {
            if(!Owner.Creature.IsAlive)break;
            var target=PokerEffects.RandomEnemy(play);if(target==null)break;
            // Roll once for each original projectile. Extra projectiles cannot recurse.
            bool extra=Owner.RunState.Rng.CombatCardGeneration.NextInt(100)<7;
            await PokerEffects.Hit(context,play,target,3,face);
            if(extra && Owner.Creature.IsAlive) await PokerEffects.Hit(context,play,PokerEffects.RandomEnemy(play),3,face);
        }
    }
}
