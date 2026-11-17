using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Relics;

// 神乐的伞（来自《银魂》）：
// 每回合切换形态，每场战斗从防御形态开始。
//   防御形态：回合开始获得格挡
//   攻击形态：回合开始对随机敌人造成两次伤害
// 图标暂用原版占位图。
[RegisterRelic(typeof(SharedRelicPool))]
public class KaguraUmbrella : ModRelicTemplate
{
    private const string PlaceholderIcon = "res://images/powers/missing_power.png";

    // 防御形态的格挡
    private const int DefenseBlock = 10;

    // 攻击形态：每段伤害与段数
    private const int AttackDamage = 7;
    private const int AttackHits = 2;

    private bool _isDefense = true;

    // 当前形态要进存档，读档后不能白白重来一次
    [SavedProperty]
    public bool IsDefense
    {
        get => _isDefense;
        set
        {
            AssertMutable();
            _isDefense = value;
        }
    }

    public override RelicRarity Rarity => RelicRarity.Event;

    #if STS2_113

    protected override bool IsAllowedInternal(IRunState runState)

    #else

    public override bool IsAllowed(IRunState runState)

    #endif
        => DevelopmentContent.Gintama;
    public override bool IsAllowedInShops => DevelopmentContent.Gintama;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: PlaceholderIcon,
        IconOutlinePath: PlaceholderIcon,
        BigIconPath: PlaceholderIcon
    );

    // 每场战斗都从防御形态开始
    public override async Task BeforeCombatStartLate()
    {
        IsDefense = true;
        await base.BeforeCombatStartLate();
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner)
        {
            Flash();
            if (IsDefense)
            {
                await CreatureCmd.GainBlock(Owner.Creature, DefenseBlock, ValueProp.Unpowered, null);
            }
            else
            {
                await StrikeRandomEnemy(choiceContext);
            }

            // 结算完再切形态，下一回合用另一面
            IsDefense = !IsDefense;
        }

        await base.AfterPlayerTurnStart(choiceContext, player);
    }

    private async Task StrikeRandomEnemy(PlayerChoiceContext choiceContext)
    {
        for (var hit = 0; hit < AttackHits; hit++)
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
            await CreatureCmd.Damage(choiceContext, target, AttackDamage, ValueProp.Unpowered, Owner.Creature, null, null);
        }
    }
}
