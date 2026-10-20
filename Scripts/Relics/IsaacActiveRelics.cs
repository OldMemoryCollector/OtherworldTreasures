using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.ValueProps;
using OtherworldTreasures.Scripts.Relics;
using STS2RitsuLib;
using STS2RitsuLib.Combat.Ui.ExtraCornerAmountLabels;
using STS2RitsuLib.Interactions.RightClick;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using MegaCrit.Sts2.Core.Entities.Gold;
using STS2RitsuLib.Combat.Rewards;
using OtherworldTreasures.Scripts.Rewards;
using OtherworldTreasures.Scripts.UI;

namespace OtherworldTreasures.Scripts.Relics;

public interface IActiveChargeRelic
{
    int CurrentCharge { get; }
    int MaxCharge { get; }
    void GainCharge(int amount = 1);
    void SetCharge(int amount);
}

/// <summary>Shared saved charge, native counter, and room-victory charging for Isaac active items.</summary>
public abstract class ActiveChargeRelicBase : ModRelicTemplate, IActiveChargeRelic,
    IRelicExtraIconAmountLabelSpecsProvider, IRelicExtraIconAmountLabelsChangeSource
{
    private int _savedCharge;

    [SavedProperty]
    public int SavedCharge
    {
        get => _savedCharge;
        set { AssertMutable(); _savedCharge = Math.Clamp(value, 0, MaxCharge); UpdateStatus(); RefreshBadge(); }
    }

    public abstract int MaxCharge { get; }
    public int CurrentCharge => LiveRelic.SavedCharge;
    protected ActiveChargeRelicBase LiveRelic => IsMutable
        ? Owner?.Relics.OfType<ActiveChargeRelicBase>().FirstOrDefault(r => r.Id == Id) ?? this
        : this;

    public event Action? RelicExtraIconAmountLabelsInvalidated;

    public override bool ShowCounter => false;

    public void GainCharge(int amount = 1)
    {
        if (amount <= 0 || Owner == null) return;
        var live = LiveRelic;
        live.SavedCharge = Math.Min(live.MaxCharge, live.SavedCharge + amount);
        live.UpdateStatus();
        live.RefreshBadge();
    }

    public void SetCharge(int amount)
    {
        var live = LiveRelic;
        live.SavedCharge = Math.Clamp(amount, 0, live.MaxCharge);
        live.UpdateStatus();
        live.RefreshBadge();
    }

    public bool SpendCharge(int amount)
    {
        var live = LiveRelic;
        if (amount <= 0 || live.SavedCharge < amount) return false;
        live.SavedCharge -= amount;
        live.UpdateStatus();
        live.RefreshBadge();
        return true;
    }

    public override async Task AfterObtained()
    {
        LiveRelic.SavedCharge = MaxCharge;
        LiveRelic.UpdateStatus();
        LiveRelic.RefreshBadge();
        await base.AfterObtained();
    }

    public override async Task AfterCombatVictory(CombatRoom room)
    {
        if (Owner != null && ReferenceEquals(LiveRelic, this)) GainCharge();
        await base.AfterCombatVictory(room);
    }

    public IReadOnlyList<ExtraIconAmountLabelSpec> GetRelicExtraIconAmountLabelSpecs()
    {
        return Array.Empty<ExtraIconAmountLabelSpec>();
    }

    protected void RefreshBadge()
    {
        var live = Owner?.Relics.OfType<ActiveChargeRelicBase>().FirstOrDefault(r => r.Id == Id);
        (live ?? this).RelicExtraIconAmountLabelsInvalidated?.Invoke();
        (live ?? this).InvokeDisplayAmountChanged();
    }

    private void UpdateStatus() => Status = SavedCharge >= MaxCharge ? RelicStatus.Active : RelicStatus.Normal;
}

