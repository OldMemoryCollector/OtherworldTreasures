using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib;
using STS2RitsuLib.Networking.ManagedActions;
using STS2RitsuLib.Networking.Sidecar;
using STS2RitsuLib.RunData;

namespace OtherworldTreasures.Scripts.Konoe;

// 局内按玩家持久化的配色编号，供读档 / 后加入的客户端恢复
public sealed class KonoeColorSave
{
    public int ColorIndex { get; set; } = -1;
}

// 配色选择的唯一状态源：本机切换 -> 广播 -> 所有端同序应用 -> 刷新各自 UI / 视觉。
// 联机通道沿用工程已验证的 RitsuLibManagedNetActions（见 RelicRightClickSync / Omnitrix）。
internal static class KonoeColorState
{
    internal const int DefaultIndex = 0;

    // 本机最后一次选中的配色。角色选择界面早于 RunManager.NetService 可用，
    // 只按 NetId 存键会写进 0 号、而局内 Player.NetId 是 1，导致换色读不到。
    internal static int LocalIndex { get; private set; } = DefaultIndex;
    private static ulong _lobbyNetId;

    private static readonly Dictionary<ulong, int> ByNetId = new();
    private static PlayerRunSavedData<KonoeColorSave>? _slot;
    private static RitsuLibManagedNetActionDescriptor<ColorPayload>? _descriptor;

    // 角色选择界面属于开局前大厅，动作队列未必就绪；Sidecar 走底层网络，
    // 客户端发给主机并由主机中继广播，覆盖「开局前切换」这条路径。
    private static readonly RitsuLibSidecarSyncMessageDescriptor<ColorPayload> Sync = new(
        Entry.ModId, "konoe_color_sync_v1",
        static p => JsonSerializer.SerializeToUtf8Bytes(p),
        static b => JsonSerializer.Deserialize<ColorPayload>(b),
        Receive,
        LocationTargeted: false, ShouldBuffer: false, DispatchLocalOnBroadcast: false);

    private readonly record struct ColorPayload(ulong NetId, byte Index);

    internal static event Action<ulong, int>? Changed;

    internal static void Initialize()
    {
        try
        {
            using (RitsuLibFramework.BeginModDataRegistration(Entry.ModId))
            {
                RunSavedDataOptions options = new()
                {
                    WritePolicy = RunSavedDataWritePolicy.WhenSet,
                    SyncLobbyOnChange = true,
                };
                _slot = RitsuLibFramework.GetRunSavedDataStore(Entry.ModId)
                    .RegisterPerPlayer("konoe_color_v1", () => new KonoeColorSave(), options: options);
            }
        }
        catch (Exception e)
        {
            Entry.Logger.Warn($"[KonoeColor] 局内数据槽注册失败（已忽略）：{e}");
        }

        try
        {
            _descriptor = new RitsuLibManagedNetActionDescriptor<ColorPayload>(
                Entry.ModId,
                "konoe_color_select",
                static p => JsonSerializer.SerializeToUtf8Bytes(p),
                static b => JsonSerializer.Deserialize<ColorPayload>(b),
                ApplyRemoteAsync,
                // 配色在角色选择（战斗外）与局内都可能切换
                GameActionType.Any);
            RitsuLibManagedNetActions.Register(_descriptor);
            RitsuLibSidecarSyncMessages.Register(Sync);
            RunManager.Instance.RunStarted += OnRunStarted;
            Entry.Logger.Info("[KonoeColor] 配色切换动作 + 大厅广播通道已注册");
        }
        catch (Exception e)
        {
            Entry.Logger.Warn($"[KonoeColor] 联机通道注册失败（已忽略）：{e}");
        }
    }

    // 角色选择阶段 RunManager.NetService 可能还没建好，优先用大厅本地玩家 id
    // （StartRunLobbyPlayer.id 就是局内 Player.NetId）。
    internal static void NoteLobbyNetId(ulong netId)
    {
        if (netId == 0UL || netId == _lobbyNetId) return;
        _lobbyNetId = netId;
        // 把「还不知道真 NetId 时」写进 0 号键的选择迁移过来
        if (ByNetId.TryGetValue(0UL, out int pending))
        {
            ByNetId.Remove(0UL);
            ByNetId[netId] = pending;
        }
    }

    internal static ulong LocalNetId
    {
        get
        {
            if (_lobbyNetId != 0UL) return _lobbyNetId;
            try { return RunManager.Instance?.NetService?.NetId ?? 0UL; }
            catch { return 0UL; }
        }
    }

    internal static int Get(ulong netId) =>
        ByNetId.TryGetValue(netId, out int index) ? index : (netId == LocalNetId ? LocalIndex : DefaultIndex);

