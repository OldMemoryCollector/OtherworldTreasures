using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.RelicPools;
using STS2RitsuLib;
using STS2RitsuLib.Interactions.RightClick;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using OtherworldTreasures.Scripts.Cards;

namespace OtherworldTreasures.Scripts.Relics;

// 缩小灯：右键点击获得一张【缩小灯】卡牌（1 费、消耗），可无限获得。
// 与库拉的骰子/原素瓶一致，右键仅战斗内可用。
[RegisterRelic(typeof(SharedRelicPool))]
public class ShrinkRay : ModRelicTemplate, IModRightClickableRelic, IDoraemonItem
{
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
        .Concat(HoverTipFactory.FromCardWithCardHoverTips<ShrinkRayCard>())
        .Append(HoverTipFactory.FromPower<ShrinkPower>());

    // 遗物获得：注册模型身份令牌（右键同步派发依赖它）
    public override async Task AfterObtained()
    {
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

    // 仅在战斗内可点（合成卡牌需要战斗状态）
    public bool CanExecuteRightClick(ModRightClickExecutionContext context)
    {
        return Owner != null && CombatManager.Instance.DebugOnlyGetState() != null;
    }

    public async Task OnRightClick(ModRightClickExecutionContext context)
    {
        await GrantShrinkRayCard();
    }

    private async Task GrantShrinkRayCard()
    {
        if (Owner == null)
        {
            return;
        }
        var combatState = CombatManager.Instance.DebugOnlyGetState();
        if (combatState == null)
        {
            Entry.Logger.Info("[ShrinkRay] GrantShrinkRayCard aborted: not in combat");
            return;
        }

        var canonical = ModelDb.AllCards.OfType<ShrinkRayCard>().FirstOrDefault();
        if (canonical == null)
        {
            Entry.Logger.Info("[ShrinkRay] GrantShrinkRayCard aborted: canonical ShrinkRayCard not found");
            return;
        }

        Flash();
        var card = combatState.CreateCard(canonical, Owner);
        await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, Owner, CardPilePosition.Random);
        Entry.Logger.Info("[ShrinkRay] Granted 【缩小灯】 card to hand");
    }
}
