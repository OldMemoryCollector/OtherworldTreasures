using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using OtherworldTreasures.Scripts.Balance;
using STS2RitsuLib;
using STS2RitsuLib.Interactions.RightClick;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Relics;

// 如果电话亭：右键重新开始本次战斗，以一个新的时间线（换掉决定抽牌的随机数）。
// 原版下"退出重进"卡牌顺序是一样的（随机数是确定性的），这个遗物会换一个新的种子，
// 所以每次重开拿到的手牌都不同。
// 实现：用原版"跳进指定房间"通道 EnterRoomDebug（控制台 fight 命令同款），
// 传入当前这场遭遇战的全新副本（怪物重新生成），而不是走存档载入通道。
[RegisterRelic(typeof(SharedRelicPool))]
public class WhatIfPhoneBooth : ModRelicTemplate, IDoraemonItem, IModRightClickableRelic,
    RelicRightClickSync.ICrossEndRightClickEffect
{
    private bool Balanced => RelicBalanceRules.WhatIfPhoneBooth(IsMutable ? Owner : null);
    private int MaxUsesPerCombat => Balanced ? 3 : 999;

    private sealed class PlayerState
    {
        public int UsesThisCombat;
        public int CombatStartHp = -1;
    }

    private PlayerState S => PerPlayerState<PlayerState>.Of(Owner);
    private WhatIfPhoneBooth Progress => Owner?.Relics.OfType<WhatIfPhoneBooth>().FirstOrDefault() ?? this;
    private string _balancedCombatKey = string.Empty;
    private int _balancedUses;
    private int _balancedStartHp = -1;

    [SavedProperty]
    public string SavedBalancedCombatKey
    {
        get => _balancedCombatKey;
        set { AssertMutable(); _balancedCombatKey = value ?? string.Empty; }
    }
    [SavedProperty]
    public int SavedBalancedUses
    {
        get => _balancedUses;
        set { AssertMutable(); _balancedUses = Math.Clamp(value, 0, 3); }
    }
    [SavedProperty]
    public int SavedBalancedStartHp
    {
        get => _balancedStartHp;
        set { AssertMutable(); _balancedStartHp = value; }
    }

    private int UsesThisCombat => !Balanced ? S.UsesThisCombat
        : Owner.RunState.CurrentRoom is CombatRoom room &&
          Progress.SavedBalancedCombatKey == RelicBalanceRules.CombatKey(Owner, room)
            ? Progress.SavedBalancedUses : 0;

    private void PrepareBalancedCombat(CombatRoom room)
    {
        string key = RelicBalanceRules.CombatKey(Owner, room);
        if (Progress.SavedBalancedCombatKey == key) return;
        Progress.SavedBalancedCombatKey = key;
        Progress.SavedBalancedUses = 0;
        Progress.SavedBalancedStartHp = Owner.Creature.CurrentHp;
    }

    // 哆啦A梦道具：自定义稀有度
    public override RelicRarity Rarity => ModRelicRarity.Doraemon;

    // 原版 MerchantCost 的 switch 遇到未知稀有度会抛异常，这里直接给值
    public override int MerchantCost => 999999999;

    protected override string IconBaseName => "what_if_phone_booth";

    protected override string BigIconPath => "res://OtherworldTreasures/images/relics/What_If_Phone_Booth.jpg";

    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://OtherworldTreasures/images/relics/What_If_Phone_Booth.jpg",
        IconOutlinePath: "res://OtherworldTreasures/images/relics/What_If_Phone_Booth.jpg",
        BigIconPath: "res://OtherworldTreasures/images/relics/What_If_Phone_Booth.jpg"
    );

    // 悬浮提示：补充"新的时间线"的含义
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => base.AdditionalHoverTips.Append(
        new HoverTip(
            new LocString("relics", "OTHERWORLD_TREASURES_RELIC_WHAT_IF_PHONE_BOOTH.tip.title"),
            new LocString("relics", "OTHERWORLD_TREASURES_RELIC_WHAT_IF_PHONE_BOOTH.tip.description")));

    public override async Task AfterObtained()
    {
        S.UsesThisCombat = 0;
        S.CombatStartHp = -1;
        Progress.SavedBalancedCombatKey = string.Empty;
        Progress.SavedBalancedUses = 0;
        Progress.SavedBalancedStartHp = -1;
        var identity = RitsuLibFramework.EnsureModelIdentity(this);
        Entry.Logger.Info($"[WhatIfPhoneBooth] AfterObtained, EnsureModelIdentity => {identity.Value}");
        await base.AfterObtained();
    }

    // 战斗开始：记录起始血量（重开战斗时会再次触发，用已用次数防止覆盖）
    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is CombatRoom) await RelicBalanceRules.WaitReady(Owner);
        if (Balanced && room is CombatRoom combatRoom) PrepareBalancedCombat(combatRoom);
        if (!Balanced && room is CombatRoom && S.UsesThisCombat == 0)
        {
            S.CombatStartHp = Owner?.Creature?.CurrentHp ?? -1;
            Entry.Logger.Info($"[WhatIfPhoneBooth] Combat start HP snapshot = {S.CombatStartHp}");
        }
        await base.AfterRoomEntered(room);
    }

    // 战斗结束：复位本场已用次数
    public override async Task AfterCombatEnd(CombatRoom room)
    {
        S.UsesThisCombat = 0;
        if (Balanced)
        {
            Progress.SavedBalancedCombatKey = string.Empty;
            Progress.SavedBalancedUses = 0;
            Progress.SavedBalancedStartHp = -1;
        }
        await base.AfterCombatEnd(room);
    }

    // 仅清理原模式的临时状态；平衡版次数保存在遗物字段中。
    internal static void ResetCombatScopedState()
    {
        PerPlayerState<PlayerState>.ClearAll();
    }

    public bool CanHandleRightClickLocal(ModRightClickContext context)
    {
        RitsuLibFramework.EnsureModelIdentity(this);
        return true;
    }

    // 仅战斗内、且本场未超上限时可点
    public bool CanExecuteRightClick(ModRightClickExecutionContext context)
    {
        return Owner != null
            && RelicBalanceRules.Ready(Owner)
            && UsesThisCombat < MaxUsesPerCombat
            && (!Balanced || CombatManager.Instance.IsInProgress)
            && CombatManager.Instance.DebugOnlyGetState() != null;
    }

    public Task OnRightClick(ModRightClickExecutionContext context)
    {
        if (Owner != null && Entry.IsLocalPlayer(Owner)) RelicRightClickSync.Request(Owner, this);
        return Task.CompletedTask;
    }

    async Task RelicRightClickSync.ICrossEndRightClickEffect.ApplyRightClickAsync(PlayerChoiceContext choiceContext)
    {
        await RelicBalanceRules.WaitReady(Owner);
        if (!CombatManager.Instance.IsInProgress) return;
        await RestartCombatWithNewTimeline();
    }

    private async Task RestartCombatWithNewTimeline()
    {
        if (UsesThisCombat >= MaxUsesPerCombat || CombatManager.Instance.DebugOnlyGetState() == null
            || (Balanced && !CombatManager.Instance.IsInProgress)) return;
        if (Owner?.RunState is not RunState runState)
        {
            return;
        }
        var runManager = RunManager.Instance;
        if (runManager == null)
        {
            return;
        }
        // 只有身处战斗房间时才谈得上"重开本场战斗"
        if (runState.CurrentRoom is not CombatRoom currentRoom)
        {
            Entry.Logger.Info("[WhatIfPhoneBooth] Not in a combat room, aborting");
            return;
        }
        if (runManager.ActionExecutor == null)
        {
            return;
        }

        if (Balanced)
        {
            PrepareBalancedCombat(currentRoom);
            if (Progress.SavedBalancedUses >= 3) return;
            Progress.SavedBalancedUses++;
        }
        else S.UsesThisCombat++;

        // 1) 换一个新的时间线：重播种影响洗牌/选牌的随机数
        // 改走联机一致的 RNG，避免两端分叉：种子从共享的 Shuffle 子流取，不再用进程本地随机数
        ulong seed = runState.Rng.Shuffle.NextUnsignedLong();
        runState.Rng.Shuffle.LoadFromSerializable(new Rng(seed).ToSerializable());
        runState.Rng.CombatCardSelection.LoadFromSerializable(new Rng(seed + 1).ToSerializable());

        // 2) 血量还原到战斗开始时（等同"退出重进"）
        // 注意：若战斗中途存档重进，本场开始的快照在进程重启后已丢失（static 重置），此时跳过回血，
        // 但"重开本场战斗"的主体效果不受影响。
        int startHp = Balanced ? Progress.SavedBalancedStartHp : S.CombatStartHp;
        if (Owner.Creature != null && startHp > 0)
        {
            await CreatureCmd.SetCurrentHp(Owner.Creature, Math.Min(startHp, Owner.Creature.MaxHp));
        }
        else
        {
            Entry.Logger.Info("[WhatIfPhoneBooth] 无本场开始血量快照（多为读档/重进），跳过回血");
        }

        // 3) 取当前这场遭遇战的全新副本 → 同一场战斗，怪物重新生成
        var encounter = ModelDb.GetById<EncounterModel>(currentRoom.Encounter.Id).ToMutable();
        // 保持同一遭遇的确定性生成；DebugRandomizeRng 使用各端的 UTC 时间播种，
        // 会在跨秒/时钟不同的客户端生成不同怪物，不能在正式联机流程里调用。

        Flash();
        Entry.Logger.Info(
            $"[WhatIfPhoneBooth] Restarting combat: encounter={encounter.Id.Entry}, roomType={currentRoom.RoomType}, " +
            $"seed={seed}, usesThisCombat={UsesThisCombat}");

        // 4) 走原版"跳进指定房间"通道重新进入这场战斗
        // 重进房间会拆掉手牌界面；如果跟当前动作队列在同一帧里进行，卡牌节点会被重复释放回对象池，
        // 日志表现为「NCard ... already been freed」（原版 owfight 通道不会，因为它不在动作队列内部）。
        // 先让出一帧，等 UI 树稳定再拆房间。
        if (NGame.Instance is { } game && game.GetTree() is { } tree)
        {
            await game.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }

        await runManager.EnterRoomDebug(currentRoom.RoomType, MapPointType.Unassigned, encounter);
        runManager.ActionExecutor.Unpause();

        Entry.Logger.Info(
            $"[WhatIfPhoneBooth] Restart finished: inCombat={CombatManager.Instance.IsInProgress}, " +
            $"currentRoom={runState.CurrentRoom?.GetType().Name}");
    }
}
