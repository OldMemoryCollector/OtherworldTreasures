using System.Runtime.CompilerServices;
using System.Threading;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace OtherworldTreasures.Scripts.Potions;

// Attribution is separate from the native merged amount. Opposing signed stat grants may
// cancel the visible power, but each potion contribution still has to be removable.
internal static class Scp294PotionSources
{
    internal sealed class Slice
    {
        internal long Amount;
        internal long MarkerUnits;
        internal MarkerLink? Marker;
    }
    internal sealed class PowerShare
    {
        internal required Creature Owner;
        internal required PowerModel Canonical;
        internal PowerModel? Power;
        internal Creature? Applier;
        internal int Order;
        internal readonly List<Slice> Slices = new();
        internal long Owned(StatusPurgePolarity polarity)
        {
            long total = Slices.Where(s => Matches(this, s, polarity)).Sum(s => Math.Abs(s.Amount));
            return Canonical.StackType == PowerStackType.Counter ? total : Math.Min(1, total);
        }
    }
    internal sealed class MarkerLink
    {
        internal required Creature Owner;
        internal required ModelId NativeId;
        internal required ModelId MarkerId;
        internal PowerModel? Marker;
        internal bool TemporaryStat;
        internal int Sign = 1;
        internal long OwnedMarkerUnits;
        internal bool Finished;
    }
    private sealed class Book
    {
        internal ICombatState? Combat;
        internal int NextOrder;
        internal readonly List<PowerShare> Powers = new();
        internal readonly List<MarkerLink> Markers = new();
    }
    private sealed class Frame : IDisposable
    {
        internal required Creature Target;
        internal required PowerModel Canonical;
        internal MarkerLink? Marker;
        internal bool MarkerIsRoot;
        internal long Numerator = 1;
        internal long Denominator = 1;
        internal int MarkerBefore;
        internal Frame? Previous;
        internal readonly List<Slice> NewSlices = new();
        public void Dispose() => Active.Value = Previous;
    }
    private sealed class Pause : IDisposable
    {
        internal Pause() => Suppressed.Value++;
        public void Dispose() => Suppressed.Value--;
    }
    private static readonly ConditionalWeakTable<Creature, Book> Books = new();
    private static readonly AsyncLocal<Frame?> Active = new();
    private static readonly AsyncLocal<int> Suppressed = new();
    private static readonly AsyncLocal<Creature?> CatalogOwner = new();
    private sealed class CatalogLease : IDisposable
    {
        private readonly Creature? _previous;
        private bool _disposed;
        internal CatalogLease(Creature owner) { _previous = CatalogOwner.Value; CatalogOwner.Value = owner; }
        public void Dispose() { if (_disposed) return; _disposed = true; CatalogOwner.Value = _previous; }
    }
    internal static IDisposable BeginCatalog(Creature owner) => new CatalogLease(owner);
    internal static object? BeginCatalogCommand(PowerModel power, Creature target, Creature? applier, CardModel? cardSource)
    {
        if (Suppressed.Value > 0 || Active.Value != null || CatalogOwner.Value == null || cardSource != null) return null;
        var canonical = ModelDb.GetById<PowerModel>(power.Id);
        var before = PowerCmd.FindExistingInstanceForStacking(canonical, target, applier);
        var frame = new Frame { Target = target, Canonical = canonical, Previous = Active.Value,
            MarkerBefore = before?.Amount ?? 0 };
        if (canonical is ITemporaryPower temporary) { frame.Marker = Link(target, canonical, temporary.InternallyAppliedPower, true); frame.MarkerIsRoot = true; }
        Active.Value = frame;
        return frame;
    }
    internal static Task FinishCatalogCommand(Task original, object? state)
    {
        if (state is not Frame frame) return original;
        frame.Dispose();
        return Finish(original, frame);
    }
    private static async Task Finish(Task original, Frame frame)
    {
        try { await original; }
        finally
        {
            if (frame.Marker is { } marker)
            {
                marker.Marker ??= frame.Target.GetPower(frame.Canonical.Id);
                long delta = Math.Max(0, (marker.Marker?.Amount ?? 0) - frame.MarkerBefore);
                marker.OwnedMarkerUnits += delta;
                AllocateMarkerUnits(frame.NewSlices, delta);
            }
        }
    }
    internal static void AbandonCatalogCommand(object? state) { if (state is Frame frame) frame.Dispose(); }
    private static readonly System.Reflection.FieldInfo OwnerField = AccessTools.Field(typeof(PowerModel), "_owner");
    internal static IDisposable Suspend() => new Pause();
    private static Creature? OwnerOf(PowerModel p) => (Creature?)OwnerField.GetValue(p);
    // Combat-local snapshots own immutable copies of attribution data. Power references
    // are identity hints only; restore always binds to the actually restored live powers.
    internal sealed class SourcesSnapshot
    {
        internal required Creature Owner;
        internal required ICombatState? Combat;
        internal required Func<PowerModel, bool> Includes;
        internal int NextOrder;
        internal required SavedShare[] Shares;
        internal required SavedMarker[] Markers;
    }
    internal sealed record SavedSlice(long Amount, long MarkerUnits, int MarkerIndex);
    internal sealed record SavedShare(ModelId Id, PowerModel? Hint, Creature? Applier, int Order, SavedSlice[] Slices);
    internal sealed record SavedMarker(ModelId NativeId, ModelId MarkerId, PowerModel? Hint, Creature? Applier, bool TemporaryStat, int Sign);

