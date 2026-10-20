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
    internal const float HiddenCharmChance = .10f;
    private List<(RelicModel Relic, string Key)>? _offers;
    public override EventAssetProfile AssetProfile => new(
        InitialPortraitPath: "res://OtherworldTreasures/images/events/terraria_reunion.png");
    private static List<(RelicModel Relic, string Key)> Pool(IRunState run)
    {
        var pool = new List<(RelicModel, string)>
        {
            (ModelDb.Relic<MythicCharm>(), "CHARM_OF_MYTHS"),
            (ModelDb.Relic<SporeSac>(), "SPORE_SAC"),
            (ModelDb.Relic<TerrariaCrossNecklace>(), "CROSS_NECKLACE"),
            (ModelDb.Relic<HoneyThorns>(), "HONEY_THORNS"),
            (ModelDb.Relic<CrossNecklace>(), "ANKH_CHARM")
        };
        if (run.Players.Count > 1) pool.Add((ModelDb.Relic<PaladinShield>(), "PALADIN_SHIELD"));
        return pool;
    }
    public override bool IsAllowed(IRunState run) => base.IsAllowed(run) &&
        run.Players.Any(player => Pool(run).Any(gift => player.Relics.All(owned => owned.Id != gift.Relic.Id)));
    protected override void DeepCloneFields() { base.DeepCloneFields(); _offers = null; }
    private List<(RelicModel Relic, string Key)> RollOffers()
    {
        var pool = Pool(Owner.RunState).Where(gift => Owner.Relics.All(owned => owned.Id != gift.Relic.Id)).ToList();
        var hidden = pool.FirstOrDefault(gift => gift.Relic is CrossNecklace);
        pool.RemoveAll(gift => gift.Relic is CrossNecklace);
        var offers = new List<(RelicModel Relic, string Key)>();
        if (hidden.Relic != null && Rng.NextFloat() < HiddenCharmChance) offers.Add(hidden);
        while (offers.Count < 3 && pool.Count > 0)
        {
            int index = Rng.NextInt(pool.Count);
            offers.Add(pool[index]); pool.RemoveAt(index);
        }
        for (int i = offers.Count - 1; i > 0; i--)
        {
            int index = Rng.NextInt(i + 1);
            (offers[i], offers[index]) = (offers[index], offers[i]);
        }
        return offers;
    }
    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        _offers ??= RollOffers();
        if (_offers.Count == 0) return [Leave()];
        return _offers.Select(gift =>
        {
            var relic = gift.Relic.ToMutable(); relic.Owner = Owner;
            return new EventOption(this, () => Resolve(async () =>
            {
                if (Owner.Relics.All(owned => owned.Id != relic.Id)) await RelicCmd.Obtain(relic, Owner);
            }, "CHOSEN"), InitialOptionKey(gift.Key), relic.HoverTips);
        }).ToArray();
    }
}
