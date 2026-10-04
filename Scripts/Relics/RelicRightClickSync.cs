using System;
using System.Text.Json;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Networking.ManagedActions;

namespace OtherworldTreasures.Scripts.Relics;

// 右键遗物的"跨端效果"统一通道。
//
// 背景：RitsuLib 右键的跨端执行在我们的模组上不可靠（实测远端根本没有跑到 OnRightClick），
// 于是所有靠它同步的右键效果都只在发起者一端生效 → 联机状态分叉。
// 因此我们的右键统一改成：发起者只负责"入队"，真正的效果由这个联机动作在所有端执行。
//
// 载荷：发起者 NetId + 遗物 ModelId。两端按同一份数据找到同一件遗物，
// 再调用它实现的 ICrossEndRightClickEffect.ApplyRightClickAsync。
internal static class RelicRightClickSync
{
    // 需要跨端执行的右键效果由遗物自己实现
    internal interface ICrossEndRightClickEffect
    {
        Task ApplyRightClickAsync(PlayerChoiceContext choiceContext);
    }

    private readonly record struct Payload(ulong OwnerNetId, string RelicId);

    private static RitsuLibManagedNetActionDescriptor<Payload>? s_descriptor;

    internal static void Register()
    {
        if (s_descriptor != null)
        {
            return;
        }
        try
        {
            s_descriptor = new RitsuLibManagedNetActionDescriptor<Payload>(
                Entry.ModId,
                "relic_right_click",
                static payload => JsonSerializer.SerializeToUtf8Bytes(payload),
                static bytes => JsonSerializer.Deserialize<Payload>(bytes),
                ExecuteAsync,
                // 右键遗物里既有"战斗中"（原素瓶/缩小灯/骰子）也有"战斗外"（任意门/四魂碎片），
                // 所以动作类型必须选 Any
                GameActionType.Any);
            RitsuLibManagedNetActions.Register(s_descriptor);
            Entry.Logger.Info("[RelicRightClick] 右键动作已注册到联机动作队列");
        }
        catch (Exception e)
        {
            Entry.Logger.Info($"[RelicRightClick] 右键动作注册失败（已忽略）：{e}");
        }
    }

    // 由各遗物的 OnRightClick 调用（只有发起者本机会走到这里）
    internal static void Request(Player owner, RelicModel relic)
    {
        if (s_descriptor == null)
        {
            return;
        }
        var queued = RitsuLibManagedNetActions.Request(
            RunManager.Instance, s_descriptor, new Payload(owner.NetId, relic.Id.ToString()));
        Entry.Logger.Info($"[RelicRightClick] {relic.Id.Entry} 已入队={queued}");
    }

    private static async Task ExecuteAsync(RitsuLibManagedNetActionContext<Payload> context)
    {
        try
        {
            var chooser = context.Player;
            var runState = chooser?.RunState;
            if (runState == null)
            {
                return;
            }

            foreach (var player in runState.Players)
            {
                if (player.NetId != context.Message.OwnerNetId)
                {
                    continue;
                }
                foreach (var relic in player.Relics)
                {
                    if (relic.Id.ToString() != context.Message.RelicId)
                    {
                        continue;
                    }
                    if (relic is ICrossEndRightClickEffect effect)
                    {
                        await effect.ApplyRightClickAsync(context.PlayerChoiceContext);
                    }
                    return;
                }
            }
            Entry.Logger.Info($"[RelicRightClick] 找不到目标遗物 {context.Message.RelicId}，跳过");
        }
        catch (Exception e)
        {
            Entry.Logger.Info($"[RelicRightClick] 右键动作执行异常（已忽略）：{e}");
        }
    }
}