[RegisterRelic(typeof(SharedRelicPool))]
public sealed class YumHeart : ActiveChargeRelicBase, IModRightClickableRelic,
    RelicRightClickSync.ICrossEndRightClickEffect
{
    public override int MaxCharge => 4;
    public override RelicRarity Rarity => RelicRarity.Rare;
    public override RelicAssetProfile AssetProfile => new(
        "res://OtherworldTreasures/images/relics/Yum_Heart.png",
        "res://OtherworldTreasures/images/relics/Yum_Heart.png",
        "res://OtherworldTreasures/images/relics/Yum_Heart.png");

    public bool CanHandleRightClickLocal(ModRightClickContext context)
    {
        RitsuLibFramework.EnsureModelIdentity(this);
        return true;
    }
    public bool CanExecuteRightClick(ModRightClickExecutionContext context) =>
        Owner != null && CurrentCharge >= MaxCharge && Owner.Creature.CurrentHp < Owner.Creature.MaxHp;
    public Task OnRightClick(ModRightClickExecutionContext context)
    {
        if (Owner != null && Entry.IsLocalPlayer(Owner)) RelicRightClickSync.Request(Owner, this);
        return Task.CompletedTask;
    }
    async Task RelicRightClickSync.ICrossEndRightClickEffect.ApplyRightClickAsync(PlayerChoiceContext choiceContext)
    {
        if (Owner == null || Owner.Creature.CurrentHp >= Owner.Creature.MaxHp || !SpendCharge(MaxCharge)) return;
        Flash();
        await CreatureCmd.Heal(Owner.Creature, 6m);
    }
}

[RegisterRelic(typeof(SharedRelicPool))]
public sealed class TheD6 : ActiveChargeRelicBase, IModRightClickableRelic,
    RelicRightClickSync.ICrossEndRightClickEffect
{
    public override int MaxCharge => 6;
    public override RelicRarity Rarity => RelicRarity.Rare;
    public override RelicAssetProfile AssetProfile => new(
        "res://OtherworldTreasures/images/relics/The_D6.png",
        "res://OtherworldTreasures/images/relics/The_D6.png",
        "res://OtherworldTreasures/images/relics/The_D6.png");

    public bool CanHandleRightClickLocal(ModRightClickContext context)
    {
        RitsuLibFramework.EnsureModelIdentity(this);
        return true;
    }
    public bool CanExecuteRightClick(ModRightClickExecutionContext context) => Owner != null && CurrentCharge >= MaxCharge;
    public Task OnRightClick(ModRightClickExecutionContext context)
    {
        if (Owner != null && Entry.IsLocalPlayer(Owner)) RelicRightClickSync.Request(Owner, this);
        return Task.CompletedTask;
    }
    async Task RelicRightClickSync.ICrossEndRightClickEffect.ApplyRightClickAsync(PlayerChoiceContext choiceContext)
    {
        // Full reroll behavior is applied by the reward/shop replacement hooks; do not consume charge
        // until there is an eligible unclaimed relic reward or stocked shop relic to reroll.
        if (Owner == null || CurrentCharge < MaxCharge) return;
        bool changed = IsaacRelicReroll.TryRerollCurrentReward(Owner) || IsaacRelicReroll.TryRerollShopRelic(Owner);
        if (changed) SpendCharge(MaxCharge);
        await Task.CompletedTask;
    }
}

internal static class IsaacRelicReroll
{
    private static readonly FieldInfo? RewardSetField = AccessTools.Field(typeof(NRewardsScreen), "_rewardsSet");
    private static readonly FieldInfo? RewardButtonsField = AccessTools.Field(typeof(NRewardsScreen), "_rewardButtons");
    private static readonly FieldInfo? RelicModelField = AccessTools.Field(typeof(RelicReward), "_relic");
    private static readonly MethodInfo? SetMerchantModelMethod = AccessTools.Method(typeof(MerchantRelicEntry), "SetModel");
    private static readonly MethodInfo? RewardButtonReloadMethod = AccessTools.Method(typeof(NRewardButton), "Reload");
    private static readonly FieldInfo? RewardButtonIconContainerField = AccessTools.Field(typeof(NRewardButton), "_iconContainer");

    internal static bool TryRerollCurrentReward(Player player)
    {
        if (player.RunState.CurrentRoom is TreasureRoom)
            return TryRerollTreasureRelics(player);
        var screen = FindNode<NRewardsScreen>();
        if (screen == null || RewardSetField?.GetValue(screen) is not RewardsSet rewardSet || rewardSet.Player != player)
            return false;
        if (rewardSet.Room is CombatRoom combatRoom && combatRoom.RoomType == RoomType.Elite) { }
        else return false;

        var eligible = rewardSet.Rewards.OfType<RelicReward>()
            .Where(r => !r.SuccessfullySelected && r.Relic != null).ToList();
        if (eligible.Count == 0) return false;

        bool changed = false;
        var offeredIds = eligible.Select(r => r.Relic!.Id).ToHashSet();
        foreach (var reward in eligible)
        {
            var previous = reward.Relic!;
            var replacement = PullReplacement(player, previous.Rarity, candidate => candidate.Id != previous.Id
                && !offeredIds.Contains(candidate.Id));
            if (replacement == null) continue;
            offeredIds.Remove(previous.Id);
            offeredIds.Add(replacement.Id);
            RelicModelField?.SetValue(reward, replacement);
            RefreshRewardButton(screen, reward);
            changed = true;
        }
        if (changed) Entry.Logger.Info($"[TheD6] Rerolled {eligible.Count} unclaimed reward relic(s) from {rewardSet.Room.RoomType}");
        return changed;
    }

