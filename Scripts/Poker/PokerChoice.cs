using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;
using OtherworldTreasures.Scripts.UI;
namespace OtherworldTreasures.Scripts.Poker;

internal static class PokerChoice
{
    internal static async Task<PokerSelection> Choose(PlayerChoiceContext context, Player player, IReadOnlyList<PokerFace> snapshot)
    {
        var run=RunManager.Instance;var sync=run.PlayerChoiceSynchronizer;
        uint choiceId=sync.ReserveChoiceId(player);
        await context.SignalPlayerChoiceBegun(player,PlayerChoiceOptions.None);
        try {
            IReadOnlyList<int> payload;
            if(run.NetService.Type!=NetGameType.Replay && (LocalContext.IsMe(player)||run.NetService.Type==NetGameType.Singleplayer)) {
                var screen=PokerSelectScreen.Open(snapshot,false);
                var choice=await screen.Completion;payload=choice.Encode();
                sync.SyncLocalChoice(player,choiceId,PlayerChoiceResult.FromIndexes(payload.ToList()));
            } else payload=(await sync.WaitForRemoteChoice(player,choiceId)).AsIndexes();
            return PokerSelection.Decode(snapshot,payload);
        } finally { await context.SignalPlayerChoiceEnded(); }
    }
}
