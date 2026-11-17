using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Rooms;
using OtherworldTreasures.Scripts.Potions;

namespace OtherworldTreasures.Scripts.Balance;

// Each item owns a separate choice. There is deliberately no global enable flag.
public sealed class RelicBalanceOptions
{
    public bool KurasDice { get; set; }
    public bool WhatIfPhoneBooth { get; set; }
    public bool ShrinkRay { get; set; }
    public bool YorozuyaScooter { get; set; }

    internal RelicBalanceOptions Copy() => new()
    {
        KurasDice = KurasDice,
        WhatIfPhoneBooth = WhatIfPhoneBooth,
        ShrinkRay = ShrinkRay,
        YorozuyaScooter = YorozuyaScooter
    };
}

internal static class RelicBalanceRules
{
    private static readonly RelicBalanceOptions Disabled = new();

    internal static bool Ready(Player? owner) => owner != null && Scp294ChaosRules.For(owner.RunState).Initialized;
    internal static Task WaitReady(Player? owner) => Ready(owner) ? Task.CompletedTask : Scp294ChaosRules.WaitReady();

    internal static bool KurasDice(Player? owner) => Options(owner).KurasDice;
    internal static bool WhatIfPhoneBooth(Player? owner) => Options(owner).WhatIfPhoneBooth;
    internal static bool ShrinkRay(Player? owner) => Options(owner).ShrinkRay;
    internal static bool YorozuyaScooter(Player? owner) => Options(owner).YorozuyaScooter;

    // EnterRoomDebug changes room IDs and appends history when the phone restarts.
    // The map position and ActFloor remain stable across those retries.
    internal static string CombatKey(Player owner, CombatRoom room)
    {
        var run = owner.RunState;
        var coord = run.CurrentMapCoord;
        return $"{run.CurrentActIndex}:{run.ActFloor}:{coord?.col ?? -1}:{coord?.row ?? -1}:{room.Encounter.Id.Entry}";
    }

    private static RelicBalanceOptions Options(Player? owner)
    {
        // Follow this relic's owner when present, not another run or local player's settings.
        var run = owner?.RunState ?? (RunManager.Instance is { IsInProgress: true } manager
            ? manager.DebugOnlyGetState() : null);
        if (run != null)
        {
            var rules = Scp294ChaosRules.For(run);
            return rules.Initialized ? rules.RelicBalance ?? Disabled : Disabled;
        }
        return Scp294Settings.Data.RelicBalance ?? Disabled;
    }
}