    internal static int Get(Player? player)
    {
        if (player == null) return LocalIndex;
        if (ByNetId.TryGetValue(player.NetId, out int index)) return index;
        if (_slot != null && player.RunState != null)
        {
            try
            {
                int saved = _slot.Get(player).ColorIndex;
                if (saved >= 0 && saved < KonoeColorPalette.Count) return saved;
            }
            catch { /* 数据槽在局外不可用 */ }
        }
        // 本机玩家兜底：不管 NetId 何时才可用，选过的配色一定能带进这一局
        if (Entry.IsLocalPlayer(player)) return LocalIndex;
        return DefaultIndex;
    }

    // 只有本机玩家在切换配色时调用：先本地生效，再把编号广播到同局所有端
    internal static void SetLocal(int index)
    {
        index = ((index % KonoeColorPalette.Count) + KonoeColorPalette.Count) % KonoeColorPalette.Count;
        ulong netId = LocalNetId;
        LocalIndex = index;
        ApplyLocal(netId, index);
        ColorPayload payload = new(netId, (byte)index);
        // 通道一：大厅/局内都能用的 Sidecar（客户端 -> 主机 -> 全员）
        try
        {
            if (RunManager.Instance != null)
                RitsuLibSidecarSyncMessages.SendToHostAndBroadcast(RunManager.Instance, Sync, payload);
        }
        catch (Exception e)
        {
            Entry.Logger.Warn($"[KonoeColor] 大厅广播失败（本机状态仍生效）：{e}");
        }
        // 通道二：局内动作队列，所有端按同一顺序执行（工程里 8 处同构用法）
        try
        {
            if (_descriptor != null && RunManager.Instance != null)
            {
                bool queued = RitsuLibManagedNetActions.Request(RunManager.Instance, _descriptor, payload);
                Entry.Logger.Info($"[KonoeColor] 配色广播入队={queued} netId={netId} index={index}");
            }
        }
        catch (Exception e)
        {
            Entry.Logger.Warn($"[KonoeColor] 动作队列广播失败（本机状态仍生效）：{e}");
        }

        // 局内数据槽：读档 / 后加入的客户端可恢复同一编号
        try
        {
            var run = RunManager.Instance?.DebugOnlyGetState();
            if (run != null && _slot != null)
            {
                foreach (Player player in run.Players)
                {
                    if (player.NetId != netId) continue;
                    _slot.Modify(player, data => data.ColorIndex = index);
                    break;
                }
            }
        }
        catch { /* 局外没有 RunState，忽略 */ }
    }

    private static async Task ApplyRemoteAsync(RitsuLibManagedNetActionContext<ColorPayload> context)
    {
        try
        {
            ColorPayload msg = context.Message;
            int index = msg.Index;
            if (index >= KonoeColorPalette.Count) index = DefaultIndex;
            ApplyLocal(msg.NetId, index);

            // 写进按玩家的局内数据槽，读档 / 后加入的客户端能恢复同一编号
            var run = context.Player?.RunState;
            if (run != null && _slot != null)
            {
                foreach (Player player in run.Players)
                {
                    if (player.NetId != msg.NetId) continue;
                    _slot.Modify(player, data => data.ColorIndex = index);
                    break;
                }
            }
        }
        catch (Exception e)
        {
            Entry.Logger.Warn($"[KonoeColor] 远端配色应用异常（已吞，不打断队列）：{e}");
        }
        await Task.CompletedTask;
    }

    private static Task Receive(RitsuLibSidecarSyncMessageContext<ColorPayload> context)
    {
        ColorPayload msg = context.Message;
        int index = msg.Index < KonoeColorPalette.Count ? msg.Index : DefaultIndex;
        ApplyLocal(msg.NetId, index);   // 幂等：两条通道可能各送达一次
        return Task.CompletedTask;
    }

    // 开局时主机把已知编号补发一遍，让中途加入 / 重连的队友也拿到同一状态
    private static void OnRunStarted(RunState run)
    {
        try
        {
            if (RunManager.Instance.NetService.Type != NetGameType.Host) return;
            // 本机在角色选择阶段的选择，用现在可用的真 NetId 补登记，确保队友拿到
            ulong local = LocalNetId;
            if (local != 0UL) ByNetId[local] = LocalIndex;
            foreach (var pair in ByNetId)
                RitsuLibSidecarSyncMessages.Broadcast(RunManager.Instance, Sync,
                    new ColorPayload(pair.Key, (byte)pair.Value));
        }
        catch (Exception e)
        {
            Entry.Logger.Warn($"[KonoeColor] 开局补发失败（已忽略）：{e}");
        }
    }

    private static void ApplyLocal(ulong netId, int index)
    {
        // 同值不重复刷新，避免双通道重复投递造成 UI 抖动
        if (ByNetId.TryGetValue(netId, out int current) && current == index) return;
        ByNetId[netId] = index;
        Changed?.Invoke(netId, index);
    }
}
