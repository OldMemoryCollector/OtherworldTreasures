using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Runs;
using OtherworldTreasures.Scripts.Relics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Events;

[RegisterSharedEvent]
public sealed class SwordInTheLake : OtherworldEvent
{
    public override EventAssetProfile AssetProfile => new(
        InitialPortraitPath: "res://OtherworldTreasures/images/events/sword_in_the_lake.png");
    protected override bool AncientExempt => true;
    public override bool IsAllowed(IRunState runState) => base.IsAllowed(runState) &&
        runState.Players.Any(p => p.Relics.Any(CanAwaken));

    // Preserve deliberate stone-sword evolution attempts after the third elite win.
    internal static bool CanAwaken(RelicModel relic) => relic is SwordOfJade ||
        relic is SwordOfStone { ElitesDefeated: <= 2 };

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        var result = new List<EventOption>();
        foreach (var sword in Owner.Relics.Where(CanAwaken).ToList())
        {
            RelicModel reward = sword is SwordOfJade ? ModelDb.Relic<TrueExcalibur>().ToMutable() : ModelDb.Relic<Excalibur>().ToMutable();
            reward.Owner = Owner;
            result.Add(new EventOption(this, () => Resolve(async () =>
            {
                if (Owner.Relics.Contains(sword)) await RelicCmd.Replace(sword, reward);
            }, sword is SwordOfJade ? "JADE_AWAKENED" : "STONE_AWAKENED"),
                InitialOptionKey(sword is SwordOfJade ? "AWAKEN_JADE" : "AWAKEN_STONE"), reward.HoverTips));
        }
        result.Add(Leave());
        return result;
    }
}