    internal static SourcesSnapshot CaptureSnapshot(Creature owner, Func<PowerModel, bool>? includes = null)
    {
        includes ??= static _ => true;
        var saved = new SourcesSnapshot { Owner = owner, Combat = owner.CombatState, Includes = includes,
            Shares = [], Markers = [] };
        if (!Current(owner, out var book)) return saved;
        saved.NextOrder = book.NextOrder;
        var markers = book.Markers.Where(m => !m.Finished && includes(ModelDb.GetById<PowerModel>(m.NativeId)) &&
            includes(ModelDb.GetById<PowerModel>(m.MarkerId))).ToArray();
        var markerIndices = new Dictionary<MarkerLink, int>(ReferenceEqualityComparer.Instance);
        for (int i = 0; i < markers.Length; i++) markerIndices[markers[i]] = i;
        saved.Markers = markers.Select(m => new SavedMarker(m.NativeId, m.MarkerId, m.Marker, m.Marker?.Applier, m.TemporaryStat, m.Sign)).ToArray();
        saved.Shares = book.Powers.Where(s => includes(s.Canonical)).Select(s => new SavedShare(s.Canonical.Id, Live(s), s.Applier, s.Order,
            s.Slices.Where(slice => slice.Amount != 0 && (slice.Marker == null || markerIndices.ContainsKey(slice.Marker)))
                .Select(slice => new SavedSlice(slice.Amount, slice.MarkerUnits,
                    slice.Marker == null ? -1 : markerIndices[slice.Marker])).ToArray())).ToArray();
        return saved;
    }
    private static PowerModel? BindSnapshotPower(Creature owner, ModelId id, PowerModel? hint, Creature? applier)
    {
        if (hint != null && owner.Powers.Contains(hint)) return hint;
        var canonical = ModelDb.GetById<PowerModel>(id);
        return canonical.InstanceType switch
        {
            PowerInstanceType.None => owner.GetPower(id),
            PowerInstanceType.InstancedPerApplier => owner.Powers.FirstOrDefault(p => p.Id == id && ReferenceEquals(p.Applier, applier)),
            _ => owner.Powers.FirstOrDefault(p => p.Id == id && ReferenceEquals(p.Applier, applier))
        };
    }
    private static readonly System.Reflection.FieldInfo NextTurnTriggered = AccessTools.Field(typeof(OtherworldTreasures.Scripts.Powers.SCP294NextTurnPower), "_triggered");
    private static readonly System.Reflection.FieldInfo ChargeExpired = AccessTools.Field(typeof(OtherworldTreasures.Scripts.Powers.SCP294ThisTurnChargePower), "_expired");
    private static void ResetSnapshotExpiry(PowerModel power)
    {
        if (power is OtherworldTreasures.Scripts.Powers.SCP294NextTurnPower) NextTurnTriggered.SetValue(power, false);
        if (power is OtherworldTreasures.Scripts.Powers.SCP294ThisTurnChargePower) ChargeExpired.SetValue(power, false);
    }
    internal static void RestoreSnapshot(Creature owner, SourcesSnapshot snapshot)
    {
        if (!ReferenceEquals(snapshot.Owner, owner) || !ReferenceEquals(snapshot.Combat, owner.CombatState)) return;
        var book = Of(owner);
        book.Powers.RemoveAll(s => snapshot.Includes(s.Canonical));
        book.Markers.RemoveAll(m => snapshot.Includes(ModelDb.GetById<PowerModel>(m.NativeId)) &&
            snapshot.Includes(ModelDb.GetById<PowerModel>(m.MarkerId)));
        var links = new MarkerLink?[snapshot.Markers.Length];
        for (int i = 0; i < links.Length; i++)
        {
            var saved = snapshot.Markers[i];
            var actual = BindSnapshotPower(owner, saved.MarkerId, saved.Hint, saved.Applier);
            if (actual == null) continue;
            ResetSnapshotExpiry(actual);
            var link = new MarkerLink { Owner = owner, NativeId = saved.NativeId, MarkerId = saved.MarkerId,
                Marker = actual, TemporaryStat = saved.TemporaryStat, Sign = saved.Sign };
            links[i] = link; book.Markers.Add(link);
        }
        foreach (var saved in snapshot.Shares)
        {
            var canonical = ModelDb.GetById<PowerModel>(saved.Id);
            var actual = BindSnapshotPower(owner, saved.Id, saved.Hint, saved.Applier);
            // Signed opposing grants can have no visible power. Keep those shares so
            // clearing one sign restores the other sign after an exact-zero rewind.
            if (actual == null && !canonical.AllowNegative) continue;
            if (actual != null) ResetSnapshotExpiry(actual);
            var share = new PowerShare { Owner = owner, Canonical = canonical, Power = actual,
                Applier = saved.Applier, Order = saved.Order };
            foreach (var slice in saved.Slices)
            {
                var marker = slice.MarkerIndex < 0 ? null : links[slice.MarkerIndex];
                if (slice.MarkerIndex >= 0 && marker == null) continue;
                share.Slices.Add(new Slice { Amount = slice.Amount, MarkerUnits = slice.MarkerUnits, Marker = marker });
                if (marker != null) marker.OwnedMarkerUnits += slice.MarkerUnits;
            }
            if (share.Slices.Count > 0) book.Powers.Add(share);
        }
        book.NextOrder = Math.Max(book.NextOrder, Math.Max(snapshot.NextOrder, book.Powers.Select(s => s.Order + 1).DefaultIfEmpty(0).Max()));
    }
    private static Book Of(Creature owner)
    {
        var book = Books.GetValue(owner, _ => new Book { Combat = owner.CombatState });
        if (!ReferenceEquals(book.Combat, owner.CombatState))
        {
            book.Combat = owner.CombatState;
            book.NextOrder = 0; book.Powers.Clear(); book.Markers.Clear();
        }
        return book;
    }
    private static bool Current(Creature owner, out Book book) => Books.TryGetValue(owner, out book!) && ReferenceEquals(book.Combat, owner.CombatState);
    internal static object? BeginRitual(PowerModel ritual)
    {
        if (Suppressed.Value > 0 || !Current(ritual.Owner, out var book) || ritual.Amount <= 0) return null;
        var share = book.Powers.FirstOrDefault(s => ReferenceEquals(s.Power, ritual));
        long owned = share?.Slices.Sum(s => Math.Max(0, s.Amount)) ?? 0;
        if (owned <= 0) return null;
        var frame = new Frame { Target = ritual.Owner, Canonical = ModelDb.Power<MegaCrit.Sts2.Core.Models.Powers.StrengthPower>(),
            Numerator = Math.Min(owned, ritual.Amount), Denominator = ritual.Amount, Previous = Active.Value };
        Active.Value = frame;
        return frame;
    }
    private static PowerShare Share(Creature owner, PowerModel power)
    {
        var book = Of(owner);
        var found = book.Powers.FirstOrDefault(s => ReferenceEquals(s.Power, power));
        found ??= book.Powers.FirstOrDefault(s => s.Canonical.Id == power.Id &&
            (power.InstanceType == PowerInstanceType.None || power.InstanceType == PowerInstanceType.InstancedPerApplier && ReferenceEquals(s.Applier, power.Applier)));
        if (found == null)
        {
            found = new PowerShare { Owner = owner, Canonical = ModelDb.GetById<PowerModel>(power.Id), Applier = power.Applier, Order = book.NextOrder++ };
            book.Powers.Add(found);
        }
        found.Power = power;
        return found;
    }
    private static PowerModel? Live(PowerShare share) => share.Power != null && share.Owner.Powers.Contains(share.Power) ? share.Power :
        share.Canonical.InstanceType == PowerInstanceType.Instanced ? null :
        share.Owner.Powers.FirstOrDefault(p => p.Id == share.Canonical.Id && (p.InstanceType == PowerInstanceType.None || ReferenceEquals(p.Applier, share.Applier)));
    private static MarkerLink Link(Creature owner, PowerModel marker, PowerModel native, bool stat)
    {
        var book = Of(owner);
        var found = book.Markers.FirstOrDefault(m => !m.Finished && m.MarkerId == marker.Id && m.NativeId == native.Id);
        if (found != null) return found;
        found = new MarkerLink { Owner = owner, MarkerId = marker.Id, NativeId = native.Id, TemporaryStat = stat,
            Sign = marker.Type == PowerType.Debuff ? -1 : 1 };
        book.Markers.Add(found);
        return found;
    }
    internal static async Task<T?> Apply<T>(PlayerChoiceContext context, Creature target, decimal amount, Creature? applier, CardModel? cardSource, bool silent = false) where T : PowerModel
    {
        var canonical = ModelDb.Power<T>();
        var before = PowerCmd.FindExistingInstanceForStacking(canonical, target, applier);
        var frame = new Frame { Target = target, Canonical = canonical, Previous = Active.Value };
        int markerBefore = before?.Amount ?? 0;
        if (canonical is ITemporaryPower temporary)
        {
            frame.Marker = Link(target, canonical, temporary.InternallyAppliedPower, true);
            frame.MarkerIsRoot = true;
        }
        Active.Value = frame;
        try
        {
            var result = await PowerCmd.Apply<T>(context, target, amount, applier, cardSource, silent);
            if (frame.Marker is { } link)
            {
                link.Marker = result ?? PowerCmd.FindExistingInstanceForStacking(canonical, target, applier);
                long markerDelta = Math.Max(0, (link.Marker?.Amount ?? 0) - markerBefore);
                link.OwnedMarkerUnits += markerDelta;
                AllocateMarkerUnits(frame.NewSlices, markerDelta);
            }
            return result;
        }
        finally { frame.Dispose(); }
    }
    internal static async Task ApplyCharge<TNative, TMarker>(PlayerChoiceContext context, Creature target, int amount, Creature? applier, CardModel? cardSource)
        where TNative : PowerModel where TMarker : PowerModel
    {
        var native = ModelDb.Power<TNative>();
        var marker = ModelDb.Power<TMarker>();
        var link = Link(target, marker, native, false);
        int markerBefore = target.GetPower(marker.Id)?.Amount ?? 0;
        var frame = new Frame { Target = target, Canonical = native, Marker = link, Previous = Active.Value };
        Active.Value = frame;
        try
        {
            await PowerCmd.Apply<TNative>(context, target, amount, applier, cardSource);
            int granted = (int)frame.NewSlices.Sum(s => Math.Max(0, s.Amount));
            if (granted <= 0) return;
            using (Suspend()) link.Marker = await PowerCmd.Apply<TMarker>(context, target, granted, applier, cardSource);
            long markerDelta = Math.Max(0, (link.Marker?.Amount ?? 0) - markerBefore);
            link.OwnedMarkerUnits += markerDelta;
            AllocateMarkerUnits(frame.NewSlices, markerDelta);
            if (link.Marker == null) foreach (var slice in frame.NewSlices) slice.Marker = null;
        }
        finally { frame.Dispose(); }
    }
    private static void AllocateMarkerUnits(List<Slice> slices, long units)
    {
        long total = slices.Sum(s => Math.Abs(s.Amount));
        long left = units;
        for (int i = 0; i < slices.Count; i++)
        {
            long n = i == slices.Count - 1 ? left : total == 0 ? 0 : units * Math.Abs(slices[i].Amount) / total;
            slices[i].MarkerUnits = n; left -= n;
        }
    }
    internal static void Changed(PowerModel power, int before)
    {
        if (Suppressed.Value > 0 || OwnerOf(power) is not { CombatState: not null } owner) return;
        long delta = (long)power.Amount - before;
        if (delta == 0) return;
        var frame = Active.Value;
        if (frame != null && ReferenceEquals(frame.Target, owner) &&
            (power.Id == frame.Canonical.Id || frame.MarkerIsRoot && power.Id == frame.Marker!.NativeId))
        {
            if (frame.MarkerIsRoot && power.Id == frame.Canonical.Id) { frame.Marker!.Marker = power; return; }
            if (!power.AllowNegative && delta < 0) { Consume(Share(owner, power), -delta); return; }
            // A derived Ritual grant can contain both native and potion Ritual layers.
            // Attribute only the potion fraction of the actual committed stat change.
            long ownedDelta = delta * frame.Numerator / frame.Denominator;
            if (ownedDelta == 0) return;
            var slice = new Slice { Amount = ownedDelta, Marker = frame.Marker };
            Share(owner, power).Slices.Add(slice); frame.NewSlices.Add(slice);
            return;
        }
        if (!Current(owner, out var book)) return;
        var marker = book.Markers.FirstOrDefault(m => !m.Finished && ReferenceEquals(m.Marker, power));
        if (marker != null)
        {
            // Native changes to an expiry marker are mirrored by the native temporary-power hook.
            if (delta < 0) ConsumeMarkerUnits(marker, -delta);
            return;
        }
        var share = book.Powers.FirstOrDefault(s => ReferenceEquals(s.Power, power));
        // A signed stat decrease is another effect, not resource consumption. Keep both sources
        // even when their sum becomes zero so clearing a potion can restore the other source.
        if (share != null && !power.AllowNegative && delta < 0) Consume(share, -delta);
    }
    private static void Consume(PowerShare share, long count)
    {
        foreach (var slice in share.Slices.Where(s => s.Amount > 0).ToArray())
        {
            long n = Math.Min(count, slice.Amount);
            ReduceSlice(slice, n, changeMarker: true); count -= n;
            if (count == 0) break;
        }
        share.Slices.RemoveAll(s => s.Amount == 0);
    }
    private static void ConsumeMarkerUnits(MarkerLink marker, long units)
    {
        foreach (var share in Of(marker.Owner).Powers)
            foreach (var slice in share.Slices.Where(s => ReferenceEquals(s.Marker, marker) && s.MarkerUnits > 0).ToArray())
            {
                long n = Math.Min(units, slice.MarkerUnits);
                long amount = n == slice.MarkerUnits ? Math.Abs(slice.Amount) : Math.Abs(slice.Amount) * n / slice.MarkerUnits;
                slice.Amount -= Math.Sign(slice.Amount) * amount;
                slice.MarkerUnits -= n; marker.OwnedMarkerUnits -= n; units -= n;
                if (units == 0) return;
            }
    }
    private static void ReduceSlice(Slice slice, long magnitude, bool changeMarker)
    {
        long old = Math.Abs(slice.Amount);
        long units = magnitude >= old ? slice.MarkerUnits : slice.MarkerUnits * magnitude / old;
        slice.Amount -= Math.Sign(slice.Amount) * magnitude;
        slice.MarkerUnits -= units;
        if (slice.Marker is not { } link || units == 0) return;
        link.OwnedMarkerUnits = Math.Max(0, link.OwnedMarkerUnits - units);
        if (changeMarker && link.Marker is { } marker && link.Owner.Powers.Contains(marker))
        {
            using (Suspend())
            {
                marker.SetAmount(Math.Max(0, marker.Amount - (int)units));
                if (marker.Amount == 0) { marker.RemoveInternal(); link.Finished = true; }
            }
        }
    }
    internal static bool Matches(PowerShare share, Slice slice, StatusPurgePolarity polarity)
    {
        if (slice.Amount == 0) return false;
        PowerType type = share.Canonical.GetTypeForAmount(slice.Amount);
        return polarity switch { StatusPurgePolarity.Positive => type == PowerType.Buff,
            StatusPurgePolarity.Negative => type == PowerType.Debuff,
            _ => type is PowerType.Buff or PowerType.Debuff };
    }
    internal static PowerShare[] Candidates(Creature creature, StatusPurgePolarity polarity) =>
        Current(creature, out var book) ? book.Powers.Where(s => s.Owned(polarity) > 0)
            .OrderBy(s => s.Canonical.Id.Category, StringComparer.Ordinal).ThenBy(s => s.Canonical.Id.Entry, StringComparer.Ordinal).ThenBy(s => s.Order).ToArray() : [];
    internal static async Task Clear(PowerShare share, StatusPurgePolarity polarity, StatusPurgeLayers layers)
    {
        long left = share.Canonical.StackType == PowerStackType.Counter
            ? Scp294StatusPurge.RemovedLayers(share.Owned(polarity), layers)
            : share.Slices.Where(s => Matches(share, s, polarity)).Sum(s => Math.Abs(s.Amount));
        long signed = 0;
        foreach (var slice in share.Slices.Where(s => Matches(share, s, polarity)).ToArray())
        {
            long n = Math.Min(left, Math.Abs(slice.Amount));
            signed += Math.Sign(slice.Amount) * n;
            ReduceSlice(slice, n, changeMarker: true); left -= n;
            if (left == 0) break;
        }
        share.Slices.RemoveAll(s => s.Amount == 0);
        await Adjust(share, -signed);
    }
    private static void AdjustNow(PowerShare share, long delta)
    {
        if (delta == 0 || !share.Owner.IsAlive) return;
        var power = Live(share);
        long next = Math.Clamp((long)(power?.Amount ?? 0) + delta, -999999999L, 999999999L);
        if (!share.Canonical.AllowNegative) next = Math.Max(0, next);
        using (Suspend())
        {
            if (power != null)
            {
                power.SetAmount((int)next);
                if (power.ShouldRemoveDueToAmount()) power.RemoveInternal();
            }
            else if (next != 0)
            {
                power = share.Canonical.ToMutable(); power.Applier = share.Applier;
                power.ApplyInternal(share.Owner, next); share.Power = power;
            }
        }
    }
    private static async Task Adjust(PowerShare share, long delta)
    {
        var power = Live(share);
        int old = power?.Amount ?? 0;
        AdjustNow(share, delta);
        if (power != null && !share.Owner.Powers.Contains(power) && old != 0)
            using (Suspend()) await power.AfterRemoved(share.Owner);
    }
    internal static bool IsTrackedTemporary(PowerModel power) => OwnerOf(power) is { } owner && Current(owner, out var book) &&
        book.Markers.Any(m => !m.Finished && m.TemporaryStat && ReferenceEquals(m.Marker, power));
    internal static void Removed(PowerModel power)
    {
        if (Suppressed.Value > 0 || OwnerOf(power) is not { } owner || !Current(owner, out var book)) return;
        var marker = book.Markers.FirstOrDefault(m => !m.Finished && ReferenceEquals(m.Marker, power));
        if (marker != null)
        {
            marker.Finished = true;
            long signed = 0;
            PowerShare? first = null;
            foreach (var share in book.Powers)
            {
                var slices = share.Slices.Where(s => ReferenceEquals(s.Marker, marker)).ToArray();
                if (slices.Length == 0) continue;
                first ??= share; signed += slices.Sum(s => s.Amount);
                foreach (var slice in slices) share.Slices.Remove(slice);
            }
            // Native temporary stat markers can contain both potion and non-potion grants.
            if (marker.TemporaryStat) signed += marker.Sign * Math.Max(0, power.Amount - marker.OwnedMarkerUnits);
            if (first == null && signed != 0)
            {
                var native = owner.GetPower(marker.NativeId) ?? ModelDb.GetById<PowerModel>(marker.NativeId).ToMutable();
                first = Share(owner, native);
            }
            if (first != null) AdjustNow(first, -signed);
            return;
        }
        var state = book.Powers.FirstOrDefault(s => ReferenceEquals(s.Power, power));
        if (state == null || power.AllowNegative && power.Amount == 0) return;
        foreach (var slice in state.Slices.ToArray()) ReduceSlice(slice, Math.Abs(slice.Amount), changeMarker: true);
        state.Slices.Clear();
    }
    internal static bool IsTrackedCharge(PowerModel power) => OwnerOf(power) is { } owner && Current(owner, out var book) &&
        book.Markers.Any(m => !m.Finished && !m.TemporaryStat && ReferenceEquals(m.Marker, power));
    internal static Task ExpireCharge(PowerModel marker)
    {
        // Removing a tracked marker settles only its still-owned underlying charges.
        return PowerCmd.Remove(marker);
    }
}

