using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using OtherworldTreasures.Scripts.Ancients;
using STS2RitsuLib.Scaffolding.Content;
using Godot;
using OtherworldTreasures.Scripts.UI;

namespace OtherworldTreasures.Scripts.Events;

// EventModel seeds its own RNG from the run seed and player slot. Re-entering a saved
// event reconstructs the same options; only choices perform inventory mutations.
public abstract class OtherworldEvent : ModEventTemplate
{
    protected new MegaCrit.Sts2.Core.Entities.Players.Player Owner => base.Owner ??
        throw new InvalidOperationException("An event option requires an active event owner.");
    public override EventAssetProfile AssetProfile => new(
        InitialPortraitPath: "res://OtherworldTreasures/images/placeholders/beta.png");
    protected override PackedScene? TryCreateLayoutPackedScene() =>
        this is GoldenAxe or SwordInTheLake or DragonBallTraining or TerrariaReunion or Scp914 or Scp294 or ShimmerLake
            ? FullArtEventLayout.Scene : base.TryCreateLayoutPackedScene();
    private bool _resolved;
    protected virtual bool AncientExempt => false;
    public override bool IsAllowed(IRunState runState) =>
        runState.CurrentActIndex == 1 && !Visited(runState, Id) &&
        (AncientExempt || !Visited(runState, ModelDb.AncientEvent<MemoryKeeper>().Id));

    internal static bool Visited(IRunState run, ModelId id) => run.MapPointHistory
        .SelectMany(act => act).SelectMany(point => point.Rooms).Any(room => room.ModelId == id);

    protected async Task Resolve(Func<Task> effect, string page)
    {
        if (_resolved) return;
        _resolved = true;
        await effect();
        SetEventFinished(PageDescription(page));
    }

    protected EventOption Leave() => new(this,
        () => Resolve(() => Task.CompletedTask, "LEAVE"), InitialOptionKey("LEAVE"));
}