    private static bool TryRerollTreasureRelics(Player player)
    {
        var synchronizer = RunManager.Instance?.TreasureRoomRelicSynchronizer;
        if (synchronizer?.CurrentRelics is not List<RelicModel> relics || relics.Count == 0) return false;
        if (Enumerable.Range(0, player.RunState.Players.Count).Any(i =>
                synchronizer.GetPlayerVote(player.RunState.Players[i]).voteReceived)) return false;

        var blockedIds = relics.Select(r => r.Id).ToHashSet();
        bool changed = false;
        for (int i = 0; i < relics.Count; i++)
        {
            var replacement = PullReplacement(player, relics[i].Rarity,
                candidate => !blockedIds.Contains(candidate.Id));
            if (replacement == null) continue;
            blockedIds.Remove(relics[i].Id);
            blockedIds.Add(replacement.Id);
            relics[i] = replacement;
            changed = true;
        }
        if (changed)
        {
            if (AccessTools.Field(typeof(TreasureRoomRelicSynchronizer), "VotesChanged")
                    ?.GetValue(synchronizer) is Action changedEvent)
                changedEvent();
            Entry.Logger.Info($"[TheD6] Rerolled {relics.Count} unclaimed treasure relic(s)");
        }
        return changed;
    }

    internal static bool TryRerollShopRelic(Player player)
    {
        if (player.RunState.CurrentRoom is not MerchantRoom room) return false;
        var inventory = room.Inventories.FirstOrDefault(i => i.Player == player);
        if (inventory == null) return false;
        var entries = inventory.RelicEntries.Where(e => e.IsStocked && e.Model != null && e.Model is not LilBattery).ToList();
        if (entries.Count == 0) return false;

        var blacklist = entries.Select(e => e.Model!.Id).ToHashSet();
        bool changed = false;
        foreach (var entry in entries)
        {
            var oldModel = entry.Model!;
            var replacement = PullReplacement(player, oldModel.Rarity, candidate => !blacklist.Contains(candidate.Id));
            if (replacement == null) continue;
            SetMerchantModelMethod?.Invoke(entry, new object[] { replacement });
            entry.OnMerchantInventoryUpdated();
            blacklist.Add(replacement.Id);
            changed = true;
        }
        if (changed) Entry.Logger.Info($"[TheD6] Rerolled unpurchased shop relics for player {player.NetId}");
        return changed;
    }

    private static RelicModel? PullReplacement(Player player, RelicRarity sourceRarity, Func<RelicModel, bool> extraFilter)
    {
        var allowedRarities = sourceRarity == RelicRarity.None
            ? new[] { RelicRarity.Common, RelicRarity.Uncommon, RelicRarity.Rare }
            : new[] { sourceRarity };
        var currentIds = player.Relics.Select(r => r.Id).ToHashSet();
        var candidates = ModelDb.RelicPool<SharedRelicPool>().GetUnlockedRelics(player.UnlockState)
            .Concat(player.Character.RelicPool.GetUnlockedRelics(player.UnlockState))
            .Where(r => allowedRarities.Contains(r.Rarity) && r.IsAllowedInShops && r.IsAllowed(player.RunState)
                && !currentIds.Contains(r.Id) && extraFilter(r))
            .GroupBy(r => r.Id).Select(g => g.First()).ToList();
        if (candidates.Count == 0) return null;
        var selected = candidates[player.PlayerRng.Rewards.NextInt(candidates.Count)];
        player.RelicGrabBag.Remove(selected);
        player.RunState.SharedRelicGrabBag.Remove(selected);
        return selected.ToMutable();
    }

    private static void RefreshRewardButton(NRewardsScreen screen, RelicReward reward)
    {
        if (RewardButtonsField?.GetValue(screen) is not List<Control> buttons) return;
        foreach (var button in buttons.OfType<NRewardButton>().Where(b => ReferenceEquals(b.Reward, reward)))
        {
            if (RewardButtonIconContainerField?.GetValue(button) is Control iconContainer)
                foreach (var child in iconContainer.GetChildren().ToArray()) child.QueueFree();
            RewardButtonReloadMethod?.Invoke(button, null);
        }
    }

