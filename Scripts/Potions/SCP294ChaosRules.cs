using System.Text.Json;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib;
using STS2RitsuLib.Networking.Sidecar;
using STS2RitsuLib.RunData;

namespace OtherworldTreasures.Scripts.Potions;

public sealed class CrazyPotionRunSave
{
    public bool Initialized { get; set; }
    public bool NeowRelics { get; set; }
    public bool MonsterPotions { get; set; }
    public bool PotionIntents { get; set; }
    public StatusPurgeRule StatusPurge { get; set; } = new();
}

// Run options are captured once, persisted through RitsuLib, and supplied by the host.
// Combat recipes use a separate deterministic RNG so UI refreshes cannot consume gameplay RNG.
internal static class Scp294ChaosRules
{
    private readonly record struct Snapshot(ulong Seed, CrazyPotionRunSave Rules);
    private static RunSavedData<CrazyPotionRunSave> _slot = null!;
    private static RunState? _run;
    private static Snapshot? _pending;
    private static TaskCompletionSource<bool> _ready = NewReady();
    private static TaskCompletionSource<bool> NewReady() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static readonly RitsuLibSidecarSyncMessageDescriptor<Snapshot> Sync = new(
        Entry.ModId, "crazy_potion_rules_v1", value => JsonSerializer.SerializeToUtf8Bytes(value),
        bytes => JsonSerializer.Deserialize<Snapshot>(bytes), Receive,
        LocationTargeted: false, ShouldBuffer: false, DispatchLocalOnBroadcast: false);
    private static readonly RitsuLibSidecarSyncMessageDescriptor<bool> Request = new(
        Entry.ModId, "crazy_potion_rules_request_v1", value => [value ? (byte)1 : (byte)0],
        bytes => bytes.Length == 1 && bytes[0] == 1, Reply,
        LocationTargeted: false, ShouldBuffer: false, DispatchLocalOnBroadcast: false);

    internal static void Initialize()
    {
        using (RitsuLibFramework.BeginModDataRegistration(Entry.ModId))
            _slot = RitsuLibFramework.GetRunSavedDataStore(Entry.ModId).Register(
                "crazy_potion_rules_v1", () => new CrazyPotionRunSave(),
                new RunSavedDataOptions { WritePolicy = RunSavedDataWritePolicy.WhenSet });
        RitsuLibSidecarSyncMessages.Register(Sync);
        RitsuLibSidecarSyncMessages.Register(Request);
        RunManager.Instance.RunStarted += Start;
    }

    private static CrazyPotionRunSave Capture() => new()
    {
        Initialized = true,
        NeowRelics = Scp294Settings.EverywhereMode && Scp294Settings.Data.CrazyNeowRelics,
        MonsterPotions = Scp294Settings.EverywhereMode && Scp294Settings.Data.CrazyMonsterPotions,
        PotionIntents = Scp294Settings.EverywhereMode && Scp294Settings.Data.CrazyPotionIntents,
        StatusPurge = Scp294StatusPurge.Capture(Scp294Settings.Data)
    };

    private static readonly CrazyPotionRunSave Disabled = new();
    internal static CrazyPotionRunSave For(IRunState run) => run is RunState state ? _slot.Get(state) : Disabled;
    internal static Task WaitReady() => _ready.Task.WaitAsync(TimeSpan.FromSeconds(15));

    private static void Start(RunState run)
    {
        _run = run;
        _ready = NewReady();
        Scp294MonsterPotions.Reset();
        if (RunManager.Instance.NetService.Type == NetGameType.Client)
        {
            if (_pending is { } pending && pending.Seed == (ulong)run.Rng.Seed) Apply(pending);
            RitsuLibSidecarSyncMessages.SendToHost(RunManager.Instance, Request, true);
            return;
        }
        if (!For(run).Initialized) Store(run, Capture());
        _ready.TrySetResult(true);
        if (RunManager.Instance.NetService.Type == NetGameType.Host)
            Broadcast();
    }

    private static void Store(RunState run, CrazyPotionRunSave rules) => _slot.Modify(run, data =>
    {
        data.Initialized = true;
        data.NeowRelics = rules.NeowRelics;
        data.MonsterPotions = rules.MonsterPotions;
        data.PotionIntents = rules.PotionIntents;
        data.StatusPurge = rules.StatusPurge ?? new StatusPurgeRule();
    });

    private static void Broadcast()
    {
        if (_run == null) return;
        var result = RitsuLibSidecarSyncMessages.Send(RunManager.Instance, Sync,
            new Snapshot((ulong)_run.Rng.Seed, For(_run)));
        if (!result) throw new InvalidOperationException("Crazy potion rules synchronization failed");
    }

    private static Task Reply(RitsuLibSidecarSyncMessageContext<bool> context)
    {
        if (context.IsHostIngest && _run != null && _ready.Task.IsCompletedSuccessfully) Broadcast();
        return Task.CompletedTask;
    }

    private static Task Receive(RitsuLibSidecarSyncMessageContext<Snapshot> context)
    {
        if (context.IsHostIngest) return Task.CompletedTask;
        _pending = context.Message;
        if (_run != null && context.Message.Seed == (ulong)_run.Rng.Seed) Apply(context.Message);
        return Task.CompletedTask;
    }

    private static void Apply(Snapshot snapshot)
    {
        Store(_run!, snapshot.Rules);
        _ready.TrySetResult(true);
        var combat = CombatManager.Instance.DebugOnlyGetState();
        if (combat == null || combat.RunState != _run) return;
        foreach (var enemy in combat.Enemies)
            if (enemy.Monster is { } monster)
            {
                Scp294MonsterPotions.Prepare(monster);
                // A client resuming directly into combat may receive the host snapshot after UI creation.
                if (enemy.GetCreatureNode() is { } node) TaskHelper.RunSafely(node.RefreshIntents());
            }
    }
}

[HarmonyPatch(typeof(AncientEventModel), "BeforeEventStarted")]
internal static class Scp294ChaosNeowReady
{
    private static void Postfix(AncientEventModel __instance, ref Task __result)
    {
        if (__instance is Neow) __result = Wait(__result);
    }
    private static async Task Wait(Task original) { await original; await Scp294ChaosRules.WaitReady(); }
}
