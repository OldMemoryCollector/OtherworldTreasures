using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib;
using STS2RitsuLib.RunData;

namespace OtherworldTreasures.Scripts.Events;

public sealed class MustDieRunSave
{
    public bool HasObtainedSeriesCard { get; set; }
    public bool HasFinishedMachineLoreCombat { get; set; }
}

/// <summary>Run-wide, save-backed limit for the XX必须死 card family.</summary>
public static class MustDieRunData
{
    private static RunSavedData<MustDieRunSave> _slot = null!;

    internal static void Initialize()
    {
        using (RitsuLibFramework.BeginModDataRegistration(Entry.ModId))
        {
            _slot = RitsuLibFramework.GetRunSavedDataStore(Entry.ModId).Register(
                "must_die_series",
                static () => new MustDieRunSave(),
                new RunSavedDataOptions { WritePolicy = RunSavedDataWritePolicy.WhenNonDefault });
        }
    }

    public static bool HasObtained(IRunState runState) => _slot.Get((RunState)runState).HasObtainedSeriesCard;

    public static bool ShouldShowMachineLore(IRunState runState) =>
        !_slot.Get((RunState)runState).HasFinishedMachineLoreCombat;

    public static void FinishMachineLoreCombat(IRunState runState)
    {
        if (!ShouldShowMachineLore(runState)) return;
        _slot.Modify((RunState)runState, static data => data.HasFinishedMachineLoreCombat = true);
    }

    public static bool TryClaim(IRunState runState)
    {
        if (HasObtained(runState)) return false;
        _slot.Modify((RunState)runState, static data => data.HasObtainedSeriesCard = true);
        return true;
    }
}