    private static T? FindNode<T>() where T : Node
    {
        var root = NRun.Instance?.GetTree()?.Root;
        if (root == null) return null;
        return FindNodeRecursive<T>(root);
    }

    private static T? FindNodeRecursive<T>(Node node) where T : Node
    {
        if (node is T found) return found;
        foreach (var child in node.GetChildren())
            if (child is Node childNode && FindNodeRecursive<T>(childNode) is { } result) return result;
        return null;
    }
}

[RegisterRelic(typeof(SharedRelicPool))]
public sealed class JackSparrowCompass : ModRelicTemplate, IModRightClickableRelic,
    RelicRightClickSync.ICrossEndRightClickEffect
{
    private int _savedUsedAct = -1;
    private int _savedTargetAct = -1;
    private int _savedTargetCol = -1;
    private int _savedTargetRow = -1;
    private int _savedIssueCol = -1;
    private int _savedIssueRow = -1;
    private bool _savedFallback;
    private string _savedRelicId = string.Empty;
    // 2026-10-18：改为按次数使用——初始 1 次，每击杀一个 Boss +1。
    private int _savedUses = 1;

    [SavedProperty] public int SavedUsedAct { get => _savedUsedAct; set { AssertMutable(); _savedUsedAct = value; } }
    [SavedProperty] public int SavedTargetAct { get => _savedTargetAct; set { AssertMutable(); _savedTargetAct = value; } }
    [SavedProperty] public int SavedTargetCol { get => _savedTargetCol; set { AssertMutable(); _savedTargetCol = value; } }
    [SavedProperty] public int SavedTargetRow { get => _savedTargetRow; set { AssertMutable(); _savedTargetRow = value; } }
    [SavedProperty] public int SavedIssueCol { get => _savedIssueCol; set { AssertMutable(); _savedIssueCol = value; } }
    [SavedProperty] public int SavedIssueRow { get => _savedIssueRow; set { AssertMutable(); _savedIssueRow = value; } }
    [SavedProperty] public bool SavedFallback { get => _savedFallback; set { AssertMutable(); _savedFallback = value; } }
    [SavedProperty] public string SavedRelicId { get => _savedRelicId; set { AssertMutable(); _savedRelicId = value ?? string.Empty; } }
    [SavedProperty] public int SavedUses { get => _savedUses; set { AssertMutable(); _savedUses = Math.Max(0, value); } }

    private JackSparrowCompass LiveRelic => IsMutable
        ? Owner?.Relics.OfType<JackSparrowCompass>().FirstOrDefault() ?? this : this;
    public override RelicRarity Rarity => RelicRarity.Rare;
    public override RelicAssetProfile AssetProfile => new(
        "res://OtherworldTreasures/images/relics/Jack_Sparrow_Compass.png",
        "res://OtherworldTreasures/images/relics/Jack_Sparrow_Compass.png",
        "res://OtherworldTreasures/images/relics/Jack_Sparrow_Compass.png");

    public bool CanHandleRightClickLocal(ModRightClickContext context)
    {
        RitsuLibFramework.EnsureModelIdentity(this);
        return true;
    }
    public bool CanExecuteRightClick(ModRightClickExecutionContext context) => Owner != null
        && LiveRelic.SavedUses > 0 && string.IsNullOrEmpty(LiveRelic.SavedRelicId);
    public Task OnRightClick(ModRightClickExecutionContext context)
    {
        if (Owner != null && Entry.IsLocalPlayer(Owner)) RelicRightClickSync.Request(Owner, this);
        return Task.CompletedTask;
    }

    async Task RelicRightClickSync.ICrossEndRightClickEffect.ApplyRightClickAsync(PlayerChoiceContext choiceContext)
    {
        if (Owner == null || !CanUseNow()) return;
        var candidates = ModelDb.RelicPool<SharedRelicPool>().GetUnlockedRelics(Owner.UnlockState)
            .Concat(Owner.Character.RelicPool.GetUnlockedRelics(Owner.UnlockState))
            .Where(r => r.Rarity is RelicRarity.Common or RelicRarity.Uncommon or RelicRarity.Rare)
            .Where(r => r.IsAllowedInShops && r.IsAllowed(Owner.RunState)
                && (r.IsStackable || Owner.Relics.All(owned => owned.Id != r.Id)))
            .GroupBy(r => r.Id).Select(g => g.First())
            .OrderBy(r => r.Id.Entry, StringComparer.Ordinal)
            .ToList();
        if (candidates.Count == 0) return;
        var rng = Owner.PlayerRng.Rewards;
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = rng.NextInt(i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }
        var offers = candidates.Take(10).Select(r => r.ToMutable()).ToList();
        // The vanilla overlay stack hides every overlay while the map is open.
        // A Compass right-click commonly starts from the map, so close it while
        // the relic picker is visible and restore it after the choice completes.
        var mapScreen = NMapScreen.Instance;
        bool restoreMap = mapScreen?.IsOpen == true;
        if (restoreMap) mapScreen!.Close(animateOut: false);

        CompassRelicSelectionLayout.Active = true;
        RelicModel? selected;
        try { selected = await RelicSelectCmd.FromChooseARelicScreen(Owner, offers); }
        finally { CompassRelicSelectionLayout.Active = false; }
        if (restoreMap && GodotObject.IsInstanceValid(mapScreen))
            mapScreen!.Open(isOpenedFromTopBar: true);
        if (selected == null) return;

        var live = LiveRelic;
        var run = Owner.RunState;
        var current = run.CurrentMapCoord ?? new MapCoord(0, 0);
        var start = run.Map.GetPoint(current);
        var target = start == null ? null : FindGuaranteedNode(run.Map, start);
        live.SavedUses = Math.Max(0, live.SavedUses - 1);
        live.SavedTargetAct = run.CurrentActIndex;
        live.SavedIssueCol = current.col;
        live.SavedIssueRow = current.row;
        live.SavedFallback = target == null;
        live.SavedTargetCol = target?.coord.col ?? -1;
        live.SavedTargetRow = target?.coord.row ?? -1;
        live.SavedRelicId = selected.Id.Entry;
        Flash();
        CompassTargetMap.Refresh(run);
    }

    private bool CanUseNow() => Owner != null && LiveRelic.SavedUses > 0
        && string.IsNullOrEmpty(LiveRelic.SavedRelicId);

    public override async Task AfterCombatVictory(CombatRoom room)
    {
        if (Owner != null && room.RoomType == RoomType.Boss)
        {
            var live = LiveRelic;
            live.SavedUses++;
            live.Flash();
        }
        await base.AfterCombatVictory(room);
    }

    public override bool ShowCounter => SavedUses > 0;
    public override int DisplayAmount => LiveRelic.SavedUses;

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (Owner != null)
        {
            var live = LiveRelic;
            var run = Owner.RunState;
            if (live.SavedTargetAct >= 0 && run.CurrentActIndex > live.SavedTargetAct)
                live.ClearPending();
            else if (live.SavedTargetAct == run.CurrentActIndex && !string.IsNullOrEmpty(live.SavedRelicId))
            {
                var coord = run.CurrentMapCoord;
                bool reached = !live.SavedFallback && coord.HasValue
                    && coord.Value.col == live.SavedTargetCol && coord.Value.row == live.SavedTargetRow;
                if (!live.SavedFallback && !reached && coord.HasValue && coord.Value.row > live.SavedTargetRow)
                {
                    live.SavedFallback = true;
                    live.SavedIssueCol = coord.Value.col;
                    live.SavedIssueRow = coord.Value.row;
                }
                bool fallbackReached = live.SavedFallback && coord.HasValue
                    && (coord.Value.col != live.SavedIssueCol || coord.Value.row != live.SavedIssueRow)
                    && room.RoomType != RoomType.Boss;
                if (reached || fallbackReached)
                {
                    var canonical = ModelDb.AllRelics.FirstOrDefault(r => r.Id.Entry == live.SavedRelicId);
                    if (canonical != null) await RelicCmd.Obtain(canonical.ToMutable(), Owner);
                    live.ClearPending();
                }
            }
            CompassTargetMap.Refresh(run);
        }
        await base.AfterRoomEntered(room);
    }

    private void ClearPending()
    {
        SavedRelicId = string.Empty;
        SavedTargetAct = SavedTargetCol = SavedTargetRow = SavedIssueCol = SavedIssueRow = -1;
        SavedFallback = false;
        CompassTargetMap.Refresh(Owner?.RunState);
    }

    private static MapPoint? FindGuaranteedNode(ActMap map, MapPoint start)
    {
        // The last rest-site row is the final normal row before the boss.
        // Using the compass there should point directly at the boss node.
        if (start.PointType == MapPointType.RestSite && start.coord.row >= map.GetRowCount() - 1)
            return map.BossMapPoint;

        var bosses = new[] { map.BossMapPoint, map.SecondBossMapPoint }.Where(p => p != null).Cast<MapPoint>().ToArray();
        var candidates = map.GetAllMapPoints().Where(p => p.coord.row > start.coord.row)
            .OrderBy(p => p.coord.row).ThenBy(p => p.coord.col).ToList();
        int minimumTargetRow = start.coord.row + 5;
        var distantCandidates = candidates.Where(p => p.coord.row >= minimumTargetRow).ToList();
        foreach (var candidate in distantCandidates)
        {
            if (CanReachAnyBossWithout(start, bosses, candidate)) continue;
            return candidate;
        }
        // If no distant node is mandatory, still prefer one at least five rows
        // ahead. The existing fallback handles a path that bypasses that node.
        if (distantCandidates.Count > 0) return distantCandidates[0];
        // Near the end of an act, use the farthest remaining normal node rather
        // than placing the marker immediately above the current room.
        return candidates.LastOrDefault();
    }

    private static bool CanReachAnyBossWithout(MapPoint start, IReadOnlyCollection<MapPoint> bosses, MapPoint blocked)
    {
        var pending = new Stack<MapPoint>();
        var visited = new HashSet<MapPoint>();
        if (!ReferenceEquals(start, blocked)) pending.Push(start);
        while (pending.Count > 0)
        {
            var point = pending.Pop();
            if (!visited.Add(point)) continue;
            if (bosses.Contains(point)) return true;
            foreach (var child in point.Children)
                if (!ReferenceEquals(child, blocked)) pending.Push(child);
        }
        return false;
    }
}