[HarmonyPatch(typeof(PowerModel), nameof(PowerModel.SetAmount))]
internal static class Scp294PotionSourceAmounts
{
    private static void Prefix(PowerModel __instance, out int __state) => __state = __instance.Amount;
    private static void Postfix(PowerModel __instance, int __state) => Scp294PotionSources.Changed(__instance, __state);
}
[HarmonyPatch(typeof(PowerModel), nameof(PowerModel.RemoveInternal))]
internal static class Scp294PotionSourceRemoval
{
    private static void Postfix(PowerModel __instance) => Scp294PotionSources.Removed(__instance);
}
[HarmonyPatch]
internal static class Scp294PotionTemporaryExpiry
{
    private static IEnumerable<System.Reflection.MethodBase> TargetMethods() =>
        new[] { AccessTools.Method(typeof(MegaCrit.Sts2.Core.Models.Powers.TemporaryStrengthPower), "AfterSideTurnEnd"),
            AccessTools.Method(typeof(MegaCrit.Sts2.Core.Models.Powers.TemporaryDexterityPower), "AfterSideTurnEnd") };
    private static bool Prefix(PowerModel __instance, IEnumerable<Creature> participants, ref Task __result)
    {
        if (!Scp294PotionSources.IsTrackedTemporary(__instance)) return true;
        __result = participants.Contains(__instance.Owner) ? PowerCmd.Remove(__instance) : Task.CompletedTask;
        return false;
    }
}

