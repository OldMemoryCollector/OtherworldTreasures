using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rooms;
using STS2RitsuLib;
using STS2RitsuLib.Interactions.RightClick;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using OtherworldTreasures.Scripts.Cards;
using OtherworldTreasures.Scripts.TimeCloth;

namespace OtherworldTreasures.Scripts.Relics;

// 时光布：右键点击获得一张【时光布】卡牌（每场战斗只能获得一张）。
// 同时负责维护战斗中的回合快照（【时光布】卡的回溯/加速依赖它）。
[RegisterRelic(typeof(SharedRelicPool))]
public class TimeCloth : ModRelicTemplate, IDoraemonItem, IModRightClickableRelic
{
    // 本场战斗是否已发放过卡牌（战斗中遗物会被克隆，用 static 跨实例共享；每场战斗开始时重置）
    private static bool s_grantedThisCombat;

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
        s_grantedThisCombat = false;
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
            s_grantedThisCombat = false;
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
        s_grantedThisCombat = false;
        Status = RelicStatus.Normal;
        TimeClothState.Clear();
        await base.AfterCombatEnd(room);
    }

    // 读档/新开跑时复位（见 RunLifecycle）：允许玩家 SL 后再次获得时光布
    internal static void ResetCombatScopedState()
    {
        s_grantedThisCombat = false;
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
            && !s_grantedThisCombat
            && CombatManager.Instance.DebugOnlyGetState() != null;
    }

    public async Task OnRightClick(ModRightClickExecutionContext context)
    {
        await GrantTimeClothCard();
    }

    private async Task GrantTimeClothCard()
    {
        if (Owner == null)
        {
            return;
        }
        if (s_grantedThisCombat)
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
        s_grantedThisCombat = true;
        Status = RelicStatus.Disabled;
        Entry.Logger.Info("[TimeCloth] Granted 【时光布】 card to hand (once per combat, relic disabled)");
    }
}