[RegisterRelic(typeof(SharedRelicPool))]
public sealed class LilBattery : ModRelicTemplate, IModRightClickableRelic,
    RelicRightClickSync.ICrossEndRightClickEffect
{
    public override RelicRarity Rarity => RelicRarity.Shop;
    public override int MerchantCost => 75;
    public override bool IsAllowedInShops => false;
    public override RelicAssetProfile AssetProfile => new(
        "res://OtherworldTreasures/images/relics/Lil_Battery.png",
        "res://OtherworldTreasures/images/relics/Lil_Battery.png",
        "res://OtherworldTreasures/images/relics/Lil_Battery.png");

    public bool CanHandleRightClickLocal(ModRightClickContext context)
    {
        RitsuLibFramework.EnsureModelIdentity(this);
        return true;
    }
    public bool CanExecuteRightClick(ModRightClickExecutionContext context) => Owner != null
        && Owner.Relics.Any(r => r is IActiveChargeRelic);
    public Task OnRightClick(ModRightClickExecutionContext context)
    {
        if (Owner != null && Entry.IsLocalPlayer(Owner)) RelicRightClickSync.Request(Owner, this);
        return Task.CompletedTask;
    }
    async Task RelicRightClickSync.ICrossEndRightClickEffect.ApplyRightClickAsync(PlayerChoiceContext choiceContext)
    {
        if (Owner == null) return;
        var active = Owner.Relics.Where(r => r is IActiveChargeRelic).ToList();
        if (active.Count == 0) return;
        RelicModel chosen;
        if (active.Count == 1) chosen = active[0];
        else
        {
            var choices = active.Select(r => ModelDb.AllRelics.First(canonical => canonical.Id == r.Id).ToMutable()).ToList();
            var result = await RelicSelectCmd.FromChooseARelicScreen(Owner, choices);
            if (result == null) return;
            chosen = active.FirstOrDefault(r => r.Id == result.Id) ?? active[0];
        }
        if (chosen is IActiveChargeRelic activeRelic) activeRelic.SetCharge(activeRelic.MaxCharge);
        Flash();
        await RelicCmd.Remove(LiveBattery);
    }

    private LilBattery LiveBattery => IsMutable
        ? Owner?.Relics.OfType<LilBattery>().FirstOrDefault() ?? this : this;
}

