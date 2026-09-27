using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib;
using STS2RitsuLib.Combat.Ui.ExtraCornerAmountLabels;
using STS2RitsuLib.Interactions.RightClick;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using OtherworldTreasures.Scripts.RestSite;

namespace OtherworldTreasures.Scripts.Relics;

// 原素瓶（灰烬瓶）：模拟 Dark Souls 的原素瓶机制（遗物形态，模拟药水效果）
// - 获得时 -1 药水栏（占用一个药水位）
// - 右击遗物使用：回复 15% 最大生命值，消耗 1 次使用次数
// - 战斗内/外均可使用
// - 不消失、次数不自动补充
// - 篝火处注火：次数恢复满 + 永久上限 +1
// - 击败精英/Boss：50% 几率 +1 次
[RegisterRelic(typeof(SharedRelicPool))]
public class AshenFlask : ModRelicTemplate, IModRightClickableRelic, ICombatCompactRelic,
    IRelicExtraIconAmountLabelSpecsProvider, IRelicExtraIconAmountLabelsChangeSource, ITimeClothCounter
{
    // === 跨战斗实例持久化（遗物每次战斗有新 runtime 实例） ===
    private static int s_currentUses = 2;
    private static int s_maxUses = 2;

    // 角标字号：RitsuLib 的角标字号继承自原版数量标签（很大），必须用 RichText 指定字号，
    // 与库拉的骰子保持一致
    private const int BadgeFontSize = 16;

    public int CurrentUses => s_currentUses;
    public int MaxUses => s_maxUses;

    // 时光布：可回溯/推进的计数 = 当前使用次数
    public int TimeClothCounter
    {
        get => s_currentUses;
        set => s_currentUses = value;
    }

    public void RefreshTimeClothCounterUi()
    {
        RefreshBadge();
    }

    // 角标刷新事件（RitsuLib 监听）
    public event Action RelicExtraIconAmountLabelsInvalidated;

    private bool _potionSlotAdjusted;

    // 药水栏扣减的"延后"标记：药水栏满时原版减槽会越界（见 TryLosePotionSlot 注释），
    // 所以先记账，等玩家用掉/丢掉一个药水腾出空位后再扣。
    // static 供战斗中的克隆实例共享，SavedProperty 让它跨存档/重启保留（同孢子囊的做法）。
    private static bool s_pendingSlotLoss;
    private bool _pendingSlotLoss;

    [SavedProperty]
    public bool PendingSlotLoss
    {
        get => _pendingSlotLoss;
        set
        {
            AssertMutable();
            _pendingSlotLoss = value;
        }
    }

    private AshenFlask? LiveRelic => Owner?.Relics?.OfType<AshenFlask>().FirstOrDefault();

    // 存档权威：读档后用存档值校正 static（进程重启后 static 会回到默认值）
    private void SyncFromSave()
    {
        var live = LiveRelic ?? this;
        if (live._pendingSlotLoss != s_pendingSlotLoss)
        {
            s_pendingSlotLoss = live._pendingSlotLoss;
            Entry.Logger.Info($"[AshenFlask] 从存档同步待扣药水栏：{s_pendingSlotLoss}");
        }
    }

    // 把 static 写回玩家手里那件（可变）遗物，随存档保存
    private void Persist()
    {
        var live = LiveRelic;
        if (live != null && !ReferenceEquals(live, this))
        {
            if (live.IsMutable)
            {
                live.PendingSlotLoss = s_pendingSlotLoss;
            }
        }
        else if (IsMutable)
        {
            PendingSlotLoss = s_pendingSlotLoss;
        }
    }

    protected override string IconBaseName => "ashen_flask";

    protected override string BigIconPath => "res://OtherworldTreasures/images/relics/AshenFlask.webp";

    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://OtherworldTreasures/images/relics/AshenFlask.webp",
        IconOutlinePath: "res://OtherworldTreasures/images/relics/AshenFlask.webp"
    );

    public override RelicRarity Rarity => RelicRarity.Ancient;

    // 通知角标刷新
    private void RefreshBadge()
    {
        RelicExtraIconAmountLabelsInvalidated?.Invoke();
    }

    // 悬浮提示：补充篝火「注火」选项的说明
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => base.AdditionalHoverTips.Append(
        new HoverTip(
            new LocString("rest_site_ui", "OPTION_ASHEN_FLASK_KINDLE.name"),
            new LocString("rest_site_ui", "OPTION_ASHEN_FLASK_KINDLE.description")));

    // 遗物获得：-1 药水栏（使用原版 PlayerCmd API）
    public override async Task AfterObtained()
    {
        Entry.Logger.Info($"[AshenFlask] AfterObtained called. Owner={(Owner == null ? "NULL" : "ok")}, potionSlots before={Owner?.MaxPotionCount}");

        // 重置跨 run 残留的 static 状态（s_currentUses/s_maxUses 在进程内全局存活，
        // 新档获得时若不重置会继承旧档的注火次数/上限，导致"新开档就是满强化瓶"）
        s_currentUses = 2;
        s_maxUses = 2;
        s_pendingSlotLoss = false;
        PendingSlotLoss = false;
        RefreshBadge();
        Entry.Logger.Info($"[AshenFlask] Reset static state for new run: uses={s_currentUses}/{s_maxUses}");

        // 在确定性入口注册模型身份令牌，否则右键同步派发会因拿不到 token 而被静默拒绝
        var identity = RitsuLibFramework.EnsureModelIdentity(this);
        Entry.Logger.Info($"[AshenFlask] EnsureModelIdentity => {identity.Value}");
        if (Owner != null && !_potionSlotAdjusted)
        {
            await TryLosePotionSlot();
        }
    }

    // -1 药水栏。
    // 原版 SetMaxPotionCountInternal 在"药水栏已满"时有个越界 bug：它用
    // _potionSlots.IndexOf(null) 找空位，全满时返回 -1，却仍然执行 _potionSlots[-1] = ...，
    // 直接抛异常（表现为获得原素瓶时卡住）。所以这里先看有没有空槽：没有就先记账。
    private async Task TryLosePotionSlot()
    {
        var player = Owner;
        if (player == null || _potionSlotAdjusted)
        {
            return;
        }
        if (player.Potions.Count() >= player.MaxPotionCount)
        {
            s_pendingSlotLoss = true;
            Persist();
            Entry.Logger.Info("[AshenFlask] 药水栏已满，延后扣除药水栏（等玩家用掉一个药水）");
            return;
        }
        await PlayerCmd.LoseMaxPotionCount(1, player);
        _potionSlotAdjusted = true;
        s_pendingSlotLoss = false;
        Persist();
        Entry.Logger.Info($"[AshenFlask] -1 potion slot. potionSlots after={player.MaxPotionCount}");
    }

    // 药水栏腾出空位后补扣（用掉/丢掉药水、以及每回合开始时都会走一遍）
    private async Task FlushPendingSlotLoss()
    {
        SyncFromSave();
        if (!s_pendingSlotLoss || _potionSlotAdjusted)
        {
            return;
        }
        await TryLosePotionSlot();
    }

    public override async Task AfterPotionUsed(PotionModel potion, Creature? target)
    {
        await base.AfterPotionUsed(potion, target);
        await FlushPendingSlotLoss();
    }

    public override async Task AfterPotionDiscarded(PotionModel potion)
    {
        await base.AfterPotionDiscarded(potion);
        await FlushPendingSlotLoss();
    }

    // 兜底：上面两个钩子对遗物的触发条件有限（原版说明里"战斗外才给遗物"），
    // 每回合开始再检查一次，确保空位出现后一定能补扣
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        await FlushPendingSlotLoss();
        await base.AfterPlayerTurnStart(choiceContext, player);
    }

    // 遗物移除：+1 药水栏
    public override async Task AfterRemoved()
    {
        // 还欠着的扣减也一并取消
        s_pendingSlotLoss = false;
        Persist();
        if (Owner != null && _potionSlotAdjusted)
        {
            await PlayerCmd.GainMaxPotionCount(1, Owner);
            _potionSlotAdjusted = false;
            Entry.Logger.Info($"[AshenFlask] Removed! +1 potion slot. potionSlots={Owner.MaxPotionCount}");
        }
    }

    // 右键预检：这里只做"确保身份令牌已注册"这一稳定事实，固定返回 true
    // （是否真的可用由 CanExecuteRightClick 按剩余次数判断——那是可变游戏状态，不应放在本地预检）
    // 不注册令牌会导致 RitsuLib 同步派发拿不到 token，右键被静默拒绝（表现为完全无法使用）
    public bool CanHandleRightClickLocal(ModRightClickContext context)
    {
        var identity = RitsuLibFramework.EnsureModelIdentity(this);
        Entry.Logger.Info($"[AshenFlask] CanHandleRightClickLocal: EnsureModelIdentity => {identity.Value}");
        return true;
    }

    // 右击遗物使用 → 回血 15% 最大生命
    public bool CanExecuteRightClick(ModRightClickExecutionContext context)
    {
        bool can = s_currentUses > 0;
        Entry.Logger.Info($"[AshenFlask] CanExecuteRightClick => {can} (uses={s_currentUses}/{s_maxUses}, Owner={(Owner == null ? "NULL" : "ok")}, Creature={(Owner?.Creature == null ? "NULL" : "ok")})");
        return can;
    }

    public async Task OnRightClick(ModRightClickExecutionContext context)
    {
        Entry.Logger.Info($"[AshenFlask] OnRightClick fired! uses={s_currentUses}/{s_maxUses}");
        await UseFlask();
    }

    private async Task UseFlask()
    {
        if (s_currentUses <= 0)
        {
            Entry.Logger.Info("[AshenFlask] UseFlask aborted: no uses left");
            return;
        }
        if (Owner?.Creature == null)
        {
            Entry.Logger.Info($"[AshenFlask] UseFlask aborted: Owner/Creature null (Owner={Owner != null})");
            return;
        }

        int healAmount = (int)(Owner.Creature.MaxHp * 0.15m);
        if (healAmount <= 0) healAmount = 1;

        // 遗物闪光（顶栏图标闪光 + relic_activate_general 音效）
        Flash();
        // 原版治疗：自带治疗音效 + 十字治疗VFX
        await CreatureCmd.Heal(Owner.Creature, healAmount);
        s_currentUses--;
        RefreshBadge();
        Entry.Logger.Info($"[AshenFlask] Healed {healAmount} HP. Remaining: {s_currentUses}/{s_maxUses}");
    }

    // 篝火注火：注入自定义 RestSite 选项
    public override bool TryModifyRestSiteOptions(Player player, ICollection<RestSiteOption> options)
    {
        foreach (var opt in options)
        {
            if (opt.OptionId == "ASHEN_FLASK_KINDLE")
                return false;
        }
        options.Add(new AshenFlaskKindleOption(player, this));
        return true;
    }

    // 注火：次数满 + 上限 +1
    public void Kindle()
    {
        s_maxUses++;
        s_currentUses = s_maxUses;
        Flash();
        RefreshBadge();
        Entry.Logger.Info($"[AshenFlask] Kindled! New max={s_maxUses}, current={s_currentUses}");
    }

    // 击败精英/Boss：50% 几率 +1 次
    public override async Task AfterCombatEnd(CombatRoom room)
    {
        bool isEliteOrBoss = room.RoomType == RoomType.Elite || room.RoomType == RoomType.Boss;
        if (isEliteOrBoss && s_currentUses < s_maxUses && Owner != null)
        {
            if (Owner.RunState.Rng.Shuffle.NextInt(2) == 0)
            {
                s_currentUses++;
                RefreshBadge();
                Entry.Logger.Info($"[AshenFlask] Elite/Boss bonus! Uses={s_currentUses}/{s_maxUses}");
            }
        }
        await Task.CompletedTask;
    }

    // 遗物角标：显示 CurrentUses / MaxUses
    public IReadOnlyList<ExtraIconAmountLabelSpec> GetRelicExtraIconAmountLabelSpecs()
    {
        // 局外（图鉴/收藏）不显示角标，避免把上一局的次数带进图鉴
        if (!(RunManager.Instance?.IsInProgress ?? false))
        {
            return new List<ExtraIconAmountLabelSpec>();
        }
        return new List<ExtraIconAmountLabelSpec>
        {
            ExtraIconAmountLabelSpec.RichText(
                ExtraIconAmountLabelCorner.BottomRight,
                $"[font_size={BadgeFontSize}]{s_currentUses}/{s_maxUses}[/font_size]")
        };
    }
}
