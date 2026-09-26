using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rooms;
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
        RefreshBadge();
        Entry.Logger.Info($"[AshenFlask] Reset static state for new run: uses={s_currentUses}/{s_maxUses}");

        // 在确定性入口注册模型身份令牌，否则右键同步派发会因拿不到 token 而被静默拒绝
        var identity = RitsuLibFramework.EnsureModelIdentity(this);
        Entry.Logger.Info($"[AshenFlask] EnsureModelIdentity => {identity.Value}");
        if (Owner != null && !_potionSlotAdjusted)
        {
            await PlayerCmd.LoseMaxPotionCount(1, Owner);
            _potionSlotAdjusted = true;
            Entry.Logger.Info($"[AshenFlask] -1 potion slot. potionSlots after={Owner.MaxPotionCount}");
        }
    }

    // 遗物移除：+1 药水栏
    public override async Task AfterRemoved()
    {
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
        return new List<ExtraIconAmountLabelSpec>
        {
            new ExtraIconAmountLabelSpec(
                Text: $"{s_currentUses}/{s_maxUses}",
                Corner: ExtraIconAmountLabelCorner.BottomRight,
                CustomRect: new Rect2(),
                FontColor: Colors.White,
                FontOutlineColor: Colors.Black
            )
        };
    }
}
