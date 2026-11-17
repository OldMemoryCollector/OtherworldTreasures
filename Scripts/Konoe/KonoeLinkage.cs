using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib;

namespace OtherworldTreasures.Scripts.Konoe;

internal static class KonoeLinkage
{
    internal static async Task Draw(PlayerChoiceContext context, Player player, Func<CardModel, bool> condition, bool retainDrawnCards = false)
    {
        if (player.Creature.CombatState is not { } combat || !player.Creature.IsAlive ||
            PileType.Hand.GetPile(player).Cards.Count >= RitsuLibFramework.GetMaxHandSize(player) || !Hook.ShouldDraw(combat, player, false, out _)) return;
        var draw = PileType.Draw.GetPile(player);
        var selected = draw.Cards.FirstOrDefault(condition);
        if (selected == null) return;
        // Native draw preserves draw triggers, history, hand limits and no-draw effects.
        if (draw.Cards.FirstOrDefault() != selected) await CardPileCmd.Add(selected, draw, CardPilePosition.Top);
        if (await CardPileCmd.Draw(context, player) is { } card && retainDrawnCards)
            CardCmd.ApplySingleTurnRetain(card);
    }
}
