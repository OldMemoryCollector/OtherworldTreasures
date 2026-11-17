using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using OtherworldTreasures.Scripts.Balance;
using STS2RitsuLib;
using STS2RitsuLib.Interactions.RightClick;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using OtherworldTreasures.Scripts.Cards;

namespace OtherworldTreasures.Scripts.Relics;

// 缩小灯：右键点击获得一张【缩小灯】卡牌（1 费、消耗）；独立平衡版每场只获取一次。
// 与库拉的骰子/原素瓶一致，右键仅战斗内可用。
[RegisterRelic(typeof(SharedRelicPool))]
public class ShrinkRay : ModRelicTemplate, IModRightClickableRelic, IDoraemonItem,
    RelicRightClickSync.ICrossEndRightClickEffect
{
    private bool Balanced => RelicBalanceRules.ShrinkRay(IsMutable ? Owner : null);
    private ShrinkRay Progress => Owner?.Relics.OfType<ShrinkRay>().FirstOrDefault() ?? this;
    private string _balancedGrantCombatKey = string.Empty;

    [SavedProperty]
    public string SavedBalancedGrantCombatKey
    {
        get => _balancedGrantCombatKey;
        set { AssertMutable(); _balancedGrantCombatKey = value ?? string.Empty; }
    }

    private bool GrantedThisCombat => Owner.RunState.CurrentRoom is CombatRoom room &&
        Progress.SavedBalancedGrantCombatKey == RelicBalanceRules.CombatKey(Owner, room);

    public override Task AfterCombatEnd(CombatRoom room)
    {
        if (Balanced) Progress.SavedBalancedGrantCombatKey = string.Empty;
        return base.AfterCombatEnd(room);
    }

    // 哆啦A梦道具：自定义稀有度
    public override RelicRarity Rarity => ModRelicRarity.Doraemon;

    // 原版 MerchantCost 的 switch 遇到未知稀有度会抛异常，这里直接给值
    public override int MerchantCost => 999999999;

    protected override string IconBaseName => "shrink_ray";

    protected override string BigIconPath => "res://OtherworldTreasures/images/relics/Shrink_Ray.jpg";

    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://OtherworldTreasures/images/relics/Shrink_Ray.jpg",
        IconOutlinePath: "res://OtherworldTreasures/images/relics/Shrink_Ray.jpg",
        BigIconPath: "res://OtherworldTreasures/images/relics/Shrink_Ray.jpg"
    );

    // 悬浮提示：预览【缩小灯】卡牌 + 原版「缩小」能力说明
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => base.AdditionalHoverTips
        .Concat(HoverTipFactory.FromCardWithCardHoverTips<ShrinkRayCard>());

    // 遗物获得：注册模型身份令牌（右键同步派发依赖它）
    public override async Task AfterObtained()
    {
        Progress.SavedBalancedGrantCombatKey = string.Empty;
        var identity = RitsuLibFramework.EnsureModelIdentity(this);
        Entry.Logger.Info($"[ShrinkRay] AfterObtained, EnsureModelIdentity => {identity.Value}");
        await base.AfterObtained();
    }

    // 右键预检：只负责确保身份令牌已注册，固定返回 true
    public bool CanHandleRightClickLocal(ModRightClickContext context)
    {
        RitsuLibFramework.EnsureModelIdentity(this);
        return true;
    }

    // 仅在战斗内可点（合成卡牌需要战斗状态）。
    // 必须同时要求 IsInProgress：战斗结束后 DebugOnlyGetState 仍然非空，只判它会把右键放行到
    // 原版 AddGeneratedCardToCombat —— 该方法在战斗已结束（以及 creature 的 CombatState 为空）
    // 时返回空数组，内部却直接取 [0]，于是抛 ArgumentOutOfRangeException。
    // 异常又被右键通道的 catch 吞掉，玩家看到的就是「右键完全没反应」。
    public bool CanExecuteRightClick(ModRightClickExecutionContext context)
    {
        return Owner != null
            && RelicBalanceRules.Ready(Owner)
            && (!Balanced || !GrantedThisCombat)
            && CombatManager.Instance.IsInProgress
            && CombatManager.Instance.DebugOnlyGetState() != null;
    }

    // 只有发起者本机负责入队；实际发卡由联机动作在所有端统一执行
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
        await RelicBalanceRules.WaitReady(Owner);
        await GrantShrinkRayCard();
    }

    private async Task GrantShrinkRayCard()
    {
        if (Owner == null)
        {
            return;
        }
        // CanExecuteRightClick 只是发起端的本地预检，权威执行侧必须再兜一次：
        // 战斗不在进行中时绝不能走到 AddGeneratedCardToCombat。
        var combatState = CombatManager.Instance.DebugOnlyGetState();
        if (combatState == null || !CombatManager.Instance.IsInProgress)
        {
            Entry.Logger.Info("[ShrinkRay] GrantShrinkRayCard aborted: combat is not in progress");
            return;
        }

        if (Balanced && GrantedThisCombat) return;

        var canonical = ModelDb.AllCards.OfType<ShrinkRayCard>().FirstOrDefault();
        if (canonical == null)
        {
            Entry.Logger.Info("[ShrinkRay] GrantShrinkRayCard aborted: canonical ShrinkRayCard not found");
            return;
        }

        Flash();
        var card = combatState.CreateCard(canonical, Owner);
        if (Balanced && Owner.RunState.CurrentRoom is CombatRoom room)
            Progress.SavedBalancedGrantCombatKey = RelicBalanceRules.CombatKey(Owner, room);
        await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, Owner, CardPilePosition.Random);
        Entry.Logger.Info("[ShrinkRay] Granted 【缩小灯】 card to hand");
    }
}
