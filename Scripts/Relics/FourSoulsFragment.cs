using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using OtherworldTreasures.Scripts.FourSouls;
using OtherworldTreasures.Scripts.Powers;
using STS2RitsuLib;
using STS2RitsuLib.Combat.Ui.ExtraCornerAmountLabels;
using STS2RitsuLib.Interactions.RightClick;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Relics;

/// <summary>
/// 四魂之玉碎片：四魂之玉效果的主体。
/// - 右键开关：开启时玩家获得四魂强化、地图上出现持有四魂的怪物；战斗中不可切换
/// - 四个角标分别显示四种魂的持有数量，各用对应颜色（荒红/和蓝/幸绿/奇紫）
/// - 击败持有四魂的敌人后，其四魂全部归玩家
/// 由【四魂之玉】获取时随机破碎而来（见 FourSoulsGem）。
/// </summary>
[RegisterRelic(typeof(SharedRelicPool))]
public class FourSoulsFragment : ModRelicTemplate, IModRightClickableRelic,
    IRelicExtraIconAmountLabelSpecsProvider, IRelicExtraIconAmountLabelsChangeSource
{
    // 角标字号：RitsuLib 角标字号继承自原版数量标签（很大），必须用 RichText 指定
    private const int BadgeFontSize = 14;

    public override RelicRarity Rarity => RelicRarity.Ancient;

    // 美术：256×256（根目录投放的源图已移入打包目录并清理）
    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://OtherworldTreasures/images/relics/Four_Souls_Fragment.png",
        IconOutlinePath: "res://OtherworldTreasures/images/relics/Four_Souls_Fragment.png",
        BigIconPath: "res://OtherworldTreasures/images/relics/Four_Souls_Fragment.png");

    // === 持久化状态 ===

    private int _wasteland;
    private int _harmony;
    private int _fortune;
    private int _wonder;
    private bool _enabled;
    private int _lockedActIndex = -1;
    private int _lockedFragmentCount;

    [SavedProperty]
    public int WastelandFragments
    {
        get => _wasteland;
        set { AssertMutable(); _wasteland = value; }
    }

    [SavedProperty]
    public int HarmonyFragments
    {
        get => _harmony;
        set { AssertMutable(); _harmony = value; }
    }

    [SavedProperty]
    public int FortuneFragments
    {
        get => _fortune;
        set { AssertMutable(); _fortune = value; }
    }

    [SavedProperty]
    public int WonderFragments
    {
        get => _wonder;
        set { AssertMutable(); _wonder = value; }
    }

    /// <summary>四魂之玉是否开启。</summary>
    [SavedProperty]
    public bool Enabled
    {
        get => _enabled;
        set { AssertMutable(); _enabled = value; }
    }

    // 玩家身上真正的那件遗物（右键派发的可能是克隆实例，直接读写 this 不可靠）
    private FourSoulsFragment? LiveRelic => Owner?.Relics.OfType<FourSoulsFragment>().FirstOrDefault();

    /// <summary>当前层档位锁定的层号（-1 表示尚未锁定）。</summary>
    [SavedProperty]
    public int LockedActIndex
    {
        get => _lockedActIndex;
        set { AssertMutable(); _lockedActIndex = value; }
    }

    /// <summary>锁定该层档位时玩家持有的碎片总数。</summary>
    [SavedProperty]
    public int LockedFragmentCount
    {
        get => _lockedFragmentCount;
        set { AssertMutable(); _lockedFragmentCount = value; }
    }

    // === 碎片读写 ===

    /// <summary>四种魂的持有数量（下标 = SoulKind）。</summary>
    public int[] Fragments => new[] { _wasteland, _harmony, _fortune, _wonder };

    public int CountOf(SoulKind kind) => kind switch
    {
        SoulKind.Wasteland => _wasteland,
        SoulKind.Harmony => _harmony,
        SoulKind.Fortune => _fortune,
        _ => _wonder,
    };

    public int TotalFragments => _wasteland + _harmony + _fortune + _wonder;

    /// <summary>增加某一种碎片。</summary>
    internal void AddFragments(SoulKind kind, int amount)
    {
        if (amount <= 0 || !IsMutable)
        {
            return;
        }
        switch (kind)
        {
            case SoulKind.Wasteland: WastelandFragments = _wasteland + amount; break;
            case SoulKind.Harmony: HarmonyFragments = _harmony + amount; break;
            case SoulKind.Fortune: FortuneFragments = _fortune + amount; break;
            case SoulKind.Wonder: WonderFragments = _wonder + amount; break;
        }
    }

    /// <summary>
    /// 取该层的档位锁定值：该层第一次读取时用当前碎片总数锁定，之后不再变化。
    /// 这样同一层内玩家继续收集碎片不会改变已 roll 出的四魂。
    /// </summary>
    internal int GetActLock(int actIndex)
    {
        if (_lockedActIndex == actIndex)
        {
            return _lockedFragmentCount;
        }
        int count = TotalFragments;
        if (IsMutable)
        {
            LockedActIndex = actIndex;
            LockedFragmentCount = count;
        }
        else
        {
            _lockedActIndex = actIndex;
            _lockedFragmentCount = count;
        }
        return count;
    }

    // === 角标 ===

    public event Action? RelicExtraIconAmountLabelsInvalidated;

    internal void RefreshBadge() => RelicExtraIconAmountLabelsInvalidated?.Invoke();

    public IReadOnlyList<ExtraIconAmountLabelSpec> GetRelicExtraIconAmountLabelSpecs()
    {
        // 局外（图鉴/收藏）不显示角标，避免把上一局的数量带进图鉴
        if (!(RunManager.Instance?.IsInProgress ?? false))
        {
            return new List<ExtraIconAmountLabelSpec>();
        }
        return new List<ExtraIconAmountLabelSpec>
        {
            Badge(ExtraIconAmountLabelCorner.TopLeft, SoulKind.Wasteland),
            Badge(ExtraIconAmountLabelCorner.TopRight, SoulKind.Harmony),
            Badge(ExtraIconAmountLabelCorner.BottomLeft, SoulKind.Fortune),
            Badge(ExtraIconAmountLabelCorner.BottomRight, SoulKind.Wonder),
        };
    }

    private ExtraIconAmountLabelSpec Badge(ExtraIconAmountLabelCorner corner, SoulKind kind)
    {
        string hex = Souls.ColorOf(kind).ToHtml(false);
        return ExtraIconAmountLabelSpec.RichText(
            corner,
            $"[font_size={BadgeFontSize}][color=#{hex}]{CountOf(kind)}[/color][/font_size]");
    }

    // === 获取 ===

    /// <summary>由【四魂之玉】破碎时调用：随机得一种碎片并开启。</summary>
    internal void InitializeAsNew(Rng rng)
    {
        if (!IsMutable)
        {
            return;
        }
        WastelandFragments = 0;
        HarmonyFragments = 0;
        FortuneFragments = 0;
        WonderFragments = 0;
        var kind = Souls.All[rng.NextInt(Souls.Count)];
        AddFragments(kind, 1);
        Enabled = true;
        LockedActIndex = -1;
        LockedFragmentCount = 0;
        RefreshBadge();
        Entry.Logger.Info($"[FourSouls] 碎片初始化：{kind}");
    }

    public override async Task AfterObtained()
    {
        // 正常由【四魂之玉】调用 InitializeAsNew；兜底在这里初始化（避免重复初始化）
        if (TotalFragments == 0 && Owner != null)
        {
            InitializeAsNew(Owner.RunState.Rng.Shuffle);
        }
        Status = Enabled ? RelicStatus.Active : RelicStatus.Disabled;
        RefreshBadge();
        Entry.Logger.Info($"[FourSouls] 碎片获得：荒{_wasteland} 和{_harmony} 幸{_fortune} 奇{_wonder}，开启={Enabled}");
        await base.AfterObtained();
    }

    // === 右键开关 ===

    public bool CanHandleRightClickLocal(ModRightClickContext context)
    {
        var identity = RitsuLibFramework.EnsureModelIdentity(this);
        Entry.Logger.Info($"[FourSouls] 右键预检：身份={identity.Value}");
        return true;
    }

    // 开关只能在战斗外进行。
    // 注意：不能用 PlayerCombatState == null 判断——它一旦首次进入战斗就会被 ResetCombatState
    // 赋成新实例，之后整局都不会再变回 null（Player.PlayerCombatState 只有 private set），
    // 用它判断会让右键在第一次战斗之后永远被静默拒绝。战斗是否进行中以 CombatManager 为准。
    public bool CanExecuteRightClick(ModRightClickExecutionContext context)
    {
        var owner = Owner;
        bool inCombat = CombatManager.Instance.IsInProgress;
        bool can = owner != null && !inCombat;
        Entry.Logger.Info($"[FourSouls] 右键可执行={can}（Owner={(owner == null ? "NULL" : "ok")}，战斗中={inCombat}）");
        return can;
    }

    public async Task OnRightClick(ModRightClickExecutionContext context)
    {
        // 右键派发到的可能是克隆实例，必须写回玩家手上那件真遗物（与任意门/原素瓶同做法），
        // 否则 Enabled 改在克隆上、真遗物没变，表现为"右键没反应、图标也不变"。
        var live = LiveRelic ?? this;
        bool next = !live.Enabled;
        if (live.IsMutable)
        {
            live.Enabled = next;
        }
        else
        {
            _enabled = next;
        }
        // 关闭时把遗物图标变灰（Disabled），开启时恢复"已启用"表现
        live.Status = next ? RelicStatus.Active : RelicStatus.Disabled;
        live.Flash();
        live.RefreshBadge();
        Entry.Logger.Info(
            $"[FourSouls] 四魂之玉{(next ? "开启" : "关闭")}（写回真遗物：{!ReferenceEquals(live, this)}）");
        await Task.CompletedTask;
    }

    // === 战斗接入 ===

    // 战斗开始：玩家强化 + 敌人四魂
    public override async Task BeforeCombatStart()
    {
        var owner = Owner;
        var creature = owner?.Creature;
        var combatState = creature?.CombatState;
        if (owner != null && creature != null && combatState != null && Enabled)
        {
            // 玩家与带四魂的敌人都会在这里被挂上【四魂】显示用能力（见 ApplyCombatStartBuffs）
            await FourSoulsSystem.ApplyCombatStartBuffs(
                new ThrowingPlayerChoiceContext(), owner, combatState);
        }
        await base.BeforeCombatStart();
    }

    // 战斗胜利：房间里的四魂全部收进玩家碎片
    public override async Task AfterCombatVictory(CombatRoom room)
    {
        if (Owner != null)
        {
            await FourSoulsSystem.AwardRoomSouls(Owner);
        }
        await base.AfterCombatVictory(room);
    }
}
