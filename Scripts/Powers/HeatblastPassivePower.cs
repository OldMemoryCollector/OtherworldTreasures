using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using OtherworldTreasures.Scripts.Alien;
using OtherworldTreasures.Scripts.Relics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Powers;

/// <summary>
/// 火焰人被动「熔岩之躯」：每回合 +1 能量上限；回合结束时燃烧（燃尽）手牌中所有状态牌，每张回复 2 生命。
/// 同时承载火焰人体系的"本回合"计数器与一次性标记（变身期间常驻，变回时随快照清除）。
///
/// 【灼伤牌免疫】原版灼伤牌在 DoTurnEnd → OnTurnEndInHand 才结算伤害（见原版 CombatManager），
/// 而本被动在更早的 BeforeSideTurnEnd 就把手牌里的状态牌（含灼伤牌）全部燃尽了，
/// 灼伤牌根本活不到结算伤害那一步 → 火焰人天然不会受到【灼伤】状态牌的伤害。
/// </summary>
[RegisterPower]
public class HeatblastPassivePower : ModPowerTemplate
{
    private const int EnergyPerTurn = 1;
    private const int HealPerBurnedCard = 2;

    // ===== 本回合计数器（AfterPlayerTurnStart 重置）=====

    /// <summary>本回合已燃尽的状态牌数量。</summary>
    public int BurnedStatusCountThisTurn;

    /// <summary>本回合是否燃尽过至少 1 张状态牌。</summary>
    public bool HasBurnedStatusThisTurn;

    /// <summary>本回合累计获得的额外能量（不含回合开始基础能量与熔岩之躯的上限+1）。</summary>
    public int ExtraEnergyGainedThisTurn;

    /// <summary>本回合是否获得过额外能量。</summary>
    public bool HasGainedExtraEnergyThisTurn;

    /// <summary>本回合打出的 0 费牌数量。</summary>
    public int ZeroCostCardsPlayedThisTurn;

    // ===== 一次性标记（消耗后复位）=====

    /// <summary>燃烧反应：下一次给予灼伤时额外 +N 层（使用后清零）。</summary>
    public int CombustionReactionBonus;

    /// <summary>熔火塑形：下一张燃尽状态牌的牌不消耗能量（使用后清零）。</summary>
    public bool NextBurnCardFreeEnergy;

    /// <summary>本次变身选定的专精流派（供炎狱天降的流派分支效果使用）。</summary>
    public AlienSpecialization CurrentSpec;

    public override PowerType Type => PowerType.Buff;

    // 数量固定 1、不显示数字
    public override PowerStackType StackType => PowerStackType.Single;

    // 占位图：先用英雄立绘，专属能力图做好后改这里
    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://OtherworldTreasures/images/heroes/Heatblast.png",
        BigIconPath: "res://OtherworldTreasures/images/heroes/Heatblast.png");

    public override LocString Title => new LocString("powers", "OTHERWORLD_TREASURES_POWER_HEATBLAST_PASSIVE.title");

    public override LocString Description => new LocString("powers", "OTHERWORLD_TREASURES_POWER_HEATBLAST_PASSIVE.description");

    // 每回合 +1 能量（走原版的能量上限修正钩子）
    public override decimal ModifyMaxEnergy(Player player, decimal amount)
        => player == Owner?.Player ? amount + EnergyPerTurn : amount;

    // 玩家回合开始：重置所有"本回合"计数器，并生成 1 张【灼伤】到手中。
    // 熔岩之躯本身就是燃料源——保证任何流派都不会因为燃料牌（岩浆喷涌/热浪/熔火塑形）沉底而断档。
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (Owner == null || player.Creature != Owner)
        {
            return;
        }
        BurnedStatusCountThisTurn = 0;
        HasBurnedStatusThisTurn = false;
        ExtraEnergyGainedThisTurn = 0;
        HasGainedExtraEnergyThisTurn = false;
        ZeroCostCardsPlayedThisTurn = 0;
        // 一次性标记不在这里重置：燃烧反应/熔火塑形是"本回合下一次"，若本回合没用掉就延续到下次使用（不跨回合强制清空）

        await AddBurnToHand(player);
    }

    // 往手牌里塞 1 张【灼伤】状态牌（走统一入口，会打上"我方"标记，变身结束只清这些）
    private static async Task AddBurnToHand(Player player)
    {
        if (player.Creature?.CombatState is not { } combatState)
        {
            return;
        }
        await HeatblastBurns.AddToHand(combatState, player, 1);
    }

    // 统计 0 费牌打出数量（供流星冲击使用）
    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (Owner == null || cardPlay.Card?.Owner?.Creature != Owner)
        {
            return Task.CompletedTask;
        }
        // 0 费牌：能量费用为 0
        if (cardPlay.Card.EnergyCost.Canonical == 0)
        {
            ZeroCostCardsPlayedThisTurn++;
        }
        return Task.CompletedTask;
    }

    // 回合结束：燃尽手牌中的状态牌（消耗），每张回血
    public override async Task BeforeSideTurnEnd(
        PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner == null || !participants.Contains(Owner))
        {
            return;
        }
        if (Owner.Player is not { } player || player.PlayerCombatState is not { } combatState)
        {
            return;
        }

        // 回合结束钩子里抛异常会打断整条回合管线（表现为卡死、日志里 turn loop died），必须吞掉
        try
        {
            // 先快照（消耗过程中手牌会变化）
            var statusCards = combatState.Hand.Cards.Where(c => c.Type == CardType.Status).ToList();
            if (statusCards.Count == 0)
            {
                return;
            }
            // 燃尽（被动回合结束燃烧不触发"熔火塑形免能"——那是给主动打牌用的）
            await HeatblastStatus.Burn(choiceContext, player, statusCards, cardPlay: null, fromPassive: true);
            await CreatureCmd.Heal(Owner, statusCards.Count * HealPerBurnedCard);
            Entry.Logger.Info($"[Heatblast] 熔岩之躯：燃烧 {statusCards.Count} 张状态牌，回复 {statusCards.Count * HealPerBurnedCard} 生命");
        }
        catch (System.Exception e)
        {
            Entry.Logger.Warn($"[Heatblast] 熔岩之躯回合结束燃烧失败（已忽略，避免打断回合管线）：{e}");
        }
    }
}
