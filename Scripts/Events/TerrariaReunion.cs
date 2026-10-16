using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using OtherworldTreasures.Scripts.Relics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Events;

[RegisterSharedEvent]
public sealed class TerrariaReunion : OtherworldEvent
{
    public override EventAssetProfile AssetProfile => new(
        InitialPortraitPath: "res://OtherworldTreasures/images/events/terraria_reunion.png");

    private static readonly Type[] Gifts = [typeof(MythicCharm), typeof(SporeSac), typeof(CrossNecklace)];
    public override bool IsAllowed(IRunState runState) => base.IsAllowed(runState) &&
        runState.Players.Any(p => Gifts.Any(t => p.Relics.All(r => r.GetType() != t)));

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        var gifts = new (RelicModel Relic, string Key)[]
        {
            (ModelDb.Relic<MythicCharm>(), "CHARM_OF_MYTHS"),
            (ModelDb.Relic<SporeSac>(), "SPORE_SAC"),
            (ModelDb.Relic<CrossNecklace>(), "ANKH_CHARM")
        };
        // A shared multiplayer event can include a player who already has all
        // three. Only that unavailable-reward case needs a safe way to leave.
        if (gifts.All(g => Owner.Relics.Any(r => r.Id == g.Relic.Id))) return [Leave()];
        var result = new List<EventOption>();
        foreach (var (canonical, key) in gifts)
        {
            var relic = canonical.ToMutable();
            relic.Owner = Owner;
            result.Add(new EventOption(this, Owner.Relics.Any(r => r.Id == relic.Id) ? null :
                () => Resolve(async () => { if (Owner.Relics.All(r => r.Id != relic.Id)) await RelicCmd.Obtain(relic, Owner); }, "CHOSEN"),
                InitialOptionKey(key), relic.HoverTips));
        }
        return result;
    }
}
