using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Konoe;

[RegisterRelic(typeof(SharedRelicPool))]
public sealed class KonoeSagesBook : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Starter;
    #if STS2_113
    protected override bool IsAllowedInternal(IRunState runState)
    #else
    public override bool IsAllowed(IRunState runState)
    #endif
        => DevelopmentContent.Nine;
    public override bool IsAllowedInShops => DevelopmentContent.Nine;
    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://OtherworldTreasures/images/characters/nine/relic_icon.png",
        IconOutlinePath: "res://OtherworldTreasures/images/characters/nine/relic_outline.png",
        BigIconPath: "res://OtherworldTreasures/images/characters/nine/relic.png");
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => [new HoverTip(
        new LocString("static_hover_tips", "KONOE_ELEMENTS.title"),
        new LocString("static_hover_tips", "KONOE_ELEMENTS.description"))];

    // Like Toolbox, resolve the synchronized choice before the first hand is drawn.
    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext context, ICombatState combat)
    {
        if (player != Owner || player.PlayerCombatState?.TurnNumber != 1) return;
        var state = await KonoeElements.EnsureState(context, player);
        if (state == null || state.StarterChoiceMade) return;
        state.StarterChoiceMade = true;
        var options = new CardModel[] {
            combat.CreateCard(ModelDb.Card<KonoeWaterChoice>(), player),
            combat.CreateCard(ModelDb.Card<KonoeWindChoice>(), player),
            combat.CreateCard(ModelDb.Card<KonoeFireChoice>(), player)
        };
        Flash();
        var chosen = await CardSelectCmd.FromChooseACardScreen(context, options, player);
        if (chosen is KonoeElementChoice element) await KonoeElements.Gain(context, player, element.Element);
    }
}

public abstract class KonoeElementChoice : ModCardTemplate
{
    protected KonoeElementChoice() : base(0, CardType.Skill, CardRarity.Token, TargetType.Self, false) { }
    public abstract KonoeElement Element { get; }
    public override bool CanBeGeneratedInCombat => false;
    public override bool CanBeGeneratedByModifiers => false;
    public override CardAssetProfile AssetProfile => KonoeCardArt.Profile;
    protected override Task OnPlay(PlayerChoiceContext context, CardPlay play) => Task.CompletedTask;
}
[RegisterCard(typeof(KonoeCardPool))]
public sealed class KonoeWaterChoice : KonoeElementChoice { public override KonoeElement Element => KonoeElement.Water; }
[RegisterCard(typeof(KonoeCardPool))]
public sealed class KonoeWindChoice : KonoeElementChoice { public override KonoeElement Element => KonoeElement.Wind; }
[RegisterCard(typeof(KonoeCardPool))]
public sealed class KonoeFireChoice : KonoeElementChoice { public override KonoeElement Element => KonoeElement.Fire; }

