using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Relics;
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
public class AnywhereDoor : ModRelicTemplate, IDoraemonItem, IModRightClickableRelic, ICombatCompactRelic
{
    private const int initialUses = 3;

    private static int s_remaining = initialUses;

    // 自由传送是否已开启（右键开关）。
    // static：战斗中遗物会被克隆，用 static 跨实例共享；SavedProperty：随存档保存
    private static bool s_armed;

    private bool _armed;

    [SavedProperty]
    public bool Armed
    {
        get => _armed;
        set
        {
            AssertMutable();
            _armed = value;
        }
    }

    // 玩家身上真正的那件遗物（右键派发的可能是克隆实例，直接读写 this 不可靠）
    private AnywhereDoor? LiveRelic => Owner?.Relics.OfType<AnywhereDoor>().FirstOrDefault();

    // 当前是否已开启（static 与存档字段任一为真）
    private static bool IsArmedIn(IRunState runState)
    {
        return s_armed || runState.Players.Any(p => p.Relics.OfType<AnywhereDoor>().Any(d => d.Armed));
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
    public override bool ShowCounter => s_remaining > 0;

    public override int DisplayAmount => s_remaining;

    // 悬浮提示：补充"自由飞行"的消耗规则
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => base.AdditionalHoverTips.Append(
        new HoverTip(
            new LocString("relics", "OTHERWORLD_TREASURES_RELIC_ANYWHERE_DOOR.tip.title"),
            new LocString("relics", "OTHERWORLD_TREASURES_RELIC_ANYWHERE_DOOR.tip.description")));

    public override async Task AfterObtained()
    {
        s_remaining = initialUses;
        s_armed = false;
        Armed = false;
        s_chargedCoords.Clear();
        Status = RelicStatus.Normal;
        InvokeDisplayAmountChanged();
        Entry.Logger.Info($"[AnywhereDoor] AfterObtained: uses reset to {s_remaining}");
        await base.AfterObtained();
    }

    // 供 MapTravel patch 查询：剩余飞行次数（日志用）
    public static int RemainingUses => s_remaining;

    // 是否还有剩余次数
    public static bool HasUsesLeft => s_remaining > 0;

    // 右键预检：注册模型身份令牌（右键同步派发依赖它）
    public bool CanHandleRightClickLocal(ModRightClickContext context)
    {
        RitsuLibFramework.EnsureModelIdentity(this);
        return true;
    }

    // 还有次数时才能开关自由传送
    public bool CanExecuteRightClick(ModRightClickExecutionContext context)
    {
        return Owner != null && HasUsesLeft;
    }

    // 右键：开关「自由传送」（开启后地图才会点亮可直达的节点）
    public async Task OnRightClick(ModRightClickExecutionContext context)
    {
        bool next = !IsArmed;
        s_armed = next;
        var relic = LiveRelic;
        if (relic != null)
        {
            relic.Armed = next;
        }
        // 开启时用 Active 让遗物图标有"已启用"的表现
        Status = next ? RelicStatus.Active : RelicStatus.Normal;
        // 正开着地图就地重算（走原版 NTransition 转场），不用玩家关掉地图再打开
        await Patch_NMapScreen_FreeTravelPoints.RefreshIfOpenAsync();
        Entry.Logger.Info($"[AnywhereDoor] 自由传送 {(next ? "开启" : "关闭")}，剩余次数={s_remaining}");
    }

    // 已经扣过次数的目的地坐标：同一个地图点重复进房间（如【如果电话亭】重开本场战斗、
    // 打完战斗后再次进入该点的房间）不再重复扣次。
    // 只按"最后两个已访问坐标是否相邻"判断会把重复进入同一地图点误判成新的飞越。
    private static readonly HashSet<MapCoord> s_chargedCoords = new();

    // 最后一次机会：此时死路检测生效，避免被困
    public static bool IsOnLastChance => s_remaining == 1;

    // 供 MapTravel patch 查询：当前 run 是否持有任意门（不看到剩余次数）
    public static bool HasRelic(IRunState runState)
    {
        return runState.Players.Any(p => p.Relics.OfType<AnywhereDoor>().Any());
    }

    // 供 MapTravel patch 查询：当前 run 是否有可用的任意门（已开启 + 还有次数）
    public static bool HasUsableFreeFlight(IRunState runState)
    {
        return s_remaining > 0 && HasRelic(runState) && IsArmedIn(runState);
    }

    // 与 WingedBoots 一致：只有走了「非相邻」路线才扣次数
    public override Task AfterRoomEntered(AbstractRoom room)
    {
        if (s_remaining <= 0)
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
        if (!s_chargedCoords.Add(currentPoint.coord))
        {
            Entry.Logger.Info($"[AnywhereDoor] {currentPoint.coord} 已扣过次数（重复进入同一地图点），本次不扣");
            return Task.CompletedTask;
        }

        s_remaining--;
        InvokeDisplayAmountChanged();
        if (s_remaining <= 0)
        {
            // 次数用尽：自动关闭自由传送，图标失效
            s_armed = false;
            var relic = LiveRelic;
            if (relic != null)
            {
                relic.Armed = false;
            }
            Status = RelicStatus.Disabled;
        }
        Entry.Logger.Info($"[AnywhereDoor] Free travel used, remaining={s_remaining}");
        return Task.CompletedTask;
    }
}
