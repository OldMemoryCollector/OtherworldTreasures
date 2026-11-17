using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using OtherworldTreasures.Scripts.Enchantments;
using OtherworldTreasures.Scripts.Balance;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Relics;

// 万事屋的电动车（来自《银魂》）：战斗开始时冲向最前方的敌人。
// 普通敌人受到其最大生命值 50% 的伤害，并被撞开；被撞开的敌人撞向身后的一名敌人，
// 造成一半的击退伤害。Boss 以巨型单位的方式抵抗冲撞，只承受 25%，且不会被撞开。
// 「巨型」判定与击退附魔共用同一套：次要敌人不算巨型，否则看 IKnockbackImmune 或 Boss 房间。
// 图标暂用原版占位图。
[RegisterRelic(typeof(SharedRelicPool))]
public class YorozuyaScooter : ModRelicTemplate
{
    private const string PlaceholderIcon = "res://images/powers/missing_power.png";

    // 普通目标承受的最大生命比例
    private const decimal NormalDamageRatio = 0.5m;

    // 巨型目标只承受四分之一
    private const decimal GiantDamageRatio = 0.25m;

    // 被撞开的敌人撞到身后目标时，承受原伤害的这一比例
    private const decimal KnockbackRatio = 0.5m;

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

    public override async Task BeforeCombatStartLate()
    {
        await RelicBalanceRules.WaitReady(Owner);
        await RamFrontEnemy();
        await base.BeforeCombatStartLate();
    }

    // 描述里的 [gold]击退[/gold] 只有染色，悬停说明靠这里挂上去
    protected override IEnumerable<IHoverTip> AdditionalHoverTips =>
        base.AdditionalHoverTips.Append(HoverTipFactory.FromKeyword(MyKeywords.Knockback));

    private async Task RamFrontEnemy()
    {
        var player = Owner;
        var enemies = player?.Creature?.CombatState?.Enemies;
        if (enemies == null)
        {
            return;
        }

        var front = enemies.FirstOrDefault(enemy => enemy.IsAlive);
        if (front == null)
        {
            return;
        }

        // 与击退附魔一致的巨型判定：次要敌人（爪牙、分身）不算巨型
        var giant = !front.IsSecondaryEnemy
            && (front.Monster is IKnockbackImmune
                || front.CombatState?.Encounter?.RoomType == RoomType.Boss);
        bool boss = !front.IsSecondaryEnemy && front.CombatState?.Encounter?.RoomType == RoomType.Boss;
        decimal ratio = RelicBalanceRules.YorozuyaScooter(player)
            ? (boss ? 0.10m : 0.20m)
            : (giant ? GiantDamageRatio : NormalDamageRatio);
        var amount = Math.Max(1m, Math.Floor(front.MaxHp * ratio));
        var context = new ThrowingPlayerChoiceContext();

        Flash();
        var results = await CreatureCmd.Damage(context, front, amount, ValueProp.Unpowered, null, null);

        var dealt = (int)results.Sum(result => result.TotalDamage);
        Entry.Logger.Info(
            $"[YorozuyaScooter] 撞向 {front.Monster?.Id.Entry}，{(giant ? "巨型" : "普通")}承受 {dealt} 点（上限 {front.MaxHp}）");

        // 巨型目标仅结算冲撞伤害，不再触发后排击退伤害。
        if (giant)
        {
            return;
        }

        // 普通目标撞完之后，按击退附魔同一套规则把伤害传给身后的敌人
        await KnockbackResolver.Apply(context, front, dealt, 1, new(), new());
    }
}
