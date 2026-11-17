using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Relics;

// 贝尔的日常小刀（来自《惊悚乐园》）：战斗开始时把一张临时【贝尔的日常小刀】放进手牌。
// 凯文暂不实现。
// 图标暂用原版占位图。
[RegisterRelic(typeof(SharedRelicPool))]
public class BellKnife : ModRelicTemplate
{
    private const string PlaceholderIcon = "res://images/powers/missing_power.png";

    public override RelicRarity Rarity => RelicRarity.Event;

    #if STS2_113

    protected override bool IsAllowedInternal(IRunState runState)

    #else

    public override bool IsAllowed(IRunState runState)

    #endif
        => DevelopmentContent.Extras;
    public override bool IsAllowedInShops => DevelopmentContent.Extras;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: PlaceholderIcon,
        IconOutlinePath: PlaceholderIcon,
        BigIconPath: PlaceholderIcon
    );

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner && player.PlayerCombatState?.TurnNumber == 1)
        {
            await GrantKnife(player);
        }

        await base.AfterPlayerTurnStart(choiceContext, player);
    }

    // 凯文暂不做召唤物，效果由遗物自己结算：每个玩家回合结束，给随机一名敌人上闪光
    public override async Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Combat.CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side == MegaCrit.Sts2.Core.Combat.CombatSide.Player && Owner != null)
        {
            await ApplyFlash(choiceContext);
        }

        await base.AfterSideTurnEnd(choiceContext, side, participants);
    }

    private async Task ApplyFlash(PlayerChoiceContext choiceContext)
    {
        var enemies = Owner?.Creature?.CombatState?.Enemies;
        if (enemies == null)
        {
            return;
        }

        var alive = enemies.Where(enemy => enemy.IsAlive).ToList();
        if (alive.Count == 0)
        {
            return;
        }

        var target = alive[System.Random.Shared.Next(alive.Count)];
        await PowerCmd.Apply<Powers.FlashPower>(choiceContext, target, 1m, Owner.Creature, null);
    }

    private async Task GrantKnife(Player player)
    {
        var combat = player.Creature?.CombatState;
        if (combat == null)
        {
            return;
        }

        Flash();
        var knife = combat.CreateCard(ModelDb.Card<Cards.BellKnifeCard>(), player);
        await CardPileCmd.Add(knife, PileType.Hand, CardPilePosition.Random, this);
        Entry.Logger.Info("[BellKnife] 把日常小刀放进手牌");
    }
}
