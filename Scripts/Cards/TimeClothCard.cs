using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using OtherworldTreasures.Scripts.TimeCloth;
using STS2RitsuLib.Combat.CardTargeting;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Cards;

// 时光布（衍生卡）：抉择 —— 加速时间或回溯时间。
// - 抉择部分已实现（复用原版"选择一张牌"界面）
// - 回溯/加速的具体效果与目标选择待实现
// 目标：先选加速/回溯，再选目标（自己 / 敌人 / 遗物）。
[RegisterCard(typeof(TokenCardPool))]
public class TimeClothCard : ModCardTemplate, IDerivedCard
{
    private const int energyCost = 0;
    private const CardType type = CardType.Skill;
    // 衍生牌：不是先古牌，用普通技能牌样式
    private const CardRarity rarity = CardRarity.Token;
    // TODO(框架阶段)：目标选择需要"先选模式、再选目标"，暂时用 Self 占位
    private const TargetType targetType = TargetType.Self;
    private const bool shouldShowInCardLibrary = true;

    public TimeClothCard() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary)
    {
    }

    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://OtherworldTreasures/images/cards/Time_Cloth.png"
    );

    // 抉择（新词条）+ 除外（用掉后送入除外池，一次性）
    public override IEnumerable<CardKeyword> CanonicalKeywords => [MyKeywords.Choice, MyKeywords.Exclude];

    // 打出后送入除外池：用掉即移除，不会再回到牌库/弃牌堆
    protected override CardLocation GetResultLocationForCardPlay()
    {
        return new CardLocation(Owner, Entry.ExclusionPile, CardPilePosition.Top);
    }

    // 悬浮提示：预览两个抉择衍生选项【回溯】/【加速】
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => base.AdditionalHoverTips
        .Append(HoverTipFactory.FromCard<TimeClothRewind>())
        .Append(HoverTipFactory.FromCard<TimeClothAccelerate>());

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var combatState = CombatManager.Instance.DebugOnlyGetState();
        if (combatState == null || Owner == null)
        {
            return;
        }

        var screenContext = new BlockingPlayerChoiceContext();

        // 第一步：抉择 —— 回溯 / 加速（复用原版"选择一张牌"界面，知识恶魔「知识的诅咒」同款机制）
        var modeOptions = new List<CardModel>
        {
            combatState.CreateCard(ModelDb.Card<TimeClothRewind>(), Owner),
            combatState.CreateCard(ModelDb.Card<TimeClothAccelerate>(), Owner)
        };
        var modeCard = await CardSelectCmd.FromChooseACardScreen(screenContext, modeOptions, Owner);
        if (modeCard == null)
        {
            return;
        }
        bool isRewind = modeCard is TimeClothRewind;
        TimeClothState.PendingIsRewind = isRewind;

        // 第二步：目标选择 —— 走原版的目标选择系统（点自己 / 某个敌人 / 顶部遗物图标）
        var targetManager = NTargetManager.Instance;
        var ownerNode = NCombatRoom.Instance?.GetCreatureNode(Owner.Creature);
        if (targetManager == null || ownerNode == null)
        {
            Entry.Logger.Info("[TimeCloth] Target selection unavailable");
            return;
        }

        targetManager.StartTargeting(
            CustomTargetType.Anyone,
            ownerNode.Hitbox,
            TargetMode.ClickMouseToTarget,
            null,
            TimeClothState.AllowTargetNode);

        // 标记"目标选择中"：此时点击遗物格要当作选目标，拦截原版打开遗物介绍的行为
        TimeClothState.IsTargeting = true;
        Node? targetNode;
        try
        {
            targetNode = await targetManager.SelectionFinished();
        }
        finally
        {
            TimeClothState.IsTargeting = false;
        }

        if (targetNode == null)
        {
            Entry.Logger.Info("[TimeCloth] Target selection cancelled");
            return;
        }

        await TimeClothState.ApplyToNode(targetNode, isRewind);
        Entry.Logger.Info($"[TimeCloth] Resolved: {(isRewind ? "Rewind" : "Accelerate")} -> {targetNode.GetType().Name}");
    }
}
