using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models.RelicPools;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Interactions.RightClick;

namespace OtherworldTreasures.Scripts.Relics;

[RegisterRelic(typeof(SharedRelicPool))]
public sealed class CombatScouter : ModRelicTemplate
{
    // This is a local visual preference, not a saved or synchronized gameplay
    // property. Reloading a run starts with the display enabled. Visibility is
    // independently gated by active combat, so the map never shows readouts.
    public bool DisplayEnabled { get; private set; } = true;

    internal bool ToggleDisplay(Player viewer)
    {
        if (!IsMutable || Owner != viewer || !viewer.Relics.Contains(this) || !LocalContext.IsMe(viewer)) return false;
        DisplayEnabled = !DisplayEnabled;
        Status = DisplayEnabled ? RelicStatus.Active : RelicStatus.Normal;
        Flash();
        return true;
    }
    public override RelicRarity Rarity => RelicRarity.Event;
    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://OtherworldTreasures/images/relics/CombatScouter_85.png",
        IconOutlinePath: "res://OtherworldTreasures/images/relics/CombatScouter_85.png",
        BigIconPath: "res://OtherworldTreasures/images/relics/CombatScouter.png");
    public override bool ShouldAddToDeck(MegaCrit.Sts2.Core.Models.CardModel card) =>
        card is not Cards.MartialTrainingCard skill ||
        Events.TrainingProgress.Remaining(card.Owner, card, skill.UseLimit) > 0;
}

// Consume this cosmetic interaction locally through RitsuLib's public input
// extension point; it does not enter a multiplayer gameplay action queue.
internal sealed class ScouterDisplayRightClick : IModRightClickHandler
{
    internal static readonly ScouterDisplayRightClick Instance = new();
    public int Priority => 0;
    public bool TryHandle(ModRightClickContext context)
    {
        if (context.Model is not CombatScouter model || model.Owner != context.Player || !LocalContext.IsMe(context.Player)) return false;
        var owned = context.Player.Relics.OfType<CombatScouter>().FirstOrDefault();
        return owned?.ToggleDisplay(context.Player) == true;
    }
}
