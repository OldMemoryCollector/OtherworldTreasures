using System;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib;
using STS2RitsuLib.Interactions.RightClick;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Networking.ManagedActions;
using STS2RitsuLib.Scaffolding.Content;
using OtherworldTreasures.Scripts.Cards;
using OtherworldTreasures.Scripts.TimeCloth;

namespace OtherworldTreasures.Scripts.Relics;

// 时光布：右键点击获得一张【时光布】卡牌（每场战斗只能获得一张）。
// 同时负责维护战斗中的回合快照（【时光布】卡的回溯/加速依赖它）。
[RegisterRelic(typeof(SharedRelicPool))]
public class TimeCloth : ModRelicTemplate, IDoraemonItem, IModRightClickableRelic
{
    // 按玩家隔离的状态（遗物实例每次战斗会被克隆，static 又会被两名玩家共享）
    private sealed class PlayerState
    {
        // 本场战斗是否已发放过卡牌
        public bool GrantedThisCombat;
    }

    private PlayerState S => PerPlayerState<PlayerState>.Of(Owner);

    // 哆啦A梦道具：自定义稀有度
    public override RelicRarity Rarity => ModRelicRarity.Doraemon;

    // 原版 MerchantCost 的 switch 遇到未知稀有度会抛异常，这里直接给值
    public override int MerchantCost => 999999999;

    protected override string IconBaseName => "time_cloth";

    protected override string BigIconPath => "res://OtherworldTreasures/images/relics/Time_Cloth.jpg";

    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://OtherworldTreasures/images/relics/Time_Cloth.jpg",
        IconOutlinePath: "res://OtherworldTreasures/images/relics/Time_Cloth.jpg",
        BigIconPath: "res://OtherworldTreasures/images/relics/Time_Cloth.jpg"
    );

    // 悬浮提示：预览【时光布】卡牌（含回溯/加速两个抉择选项的说明）
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        base.AdditionalHoverTips.Concat(HoverTipFactory.FromCardWithCardHoverTips<TimeClothCard>());

    public override async Task AfterObtained()
    {
        S.GrantedThisCombat = false;
        Status = RelicStatus.Normal;
        TimeClothState.Clear();
        var identity = RitsuLibFramework.EnsureModelIdentity(this);
        Entry.Logger.Info($"[TimeCloth] AfterObtained, EnsureModelIdentity => {identity.Value}");
        await base.AfterObtained();
    }

    // 战斗开始：清掉上一场的快照，并重置"本场已发放"（图标恢复可点）
    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is CombatRoom)
        {
            S.GrantedThisCombat = false;
            Status = RelicStatus.Normal;
            TimeClothState.Clear();
        }
        await base.AfterRoomEntered(room);
    }

    // 玩家回合开始：记录本回合快照（回溯要用"上一回合开始时"的状态）
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner)
        {
            TimeClothState.CaptureAtPlayerTurnStart(player);
        }
        await base.AfterPlayerTurnStart(choiceContext, player);
    }

    // 战斗结束：清理快照，并恢复图标（下一场可再获得一张）
    public override async Task AfterCombatEnd(CombatRoom room)
    {
        S.GrantedThisCombat = false;
        Status = RelicStatus.Normal;
        TimeClothState.Clear();
        await base.AfterCombatEnd(room);
    }

    // 读档/新开跑时复位（见 RunLifecycle）：允许玩家 SL 后再次获得时光布
    internal static void ResetCombatScopedState()
    {
        // 按玩家隔离：整体清空（下次取用时重建为默认值）
        PerPlayerState<PlayerState>.ClearAll();
        TimeClothState.Clear();
    }

    // 右键预检：确保身份令牌已注册
    public bool CanHandleRightClickLocal(ModRightClickContext context)
    {
        RitsuLibFramework.EnsureModelIdentity(this);
        return true;
    }

    // 仅战斗内、且本场还没发放过时可点
    public bool CanExecuteRightClick(ModRightClickExecutionContext context)
    {
        return Owner != null
            && !S.GrantedThisCombat
            && CombatManager.Instance.DebugOnlyGetState() != null;
    }

    public async Task OnRightClick(ModRightClickExecutionContext context)
    {
        // 只有发起者本机负责入队；真正的发放由动作队列在所有端统一执行。
        // 不再依赖 RitsuLib 右键的跨端执行——实测远端没有执行到，导致手牌两端分叉。
        if (Owner == null || !Entry.IsLocalPlayer(Owner) || s_grantDescriptor == null)
        {
            return;
        }
        if (S.GrantedThisCombat)
        {
            return;
        }

        var queued = RitsuLibManagedNetActions.Request(RunManager.Instance, s_grantDescriptor, (byte)0);
        Entry.Logger.Info($"[TimeCloth] 发放动作已入队={queued}");
    }

    // === 联机：发放动作（所有端按动作队列执行同一份逻辑）===

    private static RitsuLibManagedNetActionDescriptor<byte>? s_grantDescriptor;

    internal static void RegisterGrantAction()
    {
        if (s_grantDescriptor != null)
        {
            return;
        }
        try
        {
            s_grantDescriptor = new RitsuLibManagedNetActionDescriptor<byte>(
                Entry.ModId,
                "time_cloth_grant",
                static _ => new byte[1],
                static _ => (byte)0,
                ExecuteGrantAsync,
                GameActionType.CombatPlayPhaseOnly);
            RitsuLibManagedNetActions.Register(s_grantDescriptor);
            Entry.Logger.Info("[TimeCloth] 发放动作已注册到联机动作队列");
        }
        catch (Exception e)
        {
            Entry.Logger.Info($"[TimeCloth] 发放动作注册失败（已忽略）：{e}");
        }
    }

    private static async Task ExecuteGrantAsync(RitsuLibManagedNetActionContext<byte> context)
    {
        try
        {
            var player = context.Player;
            var relic = player?.Relics?.OfType<TimeCloth>().FirstOrDefault();
            if (relic == null)
            {
                return;
            }
            await relic.GrantTimeClothCard();
        }
        catch (Exception e)
        {
            Entry.Logger.Info($"[TimeCloth] 发放动作执行异常（已忽略）：{e}");
        }
    }

    private async Task GrantTimeClothCard()
    {
        if (Owner == null)
        {
            return;
        }
        if (S.GrantedThisCombat)
        {
            Entry.Logger.Info("[TimeCloth] GrantTimeClothCard aborted: already granted this combat");
            return;
        }
        var combatState = CombatManager.Instance.DebugOnlyGetState();
        if (combatState == null)
        {
            Entry.Logger.Info("[TimeCloth] GrantTimeClothCard aborted: not in combat");
            return;
        }

        var canonical = ModelDb.AllCards.OfType<TimeClothCard>().FirstOrDefault();
        if (canonical == null)
        {
            Entry.Logger.Info("[TimeCloth] GrantTimeClothCard aborted: canonical TimeClothCard not found");
            return;
        }

        Flash();
        var card = combatState.CreateCard(canonical, Owner);
        await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, Owner, CardPilePosition.Random);

        // 每场战斗只能获得一张：标记本场已发放并让遗物变灰（战斗结束/下场战斗开始自动恢复）
        S.GrantedThisCombat = true;
        Status = RelicStatus.Disabled;
        Entry.Logger.Info("[TimeCloth] Granted 【时光布】 card to hand (once per combat, relic disabled)");
    }
}
