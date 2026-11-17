using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.ValueProps;
using OtherworldTreasures.Scripts.Poker;
using OtherworldTreasures.Scripts.Relics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
namespace OtherworldTreasures.Scripts.Cards;

[RegisterCard(typeof(TokenCardPool))]
public sealed class PokerShield:ModCardTemplate
{
    public PokerShield():base(1,CardType.Skill,CardRarity.Token,TargetType.Self, DevelopmentContent.Extras){}
    public override bool CanBeGeneratedInCombat=>false;
    public override bool CanBeGeneratedByModifiers=>false;
    public override bool GainsBlock=>true;
    public override int MaxUpgradeLevel=>0;
    public override CardAssetProfile AssetProfile=>new(PortraitPath:DeathPoker.PileImage);
    protected override bool IsPlayable=> Owner.Relics.OfType<DeathPoker>().Any(r=>r.Cards.Count>=10);
    protected override async Task OnPlay(PlayerChoiceContext context,CardPlay play)
    {
        var relic=Owner.Relics.OfType<DeathPoker>().FirstOrDefault(r=>r.Cards.Count>=10);if(relic==null)return;
        var snapshot=relic.Cards.ToArray();var selection=PokerRules.AutoGroup(snapshot,relic.RandomIndexes(10));
        relic.Consume(selection.AllIndexes);
        await CreatureCmd.GainBlock(Owner.Creature,30,ValueProp.Move,play);
        // Only actual combinations trigger; leftover ordinary cards do not add flying-card damage.
        await PokerEffects.Resolve(context,play,snapshot,selection, scatterDamage:false);
    }
}
