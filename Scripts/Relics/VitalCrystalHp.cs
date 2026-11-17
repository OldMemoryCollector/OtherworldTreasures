using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib;
using STS2RitsuLib.RunData;

namespace OtherworldTreasures.Scripts.Relics;

public sealed class VitalCrystalHpSave
{
    public int Copies { get; set; }
    public decimal BaseMaxHp { get; set; }
    public int AppliedMaxHp { get; set; }
}

internal static class VitalCrystalHp
{
    private static PlayerRunSavedData<VitalCrystalHpSave> _slot = null!;

    internal static void Initialize()
    {
        using (RitsuLibFramework.BeginModDataRegistration(Entry.ModId))
            _slot = RitsuLibFramework.GetRunSavedDataStore(Entry.ModId).RegisterPerPlayer(
                "vital_crystal_hp_v1", () => new VitalCrystalHpSave(),
                options: new RunSavedDataOptions { WritePolicy = RunSavedDataWritePolicy.WhenSet });
    }

    // Run data owns the permanent effect, so merging/removing the icon does not erase it.
    internal static VitalCrystalHpSave? Get(Creature creature, VitalCrystal? acquiring = null)
    {
        if (_slot == null || creature.Player is not { RunState: RunState } player) return null;
        var data = _slot.Get(player);
        if (data.Copies <= 0)
        {
            int legacyCopies = (int)Math.Min(int.MaxValue, player.Relics.OfType<VitalCrystal>()
                .Where(relic => relic != acquiring).Sum(relic => (long)relic.Copies));
            if (legacyCopies <= 0) return null;
            // Old saves already contain the old acquisition bonus. Preserve that total.
            Write(player, legacyCopies, VitalCrystalHpMath.InferBase(creature.MaxHp, legacyCopies), creature.MaxHp);
            data = _slot.Get(player);
        }
        else if (data.AppliedMaxHp != creature.MaxHp)
        {
            // Native save synchronization can write the final maximum directly.
            Write(player, data.Copies, VitalCrystalHpMath.InferBase(creature.MaxHp, data.Copies), creature.MaxHp);
            data = _slot.Get(player);
        }
        return data;
    }

    internal static async Task Grant(VitalCrystal relic)
    {
        var player = relic.Owner;
        var creature = player.Creature;
        var previous = Get(creature, relic);
        int oldCopies = previous?.Copies ?? 0;
        decimal baseMaxHp = previous?.BaseMaxHp ?? creature.MaxHp;
        int oldMaxHp = creature.MaxHp;
        int copies = (int)Math.Min(int.MaxValue, (long)oldCopies + relic.Copies);
        Write(player, copies, baseMaxHp, oldMaxHp);
        try
        {
            decimal gained = await CreatureCmd.SetMaxHp(creature, baseMaxHp);
            var history = player.RunState.CurrentMapPointHistoryEntry;
            if (history != null) history.GetEntry(player.NetId).MaxHpGained += (int)Math.Max(0m, gained);
            await CreatureCmd.Heal(creature, Math.Max(0m, gained));
        }
        catch
        {
            if (creature.MaxHp == oldMaxHp) Write(player, oldCopies, baseMaxHp, oldMaxHp);
            throw;
        }
    }

    internal static void Commit(Creature creature, decimal baseMaxHp, int expectedMaxHp)
    {
        if (creature.Player is not { } player || creature.MaxHp != expectedMaxHp) return;
        var current = _slot.Get(player);
        Write(player, current.Copies, baseMaxHp, creature.MaxHp);
    }

    private static void Write(Player player, int copies, decimal baseMaxHp, int appliedMaxHp) =>
        _slot.Modify(player, data =>
        {
            data.Copies = copies;
            data.BaseMaxHp = baseMaxHp;
            data.AppliedMaxHp = appliedMaxHp;
        });
}
