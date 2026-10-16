using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib;
using STS2RitsuLib.Interactions.RightClick;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Relics;

// 任意门：右键点击开启「自由传送」，开启后地图上可直接前往任意「未通过」的节点（含跨行、回退、直飞 Boss），
// 可使用 3 次；再次右键点击关闭，即恢复原版的行动范围。
// 与哆啦A梦道具池成员。
// 实现方式：右键开关（本文件）+ MapTravel.GetTravelablePointsFrom 的 Harmony Postfix（见 GamePatches.cs）。
[RegisterRelic(typeof(SharedRelicPool))]
public class AnywhereDoor : ModRelicTemplate, IDoraemonItem, IModRightClickableRelic, ICombatCompactRelic,
    RelicRightClickSync.ICrossEndRightClickEffect
{
    private const int initialUses = 3;

    // 按玩家隔离的状态（遗物实例每次战斗重建，static 又会被两名玩家共享）
    private sealed class PlayerState
    {
        public readonly HashSet<(int ActIndex, MapCoord Coord)> ChargedCoords = new();
    }

    private PlayerState S => PerPlayerState<PlayerState>.Of(Owner);

    // 无 runState 参数时的静态查询取当前跑局
    private static IRunState? CurrentRun => RunManager.Instance?.DebugOnlyGetState();

    // 遍历跑局，取每位持有任意门玩家各自的状态
    private static IEnumerable<AnywhereDoor> StatesIn(IRunState? runState)
    {
        if (runState == null)
        {
            yield break;
        }
        foreach (var player in runState.Players)
        {
            foreach (var door in player.Relics.OfType<AnywhereDoor>())
            {
                yield return door;
            }
        }
    }

    private bool _armed;
    private int _savedRemaining = initialUses;

    [SavedProperty]
    public int SavedRemaining
    {
        get => _savedRemaining;
        set
        {
            AssertMutable();
            _savedRemaining = Math.Max(0, value);
            Status = _savedRemaining == 0 ? RelicStatus.Disabled
                : _armed ? RelicStatus.Active : RelicStatus.Normal;
        }
    }

    private AnywhereDoor Progress => IsMutable ? LiveRelic ?? this : this;
    private int Remaining => Progress.SavedRemaining;

    [SavedProperty]
    public bool Armed
    {
        get => _armed;
        set
        {
            AssertMutable();
            _armed = value;
            Status = _savedRemaining == 0 ? RelicStatus.Disabled
                : value ? RelicStatus.Active : RelicStatus.Normal;
        }
    }

    // 玩家身上真正的那件遗物（右键派发的可能是克隆实例，直接读写 this 不可靠）
    private AnywhereDoor? LiveRelic => Owner?.Relics.OfType<AnywhereDoor>().FirstOrDefault();

    // 当前是否已开启（任一玩家按玩家状态开启了自由传送，或存档字段为真）
    private static bool IsArmedIn(IRunState runState)
    {
        return StatesIn(runState).Any(d => d.Armed && d.Remaining > 0);
    }

    private bool IsArmed => Owner != null && IsArmedIn(Owner.RunState);

    // 哆啦A梦道具：自定义稀有度
    public override RelicRarity Rarity => ModRelicRarity.Doraemon;

    // 原版 MerchantCost 的 switch 遇到未知稀有度会抛异常，这里直接给值
    public override int MerchantCost => 999999999;

    protected override string IconBaseName => "anywhere_door";

    protected override string BigIconPath => "res://OtherworldTreasures/images/relics/Anywhere_Door.jpg";

    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://OtherworldTreasures/images/relics/Anywhere_Door.jpg",
        IconOutlinePath: "res://OtherworldTreasures/images/relics/Anywhere_Door.jpg",
        BigIconPath: "res://OtherworldTreasures/images/relics/Anywhere_Door.jpg"
    );

    // 剩余次数角标
    public override bool ShowCounter => Remaining > 0;

    public override int DisplayAmount => Remaining;

    // 悬浮提示：补充"自由飞行"的消耗规则
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => base.AdditionalHoverTips.Append(
        new HoverTip(
            new LocString("relics", "OTHERWORLD_TREASURES_RELIC_ANYWHERE_DOOR.tip.title"),
            new LocString("relics", "OTHERWORLD_TREASURES_RELIC_ANYWHERE_DOOR.tip.description")));

    public override async Task AfterObtained()
    {
        Progress.SavedRemaining = initialUses;
        Progress.Armed = false;
        S.ChargedCoords.Clear();
        Status = RelicStatus.Normal;
        InvokeDisplayAmountChanged();
        Entry.Logger.Info($"[AnywhereDoor] AfterObtained: uses reset to {Remaining}");
        await base.AfterObtained();
    }

    // 供 MapTravel patch 查询：剩余飞行次数（日志用）。按玩家存放，取持有者中最多的剩余次数
    public static int RemainingUses
    {
        get
        {
            var states = StatesIn(CurrentRun).ToList();
            return states.Count == 0 ? initialUses : states.Max(s => s.Remaining);
        }
    }

    // 是否还有剩余次数（任一玩家；无持有者时与原先的默认值一致，为 true）
    public static bool HasUsesLeft
    {
        get
        {
            var states = StatesIn(CurrentRun).ToList();
            return states.Count == 0 || states.Any(s => s.Remaining > 0);
        }
    }

    // 右键预检：注册模型身份令牌（右键同步派发依赖它）
    public bool CanHandleRightClickLocal(ModRightClickContext context)
    {
        RitsuLibFramework.EnsureModelIdentity(this);
        return true;
    }

    // 还有次数时才能开关自由传送
    public bool CanExecuteRightClick(ModRightClickExecutionContext context)
    {
        return Owner != null && Remaining > 0;
    }

    // 右键：开关「自由传送」（开启后地图才会点亮可直达的节点）。
    // 只有发起者本机负责入队，真正的开关由联机动作在所有端统一执行。
    public Task OnRightClick(ModRightClickExecutionContext context)
    {
        if (Owner == null || !Entry.IsLocalPlayer(Owner))
        {
            return Task.CompletedTask;
        }
        RelicRightClickSync.Request(Owner, this);
        return Task.CompletedTask;
    }

    async Task RelicRightClickSync.ICrossEndRightClickEffect.ApplyRightClickAsync(PlayerChoiceContext choiceContext)
    {
        if (Owner == null || Remaining <= 0) return;
        bool next = !Progress.Armed;
        Progress.Armed = next;
        // 开启时用 Active 让遗物图标有"已启用"的表现
        Progress.Status = next ? RelicStatus.Active : RelicStatus.Normal;
        // 正开着地图就地重算（走原版 NTransition 转场），不用玩家关掉地图再打开
        await Patch_NMapScreen_FreeTravelPoints.RefreshIfOpenAsync();
        Entry.Logger.Info($"[AnywhereDoor] 自由传送 {(next ? "开启" : "关闭")}，剩余次数={Remaining}");
    }

    // 只有已开启的门都最多剩一次时才是最后机会；关闭的门不能限制另一位玩家的路线。
    public static bool IsOnLastChance => StatesIn(CurrentRun).Where(d => d.Armed)
        .Select(d => d.Remaining).DefaultIfEmpty(0).Max() == 1;

    // 供 MapTravel patch 查询：当前 run 是否持有任意门（不看到剩余次数）
    public static bool HasRelic(IRunState runState)
    {
        return runState.Players.Any(p => p.Relics.OfType<AnywhereDoor>().Any());
    }

    // 供 MapTravel patch 查询：当前 run 是否有可用的任意门（任一玩家已开启 + 还有次数）
    public static bool HasUsableFreeFlight(IRunState runState)
    {
        return IsArmedIn(runState);
    }

    // 与 WingedBoots 一致：只有走了「非相邻」路线才扣次数
    public override Task AfterRoomEntered(AbstractRoom room)
    {
        // 联机共用地图，但只能扣实际开启自由传送的那件门。
        if (Remaining <= 0 || !Progress.Armed)
        {
            return Task.CompletedTask;
        }
        if (Owner?.RunState is not RunState runState)
        {
            return Task.CompletedTask;
        }
        // 只在进入该地图点的第一个房间时结算，避免一次移动重复扣次
        if (runState.CurrentRoomCount > 1)
        {
            return Task.CompletedTask;
        }
        IReadOnlyList<MapCoord> visited = runState.VisitedMapCoords;
        if (visited.Count <= 1)
        {
            return Task.CompletedTask;
        }

        var previousPoint = runState.Map.GetPoint(visited[visited.Count - 2]);
        var currentPoint = runState.CurrentMapPoint;
        if (previousPoint == null || currentPoint == null)
        {
            return Task.CompletedTask;
        }
        // 常规相邻移动（走的是原版连线）不消耗次数
        if (previousPoint.Children.Contains(currentPoint))
        {
            return Task.CompletedTask;
        }

        // 同一目的地只扣一次：重复进入这个地图点的房间不算新的飞越
        if (!S.ChargedCoords.Add((runState.CurrentActIndex, currentPoint.coord)))
        {
            Entry.Logger.Info($"[AnywhereDoor] {currentPoint.coord} 已扣过次数（重复进入同一地图点），本次不扣");
            return Task.CompletedTask;
        }

        Progress.SavedRemaining--;
        Progress.InvokeDisplayAmountChanged();
        if (Remaining <= 0)
        {
            // 次数用尽：自动关闭自由传送，图标失效
            Progress.Armed = false;
            Progress.Status = RelicStatus.Disabled;
        }
        Entry.Logger.Info($"[AnywhereDoor] Free travel used, remaining={Remaining}");
        return Task.CompletedTask;
    }
}
