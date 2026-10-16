using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Combat.AttackHits;
using OtherworldTreasures.Scripts.Alien;

namespace OtherworldTreasures.Scripts.Cards.Alien;

public abstract class AlienCardBase : ModCardTemplate, IDerivedCard, IAlienCard, IAttackHitHookListener
{
    protected AlienCardBase(int cost, CardType type, CardRarity rarity, TargetType target)
        : base(cost, type, rarity, target, true) { }
    public override int MaxUpgradeLevel => 0;
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => base.AdditionalHoverTips.Concat(AlienCardHoverTips.For(this));
    public Task BeforeAttackHit(AttackHitContext context)
    {
        if (ReferenceEquals(context.CardSource, this)) AlienAttack.BeforeHit(context);
        return Task.CompletedTask;
    }
    protected Task Draw(PlayerChoiceContext c, int count = 1) => CardPileCmd.Draw(c, count, Owner);
    protected Task Block(CardPlay p, decimal amount) => CreatureCmd.GainBlock(Owner.Creature, amount, ValueProp.Move, p);
    protected List<Creature> Enemies => Owner.Creature.CombatState?.Enemies.Where(e => e.IsAlive).ToList() ?? [];
    protected async Task Hit(PlayerChoiceContext c, CardPlay p, Creature? target, decimal amount, int hits = 1, bool piercing = false)
    {
        if (target is not { IsAlive: true }) return;
        var attack = DamageCmd.Attack(amount).FromCard(this, p).Targeting(target).WithHitCount(hits)
            .WithHitFx("vfx/vfx_attack_slash");
        if (piercing) AlienAttack.BypassBlock(attack);
        await attack.Execute(c);
    }
    protected async Task HitAll(PlayerChoiceContext c, CardPlay p, decimal amount, bool piercing = false)
    {
        if (Owner.Creature.CombatState is not { } combat || Enemies.Count == 0) return;
        var attack = DamageCmd.Attack(amount).FromCard(this, p).TargetingAllOpponents(combat).WithHitFx("vfx/vfx_attack_slash");
        if (piercing) AlienAttack.BypassBlock(attack);
        await attack.Execute(c);
    }
}