// Only explicitly augmented catalog potions enable these commands; ordinary native potions
// never enter this context. A command frame excludes unrelated powers changed by its hooks.
[HarmonyPatch(typeof(PowerCmd), nameof(PowerCmd.Apply), new[] { typeof(PlayerChoiceContext), typeof(PowerModel), typeof(Creature), typeof(decimal), typeof(Creature), typeof(CardModel), typeof(bool) })]
internal static class Scp294CatalogPowerApplication
{
    private static void Prefix(PowerModel power, Creature target, Creature? applier, CardModel? cardSource, out object? __state) =>
        __state = Scp294PotionSources.BeginCatalogCommand(power, target, applier, cardSource);
    private static void Postfix(ref Task __result, object? __state) => __result = Scp294PotionSources.FinishCatalogCommand(__result, __state);
    private static void Finalizer(Exception? __exception, object? __state) { if (__exception != null) Scp294PotionSources.AbandonCatalogCommand(__state); }
}
[HarmonyPatch(typeof(PowerCmd), nameof(PowerCmd.ModifyAmount))]
internal static class Scp294CatalogPowerStacking
{
    private static void Prefix(PowerModel power, Creature? applier, CardModel? cardSource, out object? __state) =>
        __state = Scp294PotionSources.BeginCatalogCommand(power, power.Owner, applier, cardSource);
    private static void Postfix(ref Task<int> __result, object? __state)
    {
        if (__state != null)
        {
            var original = __result;
            var completion = Scp294PotionSources.FinishCatalogCommand(original, __state);
            __result = Finish(original, completion);
        }
    }
    private static async Task<int> Finish(Task<int> original, Task completion)
    {
        await completion;
        return await original;
    }
    private static void Finalizer(Exception? __exception, object? __state) { if (__exception != null) Scp294PotionSources.AbandonCatalogCommand(__state); }
}

// Ritual is the native potion-granted status which creates another status later.
// Its child Strength keeps the same source split instead of becoming native Strength.
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Models.Powers.RitualPower), "AfterSideTurnEnd")]
internal static class Scp294PotionRitualGrowth
{
    private static void Prefix(PowerModel __instance, out object? __state) => __state = Scp294PotionSources.BeginRitual(__instance);
    private static void Postfix(ref Task __result, object? __state) => __result = Scp294PotionSources.FinishCatalogCommand(__result, __state);
    private static void Finalizer(Exception? __exception, object? __state) { if (__exception != null) Scp294PotionSources.AbandonCatalogCommand(__state); }
}
