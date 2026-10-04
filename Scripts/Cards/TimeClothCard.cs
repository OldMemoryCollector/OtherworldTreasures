using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;
using OtherworldTreasures.Scripts.TimeCloth;
using STS2RitsuLib.Combat.CardTargeting;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Networking.ManagedActions;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Cards;

// 时光布（衍生卡）：抉择 —— 加速时间或回溯时间。
// 打出后两步交互：先用原版"选择一张牌"界面选择加速/回溯，
// 再走原版目标选择系统点选目标（自己 / 敌人 / 顶部遗物图标）。
[RegisterCard(typeof(TokenCardPool))]
public class TimeClothCard : ModCardTemplate, IDerivedCard
{
    private const int energyCost = 0;
    private const CardType type = CardType.Skill;
    // 衍生牌：不是先古牌，用普通技能牌样式
    private const CardRarity rarity = CardRarity.Token;
    // 卡本身不走出牌目标系统：目标在 OnPlay 里用自定义 StartTargeting 单独选择
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

        // 第一步：抉择 —— 回溯 / 加速。
        // 这一步必须【所有端都调用】原版选择接口：玩家选择的 id 记账是两端对称的，
        // 只在一端调用会让 choice id 分叉（联机分叉诊断实锤过）。
        // 发起者本机会弹界面，其它端等远端结果。
        var modeOptions = new List<CardModel>
        {
            combatState.CreateCard(ModelDb.Card<TimeClothRewind>(), Owner),
            combatState.CreateCard(ModelDb.Card<TimeClothAccelerate>(), Owner)
        };
        var modeCard = await CardSelectCmd.FromChooseACardScreen(choiceContext, modeOptions, Owner);
        if (modeCard == null)
        {
            return;
        }
        bool isRewind = modeCard is TimeClothRewind;
        TimeClothState.PendingIsRewind = isRewind;

        // 第二步：目标选择只有发起者本机能做（本地 UI）；
        // 选完把"模式 + 目标"用联机动作下发给所有端统一结算
        if (!Entry.IsLocalPlayer(Owner) || s_targetDescriptor == null)
        {
            return;
        }

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

        // 把目标打包：生物走 CombatId；遗物走"主人 NetId + 遗物序号"（两端一致）
        ClothPayload payload;
        switch (targetNode)
        {
            case NCreature creatureNode when creatureNode.Entity?.CombatId is uint combatId:
                payload = new ClothPayload(isRewind, combatId, 0, 0);
                break;
            case NRelicInventoryHolder holder:
                var relic = holder.Relic?.Model;
                var relicOwner = relic?.Owner;
                if (relic == null || relicOwner?.Relics == null)
                {
                    return;
                }
                var index = IndexOfRelic(relicOwner.Relics, relic);
                if (index < 0)
                {
                    Entry.Logger.Info("[TimeCloth] 找不到目标遗物在列表中的位置，取消");
                    return;
                }
                payload = new ClothPayload(isRewind, 0, relicOwner.NetId, index);
                break;
            default:
                Entry.Logger.Info("[TimeCloth] 目标类型不支持，取消");
                return;
        }

        var queued = RitsuLibManagedNetActions.Request(RunManager.Instance, s_targetDescriptor, payload);
        Entry.Logger.Info($"[TimeCloth] 抉择动作已入队={queued}: {(isRewind ? "Rewind" : "Accelerate")}（目标 {targetNode.GetType().Name}）");
    }

    // === 联机：抉择动作（所有端按动作队列执行同一份结算逻辑）===

    private readonly record struct ClothPayload(bool IsRewind, uint CombatId, ulong RelicOwnerNetId, int RelicIndex);

    private static RitsuLibManagedNetActionDescriptor<ClothPayload>? s_targetDescriptor;

    internal static void RegisterTargetAction()
    {
        if (s_targetDescriptor != null)
        {
            return;
        }
        try
        {
            s_targetDescriptor = new RitsuLibManagedNetActionDescriptor<ClothPayload>(
                Entry.ModId,
                "time_cloth_choice",
                static p =>
                {
                    var bytes = new byte[17];
                    bytes[0] = p.IsRewind ? (byte)1 : (byte)0;
                    BitConverter.GetBytes(p.CombatId).CopyTo(bytes, 1);
                    BitConverter.GetBytes(p.RelicOwnerNetId).CopyTo(bytes, 5);
                    BitConverter.GetBytes(p.RelicIndex).CopyTo(bytes, 13);
                    return bytes;
                },
                static b => new ClothPayload(
                    b[0] != 0,
                    BitConverter.ToUInt32(b.Slice(1, 4)),
                    BitConverter.ToUInt64(b.Slice(5, 8)),
                    BitConverter.ToInt32(b.Slice(13, 4))),
                ExecuteTargetAsync,
                GameActionType.CombatPlayPhaseOnly);
            RitsuLibManagedNetActions.Register(s_targetDescriptor);
            Entry.Logger.Info("[TimeCloth] 抉择动作已注册到联机动作队列");
        }
        catch (Exception e)
        {
            Entry.Logger.Info($"[TimeCloth] 抉择动作注册失败（已忽略）：{e}");
        }
    }

    private static async Task ExecuteTargetAsync(RitsuLibManagedNetActionContext<ClothPayload> context)
    {
        try
        {
            var player = context.Player;
            if (player == null)
            {
                return;
            }
            var msg = context.Message;

            if (msg.CombatId != 0)
            {
                var creature = player.Creature?.CombatState?.GetCreature(msg.CombatId);
                if (creature == null)
                {
                    Entry.Logger.Info("[TimeCloth] 抉择动作：目标生物已不存在，跳过");
                    return;
                }
                await TimeClothState.ApplyToCreature(creature, msg.IsRewind);
                return;
            }

            var owner = player.RunState?.Players?.FirstOrDefault(p => p.NetId == msg.RelicOwnerNetId);
            var relics = owner?.Relics;
            if (relics == null || msg.RelicIndex < 0 || msg.RelicIndex >= relics.Count)
            {
                Entry.Logger.Info("[TimeCloth] 抉择动作：目标遗物已不存在，跳过");
                return;
            }
            TimeClothState.ApplyToRelic(relics[msg.RelicIndex], msg.IsRewind);
        }
        catch (Exception e)
        {
            Entry.Logger.Info($"[TimeCloth] 抉择动作执行异常（已忽略）：{e}");
        }
    }

    // 在玩家遗物列表里定位某件遗物（先按引用，引用不一致时退回按 ModelId）
    private static int IndexOfRelic(IReadOnlyList<RelicModel> relics, RelicModel target)
    {
        for (var i = 0; i < relics.Count; i++)
        {
            if (ReferenceEquals(relics[i], target))
            {
                return i;
            }
        }
        for (var i = 0; i < relics.Count; i++)
        {
            if (relics[i].Id.Equals(target.Id))
            {
                return i;
            }
        }
        return -1;
    }
}
