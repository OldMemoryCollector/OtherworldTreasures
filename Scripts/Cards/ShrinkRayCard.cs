using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Combat.CardTargeting;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Cards;

// 缩小灯（衍生卡）：将一名敌人缩小；已被缩小的敌人无法再次被选中。
// 复用原版 ShrinkPower（缩放体型 + 该生物攻击伤害 -30%），施加 -1 层表示持续整场战斗，
// 与原版 ShrinkBeetle 的施加方式一致。
[RegisterCard(typeof(TokenCardPool))]
public class ShrinkRayCard : ModCardTemplate, IDerivedCard
{
    private const int energyCost = 1;
    private const CardType type = CardType.Skill;
    // 衍生牌：不是先古牌，用普通技能牌样式
    private const CardRarity rarity = CardRarity.Token;
    private const bool shouldShowInCardLibrary = true;

    // 自定义单体目标类型：只能选中"存活、可被施加能力、且尚未被缩小"的敌方生物。
    // TargetType 不是 virtual，RitsuLib 的 RegisterSingleTargetType 会把这些谓词注入 IsValidTarget。
    private static readonly TargetType ShrinkTargetType = CustomTargetType.RegisterSingleTargetType(
        Entry.ModId,
        "shrink_ray_target",
        (creature, player) => creature.CanReceivePowers
            && player?.Creature != null
            && creature.Side != player.Creature.Side
            && !creature.HasPower<ShrinkPower>());

    public ShrinkRayCard() : base(energyCost, type, rarity, ShrinkTargetType, shouldShowInCardLibrary)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://OtherworldTreasures/images/cards/Shrink_Ray.png"
    );

    // 关键词：除外（打出后进除外池）+ 湮灭（留在手里回合结束时也除外）
    public override IEnumerable<CardKeyword> CanonicalKeywords => [MyKeywords.Exclude, MyKeywords.Annihilate];

    // 打出后送入除外池（而不是消耗堆）
    protected override CardLocation GetResultLocationForCardPlay()
    {
        return new CardLocation(Owner, Entry.ExclusionPile, CardPilePosition.Top);
    }

    // 悬浮提示：预览原版「缩小」能力（体型缩小 + 攻击伤害 -30%）
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => base.AdditionalHoverTips
        .Append(HoverTipFactory.FromPower<ShrinkPower>());

    // 湮灭：回合结束时若此牌仍在手牌中，则将其除外（走原版回合结束前的时点，早于手牌结算）
    // 注意：本 hook 跑在回合结束管线中间，抛异常会打断整条管线（表现为卡死），因此全程兜异常。
    public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        try
        {
            if (!participants.Contains(Owner.Creature))
            {
                return;
            }
            if (Pile?.Type != PileType.Hand)
            {
                return;
            }
            Entry.Logger.Info("[ShrinkRayCard] 湮灭触发：卡牌送入除外池");
            // skipVisuals 必须为 true：原版回合结束清手牌同样跳过动画；
            // 否则这里会 await 卡牌飞向除外堆按钮的 Tween，在回合结束时段有卡死风险。
            var results = await CardPileCmd.Add(
                new[] { this },
                Entry.ExclusionPile,
                CardPilePosition.Top,
                null!,
                true);
            // 静默移动只会搬走模型，手牌里的卡牌节点会留下"尸体"。
            // 用原版配套 API 补一次视觉：它会补发旧牌堆回调并生成飞向除外池的动画（动画末尾释放卡牌节点）。
            // 只 Play 不 await：回合结束时段等待卡牌动画有卡死风险。
            var (tween, _) = CardPileCmd.GetTweenForCardsChangingPiles(results, fromSilentAdd: true);
            tween?.Play();
            Entry.Logger.Info($"[ShrinkRayCard] 湮灭完成：已进入除外池 (tween={tween != null})");
        }
        catch (Exception e)
        {
            Entry.Logger.Info($"[ShrinkRayCard] 湮灭 EXCEPTION（已忽略，避免打断回合结束）：{e}");
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var target = cardPlay.Target;
        if (target == null)
        {
            return;
        }
        // 双保险：已被缩小的敌人不再重复施加
        if (target.HasPower<ShrinkPower>())
        {
            return;
        }
        await PowerCmd.Apply<ShrinkPower>(choiceContext, target, -1m, Owner.Creature, this);
    }
}
