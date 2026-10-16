using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib;
using STS2RitsuLib.RunData;

namespace OtherworldTreasures.Scripts.Events;

public sealed class TrainingSave
{
    public Dictionary<string, int> Remaining { get; set; } = new();
    public int NextBombId { get; set; }
}

internal static class TrainingProgress
{
    private static PlayerRunSavedData<TrainingSave> _slot = null!;
    internal static void Initialize()
    {
        using (RitsuLibFramework.BeginModDataRegistration(Entry.ModId))
            _slot = RitsuLibFramework.GetRunSavedDataStore(Entry.ModId).RegisterPerPlayer(
                "martial_training_v1", () => new TrainingSave(),
                options: new RunSavedDataOptions { WritePolicy = RunSavedDataWritePolicy.WhenSet });
    }

    internal static int Remaining(Player player, CardModel card, int limit) =>
        player.RunState is not MegaCrit.Sts2.Core.Runs.RunState ? limit :
            _slot.Get(player).Remaining.GetValueOrDefault(card.Id.Entry, limit);

    internal static bool Spend(Player player, CardModel card, int limit)
    {
        var remaining = Remaining(player, card, limit);
        if (remaining <= 0) return false;
        _slot.Modify(player, data => data.Remaining[card.Id.Entry] = remaining - 1);
        return true;
    }

    internal static int NewBombId(Player player)
    {
        int id = _slot.Get(player).NextBombId + 1;
        _slot.Modify(player, data => data.NextBombId = id);
        return id;
    }
}
