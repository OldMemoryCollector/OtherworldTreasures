using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib;
using STS2RitsuLib.RunData;

namespace OtherworldTreasures.Scripts.Rewards;

public sealed class ShimmerDropSave
{
    public HashSet<string> RolledRooms { get; set; } = new();
}
internal static class ShimmerDropData
{
    private static RunSavedData<ShimmerDropSave> _slot = null!;
    internal static void Initialize()
    {
        using (RitsuLibFramework.BeginModDataRegistration(Entry.ModId))
            _slot = RitsuLibFramework.GetRunSavedDataStore(Entry.ModId).Register("shimmer_drops",
                static () => new ShimmerDropSave(), new RunSavedDataOptions { WritePolicy = RunSavedDataWritePolicy.WhenNonDefault });
    }
    internal static bool TryBegin(Player player, CombatRoom room)
    {
        var run = (RunState)player.RunState;
        string key = $"{run.CurrentActIndex}:{run.TotalFloor}:{run.CurrentMapPointHistoryEntry?.Rooms.Count ?? 0}:{room.Encounter.Id}:{player.NetId}";
        if (_slot.Get(run).RolledRooms.Contains(key)) return false;
        _slot.Modify(run, data => data.RolledRooms.Add(key));
        return true;
    }
}