public sealed class IsaacBatteryReward : ModCustomReward
{
    public IsaacBatteryReward(Player player) : base(player) { }
    public override RewardType ModRewardType => Entry.IsaacBatteryRewardType;
    protected override string? RewardIconPath => "res://OtherworldTreasures/images/relics/Lil_Battery.png";
    protected override string DescriptionLocKey => "OTHERWORLD_TREASURES_REWARD_LIL_BATTERY";
    public override void MarkContentAsSeen() { }

    protected override async Task<bool> OnSelect()
    {
        var active = Player.Relics.Where(r => r is IActiveChargeRelic).ToList();
        if (active.Count == 0) return false;
        RelicModel chosen;
        if (active.Count == 1) chosen = active[0];
        else
        {
            var choices = active.Select(r => ModelDb.AllRelics.First(canonical => canonical.Id == r.Id).ToMutable()).ToList();
            var result = await RelicSelectCmd.FromChooseARelicScreen(Player, choices);
            if (result == null) return false;
            chosen = active.FirstOrDefault(r => r.Id == result.Id) ?? active[0];
        }
        if (chosen is IActiveChargeRelic charge) charge.SetCharge(charge.MaxCharge);
        return true;
    }
}

[HarmonyPatch(typeof(RewardsCmd), nameof(RewardsCmd.GenerateForRoomEnd))]
internal static class IsaacBatteryRewardInjection
{
    private static void Prefix(Player player, AbstractRoom room)
    {
        if (room is not CombatRoom combat || !player.Relics.Any(r => r is IActiveChargeRelic)) return;
        if (combat.ExtraRewards.TryGetValue(player, out var existing) && existing.Any(r => r is IsaacBatteryReward)) return;
        if (player.PlayerRng.Rewards.NextFloat() < LifeCrystalRewardInjection.AppearChance)
            combat.AddExtraReward(player, new IsaacBatteryReward(player));
    }
}

