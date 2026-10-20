using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Enchantments;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using OtherworldTreasures.Scripts.Powers;
using OtherworldTreasures.Scripts.Alien;

namespace OtherworldTreasures.Scripts.Enchantments;

public abstract class SlayerEnchantmentBase : OtherworldEnchantmentBase
{
    protected abstract MonsterTypeTag TypeTag { get; }
    internal MonsterTypeTag SlayerTag => TypeTag;
    public override bool CanEnchant(CardModel card) => base.CanEnchant(card) && card.Type == CardType.Attack;
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay) =>
        target != null && ReferenceEquals(cardSource?.Enchantment, this) && props.IsPoweredAttack()
            && target.Side == CombatSide.Enemy && MonsterTypeTags.For(target).Contains(TypeTag) ? 1.5m : 1m;
}

[RegisterEnchantment] public sealed class HumanoidSlayer : SlayerEnchantmentBase { protected override MonsterTypeTag TypeTag => MonsterTypeTag.Humanoid; }
[RegisterEnchantment] public sealed class MechanicalSlayer : SlayerEnchantmentBase { protected override MonsterTypeTag TypeTag => MonsterTypeTag.Mechanical; }
[RegisterEnchantment] public sealed class InsectSlayer : SlayerEnchantmentBase { protected override MonsterTypeTag TypeTag => MonsterTypeTag.Insect; }
[RegisterEnchantment] public sealed class BeastSlayer : SlayerEnchantmentBase { protected override MonsterTypeTag TypeTag => MonsterTypeTag.Beast; }
[RegisterEnchantment] public sealed class SeaMonsterSlayer : SlayerEnchantmentBase { protected override MonsterTypeTag TypeTag => MonsterTypeTag.SeaMonster; }
[RegisterEnchantment] public sealed class PlantSlayer : SlayerEnchantmentBase { protected override MonsterTypeTag TypeTag => MonsterTypeTag.Plant; }
[RegisterEnchantment] public sealed class SlimeSlayer : SlayerEnchantmentBase { protected override MonsterTypeTag TypeTag => MonsterTypeTag.Slime; }
[RegisterEnchantment] public sealed class OtherSlayer : SlayerEnchantmentBase { protected override MonsterTypeTag TypeTag => MonsterTypeTag.Other; }

[RegisterEnchantment]
public sealed class FireAspect : OtherworldEnchantmentBase
{
    private readonly HashSet<Creature> _affectedThisPlay = new();
    public override bool CanEnchant(CardModel card) => base.CanEnchant(card) && card.Type == CardType.Attack;
    public override async Task AfterDamageGiven(PlayerChoiceContext context, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        if (ReferenceEquals(cardSource, Card) && target.Side == CombatSide.Enemy && result.TotalDamage > 0 && _affectedThisPlay.Add(target))
            await AlienScorch.Apply(context, target, 2, Card, Card?.Owner.Creature);
    }
    public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
    {
        if (ReferenceEquals(play.Card, Card)) _affectedThisPlay.Clear();
        return Task.CompletedTask;
    }
}

[RegisterEnchantment]
public sealed class SweepingEdge : OtherworldEnchantmentBase
{
    private decimal _totalDamage;
    private readonly HashSet<Creature> _primaryTargets = new();
    public override bool CanEnchant(CardModel card) => base.CanEnchant(card) && card.Type == CardType.Attack && card.TargetType.IsSingleTarget();
    public override async Task AfterDamageGiven(PlayerChoiceContext context, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        if (ReferenceEquals(cardSource, Card) && target.Side == CombatSide.Enemy)
        {
            _totalDamage += Math.Max(0, result.TotalDamage);
            _primaryTargets.Add(target);
        }
        await Task.CompletedTask;
    }
    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
    {
        if (!ReferenceEquals(play.Card, Card)) return;
        try
        {
            var state = Card?.Owner.Creature.CombatState;
            int amount = (int)Math.Floor(_totalDamage * 0.4m);
            if (state != null && amount > 0)
                foreach (var enemy in state.Enemies.Where(e => e.IsAlive && !_primaryTargets.Contains(e)).ToArray())
                    await CreatureCmd.Damage(context, enemy, amount, ValueProp.Unpowered, Card?.Owner.Creature, null, null);
        }
        finally { _totalDamage = 0; _primaryTargets.Clear(); }
    }
}

[RegisterEnchantment]
public sealed class Looting : OtherworldEnchantmentBase
{
    private readonly HashSet<Creature> _paidKills = new();
    public override bool CanEnchant(CardModel card) => base.CanEnchant(card) && card.Type == CardType.Attack;
    public override Task AfterDamageGiven(PlayerChoiceContext context, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        if (ReferenceEquals(cardSource, Card) && !target.IsAlive && target.Side == CombatSide.Enemy && _paidKills.Add(target))
            PlayerCmd.GainGold(12, Card!.Owner);
        return Task.CompletedTask;
    }
    public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
    {
        if (ReferenceEquals(play.Card, Card)) _paidKills.Clear();
        return Task.CompletedTask;
    }
}

[RegisterEnchantment]
public sealed class Efficiency : OtherworldEnchantmentBase
{
    [MegaCrit.Sts2.Core.Saves.Runs.SavedProperty] public bool ReducedCost { get; set; }
    public override bool CanEnchant(CardModel card) => base.CanEnchant(card) && card.Type == CardType.Skill;
    protected override void OnEnchant()
    {
        if (Card == null || ReducedCost || Card.EnergyCost.CostsX || Card.EnergyCost.Canonical <= 0) return;
        Card.EnergyCost.UpgradeBy(-1);
        ReducedCost = true;
    }
}

[RegisterEnchantment]
public sealed class Protection : OtherworldEnchantmentBase
{
    public override bool CanEnchant(CardModel card) => base.CanEnchant(card) && card.Type == CardType.Skill && card.GainsBlock;
    public override decimal EnchantBlockAdditive(decimal originalBlock) => 6m;
}

[RegisterEnchantment]
public sealed class Thorns : OtherworldEnchantmentBase
{
    public override bool CanEnchant(CardModel card) => base.CanEnchant(card) && card.Type == CardType.Skill;
    public override async Task OnPlay(PlayerChoiceContext context, CardPlay? cardPlay)
    {
        if (Card != null)
            await PowerCmd.Apply<EnchantTableThornsPower>(context, Card.Owner.Creature, 5, Card.Owner.Creature, Card);
    }
}

internal static class SlayerLimitBypass
{
    public static bool Applies(CardModel? card, Creature? target)
    {
        if (card?.Enchantment is not SlayerEnchantmentBase slayer || target == null) return false;
        if (!MonsterTypeTags.For(target).Contains(slayer.SlayerTag)) return false;
        return target.Powers.Any(p => Overrides(p, nameof(AbstractModel.ModifyDamageCap))
            || Overrides(p, nameof(AbstractModel.ModifyHpLostBeforeOstyLate)));
    }
    private static bool Overrides(AbstractModel model, string hook) =>
        model.GetType().GetMethod(hook)?.DeclaringType != typeof(AbstractModel);
}
