using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using OtherworldTreasures.Scripts.Relics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Powers;

/// <summary>
/// 小破表的变身状态：变身只持续 3 个回合，图标上的数量就是剩余回合数。
/// 形象替换（隐藏原身体 + 叠加英雄立绘）由遗物 Omnitrix 负责，本能力只负责计时。
/// 计时点在"玩家回合开始"：变身当回合不变，从下一个回合开始每回合 -1，
/// 数到 0 时就在那个回合开始的瞬间结束变身。
/// 结束演出的声画对齐（声音提前放、闪光与变身解除延后）在 Omnitrix 里，见 StartTransformationEnd。
/// </summary>
[RegisterPower]
public class OmnitrixTransformPower : ModPowerTemplate
{
    // 变身持续回合数
    public const int DurationTurns = 3;

    public override PowerType Type => PowerType.Buff;

    // 数量直接显示在图标上（剩余回合），需要手动递减
    public override PowerStackType StackType => PowerStackType.Counter;

    // 变身标志：用当前英雄的立绘（同时也就是"变身后获得的能力"的标志），
    // 图标上的数字即剩余变身回合数；英雄还没有专属内容时退回变身器（小破表）的图。
    // 注意：图鉴/预载阶段读的是 canonical 模型，此时碰 Owner 会抛 CanonicalModelException，
    // 所以必须先判 IsMutable 短路掉（canonical 走兜底图标）。
    public override PowerAssetProfile AssetProfile
    {
        get
        {
            string path = IsMutable && Owner?.GetPower<HeatblastPassivePower>() != null
                ? Omnitrix.GetHeroTexturePath(AlienHero.Heatblast)
                : "res://OtherworldTreasures/images/relics/Omnitrix.png";
            return new PowerAssetProfile(IconPath: path, BigIconPath: path);
        }
    }

    // 悬浮时把"变身后获得的能力"一并显示出来（目前的英雄被动）。同样要避开 canonical 模型。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips
        => IsMutable && Owner?.GetPower<HeatblastPassivePower>() is { } passive
            ? new[] { HoverTipFactory.FromPower(passive) }
            : Array.Empty<IHoverTip>();

    public override LocString Title => new LocString("powers", "OTHERWORLD_TREASURES_POWER_OMNITRIX_TRANSFORM.title");

    public override LocString Description => new LocString("powers", "OTHERWORLD_TREASURES_POWER_OMNITRIX_TRANSFORM.description");

    // 玩家回合开始扣 1；扣到 0 就在这一刻结束变身
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Owner == null || player.Creature != Owner)
        {
            return;
        }

        // 回合管线里的钩子抛异常会打断整条管线（表现为卡死），必须兜住
        try
        {
            if (Amount <= 1)
            {
                // 还原牌组/自身状态 + 结束演出（出声、闪光、解除形象），冷却长短由遗物决定
                await Omnitrix.OnTransformationExpired(player);
                await PowerCmd.Remove(this);
                return;
            }

            Flash();
            SetAmount(Amount - 1, silent: true);
        }
        catch (System.Exception e)
        {
            Entry.Logger.Warn($"[Omnitrix] 变身计时钩子失败（已忽略，避免打断回合管线）：{e}");
        }
    }

    // 最后一回合的敌方回合结束时收尾：还原牌组/自身状态 + 开始结束演出（声音、闪光、解除形象）。
    // 时点选在这里是因为此刻玩家回合已结束、手牌为空，换牌不会在手牌里留下卡牌节点。
    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner?.Player == null || side != CombatSide.Enemy || Amount > 1)
        {
            return;
        }
        // 同上：这里是回合结束管线，异常必须吞掉
        try
        {
            await Omnitrix.StartTransformationEnd(Owner.Player);
        }
        catch (System.Exception e)
        {
            Entry.Logger.Warn($"[Omnitrix] 变身收尾钩子失败（已忽略，避免打断回合管线）：{e}");
        }
    }

    // 兜底：能力被其它方式移除（战斗结束清空、被驱散等）时也要变回原形，但不播音、不动冷却
    public override Task AfterRemoved(Creature oldOwner)
    {
        Omnitrix.RestoreAppearanceSilently();
        return Task.CompletedTask;
    }
}
