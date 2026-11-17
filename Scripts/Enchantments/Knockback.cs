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
    private Dictionary<Creature, int> _knockbackDamageByCreature = new();
    private HashSet<Creature> _stunnedThisPlay = new();

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _knockbackDamageByCreature = new();
        _stunnedThisPlay = new();
    }

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

        await KnockbackResolver.Apply(
            choiceContext, target, results.TotalDamage, Rank, _knockbackDamageByCreature, _stunnedThisPlay);
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
