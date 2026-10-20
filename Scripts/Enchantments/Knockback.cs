using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Enchantments;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using OtherworldTreasures.Scripts.UI;

namespace OtherworldTreasures.Scripts.Enchantments;

/// <summary>
/// Implement on a monster model to mark it as too large to be pushed.
/// Primary enemies in boss encounters are treated as giant by default; secondary enemies remain pushable.
/// </summary>
public interface IKnockbackImmune
{
}

public abstract class KnockbackEnchantmentBase : OtherworldEnchantmentBase
{
    private readonly Dictionary<Creature, int> _knockbackDamageByCreature = new();
    private readonly HashSet<Creature> _stunnedThisPlay = new();

    protected abstract int Rank { get; }

    public override bool CanEnchant(CardModel card)
        => base.CanEnchant(card) && card.Type == CardType.Attack;

    protected override void OnEnchant()
    {
        // The two registered models have fixed ranks; persist the rank through the native Amount field.
        Amount = Rank;
    }

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext,
        Creature? dealer,
        DamageResult results,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (!target.IsAlive)
            KnockbackVfx.Cancel(target);

        if (cardSource?.Enchantment is not KnockbackEnchantmentBase sourceEnchantment
            || !ReferenceEquals(sourceEnchantment, this)
            || (props & ValueProp.Move) == 0
            || results.TotalDamage <= 0
            || target.Side != CombatSide.Enemy
            || !target.IsAlive)
        {
            return;
        }

        // TotalDamage includes the portion absorbed by Block, matching the original hit's definition.
        int knockbackDamage = Math.Max(1, results.TotalDamage / 2);
        bool targetIsGiant = IsGiant(target);
        if (targetIsGiant)
        {
            await DealKnockbackDamage(choiceContext, target, knockbackDamage,
                () => KnockbackVfx.ShowSelfHit(target, heavy: true));
            return;
        }

        IReadOnlyList<Creature> enemies = target.CombatState?.Enemies ?? [];
        Creature? firstBehindTarget = FindNextAlive(enemies, target);
        if (firstBehindTarget == null)
        {
            // With only one living enemy, tier II's second hit recoils onto the original target.
            await DealKnockbackDamage(choiceContext, target, knockbackDamage,
                () => KnockbackVfx.ShowSelfHit(target, heavy: false));
            if (Rank >= 2 && target.IsAlive)
                await DealKnockbackDamage(choiceContext, target, Math.Max(1, knockbackDamage / 2),
                    () => KnockbackVfx.ShowSelfHit(target, heavy: false, secondSegment: true));
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
            firstHitVisual);

        if (!firstBehindTarget.IsAlive)
        {
            KnockbackVfx.Cancel(firstBehindTarget);
            return;
        }

        if (Rank < 2 || firstBehindIsGiant)
            return;

        int secondKnockbackDamage = Math.Max(1, knockbackDamage / 2);
        if (secondBehind == null)
        {
            await DealKnockbackDamage(choiceContext, firstBehindTarget, secondKnockbackDamage,
                () => KnockbackVfx.ShowSelfHit(firstBehindTarget, heavy: false, secondSegment: true));
        }
        else
        {
            // The following enemy takes its knockback damage exactly when the first unit reaches it.
            await collisionReached;
            if (!firstBehindTarget.IsAlive || !secondBehind.IsAlive)
                return;

            bool secondBehindIsGiant = IsGiant(secondBehind);
            await DealKnockbackDamage(choiceContext, secondBehind, secondKnockbackDamage,
                () => KnockbackVfx.ShowSelfHit(secondBehind, heavy: secondBehindIsGiant, secondSegment: true));
        }
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (ReferenceEquals(cardPlay.Card, Card))
        {
            _knockbackDamageByCreature.Clear();
            _stunnedThisPlay.Clear();
        }

        return Task.CompletedTask;
    }

    private async Task DealKnockbackDamage(
        PlayerChoiceContext choiceContext,
        Creature target,
        int amount,
        Action? startVisual)
    {
        if (!target.IsAlive)
            return;

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
            return;

        _knockbackDamageByCreature.TryGetValue(target, out int previous);
        int cumulative = previous + dealt;
        _knockbackDamageByCreature[target] = cumulative;

        if (target.IsAlive && cumulative * 2 > target.MaxHp && _stunnedThisPlay.Add(target))
            await CreatureCmd.Stun(target);
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
                return enemies[i];
        }

        return null;
    }

    private static bool IsGiant(Creature creature)
    {
        // Secondary enemies (such as minions and illusions) are never giants, even in a boss encounter.
        if (creature.IsSecondaryEnemy)
            return false;

        return creature.Monster is IKnockbackImmune
            || creature.CombatState?.Encounter?.RoomType == RoomType.Boss;
    }
}

[RegisterEnchantment]
public sealed class KnockbackI : KnockbackEnchantmentBase
{
    protected override int Rank => 1;
}

[RegisterEnchantment]
public sealed class KnockbackII : KnockbackEnchantmentBase
{
    protected override int Rank => 2;
}