[HarmonyPatch(typeof(MerchantInventory), nameof(MerchantInventory.CreateForNormalMerchant))]
internal static class IsaacBatteryShopOfferPatch
{
    private static readonly MethodInfo? SetModelMethod = AccessTools.Method(typeof(MerchantRelicEntry), "SetModel");

    private static void Postfix(Player player, MerchantInventory __result)
    {
        if (!player.Relics.Any(r => r is IActiveChargeRelic) || __result.RelicEntries.Count == 0) return;
        var entry = __result.RelicEntries.Last();
        var battery = ModelDb.Relic<LilBattery>().ToMutable();
        SetModelMethod?.Invoke(entry, new object[] { battery });
        entry.OnMerchantInventoryUpdated();
    }
}

[HarmonyPatch(typeof(MerchantRelicEntry), nameof(MerchantRelicEntry.CalcCost))]
internal static class IsaacBatteryShopPricePatch
{
    private static readonly FieldInfo? CostField = AccessTools.Field(typeof(MerchantEntry), "_cost");

    private static void Postfix(MerchantRelicEntry __instance)
    {
        if (__instance.Model is LilBattery) CostField?.SetValue(__instance, 75);
    }
}

[HarmonyPatch(typeof(MerchantEntry), nameof(MerchantEntry.OnTryPurchaseWrapper))]
internal static class IsaacBatteryRepeatPurchasePatch
{
    private static readonly MethodInfo? SetModelMethod = AccessTools.Method(typeof(MerchantRelicEntry), "SetModel");

    private static void Prefix(MerchantEntry __instance, out bool __state) =>
        __state = __instance is MerchantRelicEntry { Model: LilBattery };

    private static void Postfix(MerchantEntry __instance, MerchantInventory? inventory, bool __state, ref Task<bool> __result)
    {
        if (!__state || __instance is not MerchantRelicEntry entry || inventory == null) return;
        __result = KeepBatteryInStock(entry, inventory, __result);
    }

    private static async Task<bool> KeepBatteryInStock(MerchantRelicEntry entry, MerchantInventory inventory, Task<bool> original)
    {
        var purchased = await original;
        if (purchased && inventory.Player.Relics.Any(r => r is IActiveChargeRelic))
        {
            SetModelMethod?.Invoke(entry, new object[] { ModelDb.Relic<LilBattery>().ToMutable() });
            entry.OnMerchantInventoryUpdated();
        }
        return purchased;
    }
}


internal static class CompassTargetMap
{
    private const string BossMarkerName = "CompassBossTargetMarker";
    private static readonly FieldInfo? RunStateField = AccessTools.Field(typeof(NMapPoint), "_runState");
    private static readonly FieldInfo? PointDictField = AccessTools.Field(typeof(NMapScreen), "_mapPointDictionary");

    internal static bool IsTarget(IRunState run, MapCoord coord) => run.Players.Any(player => player.Relics
        .OfType<JackSparrowCompass>().Any(c => c.SavedTargetAct == run.CurrentActIndex
            && !string.IsNullOrEmpty(c.SavedRelicId) && !c.SavedFallback
            && c.SavedTargetCol == coord.col && c.SavedTargetRow == coord.row));

