using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using OtherworldTreasures.Scripts.UI;

namespace OtherworldTreasures.Scripts.Enchantments;

// 击退的统一结算：把一段伤害按规则传给目标身后的敌人。
// 击退附魔和万事屋的电动车共用这一份，避免两套实现走样。
public static class KnockbackResolver
{
    /// <param name="totalDamage">触发击退的那次伤害量</param>
    /// <param name="rank">1 = 只有第一段，2 = 连带到更后面的敌人</param>
    /// <param name="cumulativeByCreature">本次出牌内各目标累计承受的击退伤害（用于眩晕判定）</param>
    /// <param name="stunnedThisPlay">本次出牌内已经眩晕过的目标</param>
    public static async Task Apply(
        PlayerChoiceContext choiceContext,
        Creature target,
        int totalDamage,
        int rank,
        Dictionary<Creature, int> cumulativeByCreature,
        HashSet<Creature> stunnedThisPlay)
    {
        if (totalDamage <= 0 || target.Side != CombatSide.Enemy || !target.IsAlive)
        {
            return;
        }

        // TotalDamage includes the portion absorbed by Block, matching the original hit's definition.
        int knockbackDamage = Math.Max(1, totalDamage / 2);
        bool targetIsGiant = IsGiant(target);
        if (targetIsGiant)
        {
            await DealKnockbackDamage(choiceContext, target, knockbackDamage,
                () => KnockbackVfx.ShowSelfHit(target, heavy: true), cumulativeByCreature, stunnedThisPlay);
            return;
        }

        IReadOnlyList<Creature> enemies = target.CombatState?.Enemies ?? [];
        Creature? firstBehindTarget = FindNextAlive(enemies, target);
        if (firstBehindTarget == null)
        {
            // With only one living enemy, tier II's second hit recoils onto the original target.
            await DealKnockbackDamage(choiceContext, target, knockbackDamage,
                () => KnockbackVfx.ShowSelfHit(target, heavy: false), cumulativeByCreature, stunnedThisPlay);
            if (rank >= 2 && target.IsAlive)
            {
                await DealKnockbackDamage(choiceContext, target, Math.Max(1, knockbackDamage / 2),
                    () => KnockbackVfx.ShowSelfHit(target, heavy: false, secondSegment: true),
                    cumulativeByCreature, stunnedThisPlay);
            }

            return;
        }

        bool firstBehindIsGiant = IsGiant(firstBehindTarget);
        Creature? secondBehind = FindNextAlive(enemies, firstBehindTarget);
        Task collisionReached = Task.CompletedTask;
        Action firstHitVisual = firstBehindIsGiant
            ? () => KnockbackVfx.ShowSelfHit(firstBehindTarget, heavy: true)
            : secondBehind != null
                ? () => collisionReached = KnockbackVfx.ShowPushHit(firstBehindTarget, target, secondBehind)
                : () => KnockbackVfx.ShowSelfHit(firstBehindTarget, heavy: false);
        await DealKnockbackDamage(choiceContext, firstBehindTarget, knockbackDamage,
            firstHitVisual, cumulativeByCreature, stunnedThisPlay);

        if (!firstBehindTarget.IsAlive)
        {
            KnockbackVfx.Cancel(firstBehindTarget);
            return;
        }

        if (rank < 2 || firstBehindIsGiant)
        {
            return;
        }

        int secondKnockbackDamage = Math.Max(1, knockbackDamage / 2);
        if (secondBehind == null)
        {
            await DealKnockbackDamage(choiceContext, firstBehindTarget, secondKnockbackDamage,
                () => KnockbackVfx.ShowSelfHit(firstBehindTarget, heavy: false, secondSegment: true),
                cumulativeByCreature, stunnedThisPlay);
        }
        else
        {
            // The following enemy takes its knockback damage exactly when the first unit reaches it.
            await collisionReached;
            if (!firstBehindTarget.IsAlive || !secondBehind.IsAlive)
            {
                return;
            }

            bool secondBehindIsGiant = IsGiant(secondBehind);
            await DealKnockbackDamage(choiceContext, secondBehind, secondKnockbackDamage,
                () => KnockbackVfx.ShowSelfHit(secondBehind, heavy: secondBehindIsGiant, secondSegment: true),
                cumulativeByCreature, stunnedThisPlay);
        }
    }

    private static async Task DealKnockbackDamage(
        PlayerChoiceContext choiceContext,
        Creature target,
        int amount,
        Action? startVisual,
        Dictionary<Creature, int> cumulativeByCreature,
        HashSet<Creature> stunnedThisPlay)
    {
        if (!target.IsAlive)
        {
            return;
        }

        // Start the visual without awaiting it; it cannot extend the damage command or turn timeline.
        startVisual?.Invoke();
        IEnumerable<DamageResult> damageResults = await CreatureCmd.Damage(
            choiceContext, target, amount, ValueProp.Unpowered, null, null);
        if (!target.IsAlive)
        {
            KnockbackVfx.Cancel(target);
            return;
        }

        int dealt = damageResults.Sum(result => result.TotalDamage);
        if (dealt <= 0)
        {
            return;
        }

        cumulativeByCreature.TryGetValue(target, out int previous);
        int cumulative = previous + dealt;
        cumulativeByCreature[target] = cumulative;

        if (target.IsAlive && cumulative * 2 > target.MaxHp && stunnedThisPlay.Add(target))
        {
            await CreatureCmd.Stun(target);
        }
    }

    private static Creature? FindNextAlive(IReadOnlyList<Creature> enemies, Creature creature)
    {
        int index = -1;
        for (int i = 0; i < enemies.Count; i++)
        {
            if (ReferenceEquals(enemies[i], creature))
            {
                index = i;
                break;
            }
        }

        for (int i = index + 1; i < enemies.Count; i++)
        {
            if (enemies[i].IsAlive)
            {
                return enemies[i];
            }
        }

        return null;
    }

    // Secondary enemies (such as minions and illusions) are never giants, even in a boss encounter.
    private static bool IsGiant(Creature creature)
    {
        if (creature.IsSecondaryEnemy)
        {
            return false;
        }

        return creature.Monster is IKnockbackImmune
            || creature.CombatState?.Encounter?.RoomType == RoomType.Boss;
    }
}