    internal static void Refresh(IRunState? run)
    {
        if (run == null || NMapScreen.Instance == null || PointDictField?.GetValue(NMapScreen.Instance) is not Dictionary<MapCoord, NMapPoint> points) return;
        var targets = run.Players.SelectMany(player => player.Relics.OfType<JackSparrowCompass>())
            .Where(compass => compass.SavedTargetAct == run.CurrentActIndex
                && !string.IsNullOrEmpty(compass.SavedRelicId) && !compass.SavedFallback)
            .ToList();
        foreach (var pair in points)
        {
            var point = pair.Value.Point;
            var targetCompasses = targets.Where(compass => compass.SavedTargetCol == pair.Key.col
                && compass.SavedTargetRow == pair.Key.row).ToList();

            // Match the vanilla SpoilsMap/FurCoat pattern: mark the MapPoint with
            // AddQuest so NNormalMapPoint displays its built-in quest indicator.
            foreach (var oldMark in point.Quests.OfType<JackSparrowCompass>().ToArray())
                if (!targetCompasses.Contains(oldMark)) point.RemoveQuest(oldMark);
            foreach (var compass in targetCompasses)
                if (!point.Quests.Contains(compass)) point.AddQuest(compass);

            // The vanilla quest badge exists on normal points; boss points have
            // no quest-icon slot, so use the supplied compass X on the boss node.
            if (pair.Value is NBossMapPoint boss)
            {
                var marker = boss.GetNodeOrNull<TextureRect>(BossMarkerName);
                if (targetCompasses.Count > 0 && marker == null) AddBossMarker(boss);
                else if (targetCompasses.Count == 0 && marker != null) marker.QueueFree();
            }
        }
    }

    private static void AddBossMarker(NBossMapPoint node)
    {
        const string markerPath = "res://OtherworldTreasures/images/ui/compass_target_x.png";
        if (!ResourceLoader.Exists(markerPath)) return;
        const float size = 18f;
        var marker = new TextureRect
        {
            Name = BossMarkerName,
            Texture = ResourceLoader.Load<Texture2D>(markerPath),
            Size = Vector2.One * size,
            Position = new Vector2((node.Size.X - size) * 0.5f, -size * 0.65f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 0,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        node.AddChild(marker);
    }
}

[HarmonyPatch(typeof(NNormalMapPoint), "_Ready")]
internal static class CompassMapMarkerReady
{
    private static void Postfix(NNormalMapPoint __instance)
    {
        if (AccessTools.Field(typeof(NMapPoint), "_runState")?.GetValue(__instance) is IRunState run)
            CompassTargetMap.Refresh(run);
    }
}

[HarmonyPatch(typeof(NBossMapPoint), "_Ready")]
internal static class CompassBossMapMarkerReady
{
    private static void Postfix(NBossMapPoint __instance)
    {
        if (AccessTools.Field(typeof(NMapPoint), "_runState")?.GetValue(__instance) is IRunState run)
            CompassTargetMap.Refresh(run);
    }
}

[HarmonyPatch(typeof(RunManager), "ExitCurrentRoom")]
internal static class ActiveRelicRoomChargePatch
{
    private static void Postfix(ref Task<AbstractRoom?> __result) => __result = GrantAfterExit(__result);

    private static async Task<AbstractRoom?> GrantAfterExit(Task<AbstractRoom?> original)
    {
        var room = await original;
        if (room is MerchantRoom or EventRoom or RestSiteRoom)
        {
            var run = RunManager.Instance?.DebugOnlyGetState();
            if (run != null)
                foreach (var player in run.Players)
                    foreach (var relic in player.Relics.OfType<IActiveChargeRelic>())
                        relic.GainCharge();
        }
        return room;
    }
}

// Treasure rooms grant charge only after the chest rewards are actually opened.
[HarmonyPatch(typeof(OneOffSynchronizer), "DoTreasureRoomRewards")]
internal static class ActiveRelicTreasureChargePatch
{
    private static void Postfix(Player player, ref Task<int> __result) => __result = GrantAfterOpening(player, __result);

    private static async Task<int> GrantAfterOpening(Player player, Task<int> original)
    {
        var gold = await original;
        foreach (var relic in player.Relics.OfType<IActiveChargeRelic>())
            relic.GainCharge();
        return gold;
    }
}
