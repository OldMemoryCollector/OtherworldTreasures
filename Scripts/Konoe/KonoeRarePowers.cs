using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Combat.AttackHits;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;

namespace OtherworldTreasures.Scripts.Konoe;

public abstract class KonoeRarePower : KonoeBluePower { }

[RegisterPower]
public sealed class KonoeOverdrivePower : KonoeRarePower
{
    [SavedProperty] public bool SpellUsed { get; set; }
    public override Task AfterApplied(Creature? applier, CardModel? source) { SpellUsed = Owner.GetPower<KonoeElementsPower>()?.SpellsThisTurn > 0; return Task.CompletedTask; }
    public override Task BeforeSideTurnStart(PlayerChoiceContext c, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState) { SpellUsed = false; return Task.CompletedTask; }
    public override async Task AfterSideTurnEnd(PlayerChoiceContext c, CombatSide side, IEnumerable<Creature> participants) { if (side != Owner.Side) await PowerCmd.Decrement(this); }
}

[RegisterPower]
public sealed class KonoeAttackInterruptedPower : KonoeRarePower, IAttackHitHookListener
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Single;
    public Task BeforeAttackHit(AttackHitContext context)
    {
        if (context.Dealer == Owner) context.Targets = [];
        return Task.CompletedTask;
    }
    public override async Task AfterSideTurnEnd(PlayerChoiceContext c, CombatSide side, IEnumerable<Creature> participants) { if (side == Owner.Side) await PowerCmd.Remove(this); }
}

[RegisterPower]
public sealed class KonoeAutomaticChantPower : KonoeRarePower
{
    private CardModel? _source;
    public override Task AfterApplied(Creature? applier, CardModel? source) { _source = source; return Task.CompletedTask; }
    public override async Task BeforeSideTurnEnd(PlayerChoiceContext c, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != Owner.Side || Owner.Player is not { } p || Owner.CombatState is not { } combat) return;
        var source = _source ?? combat.CreateCard(ModelDb.Card<KonoeBlackStone>(), p);
        for (int i = 0; i < Amount && Owner.IsAlive && combat.HittableEnemies.Any(); i++)
        {
            var target = KonoeRareSupport.RandomTarget(p);
            await KonoeRareSupport.Current(c, source, KonoeRareSupport.AutomaticPlay(source, target), retainSpellDraws: true);
        }
    }
}

[RegisterPower]
public sealed class KonoeSpellPermanencePower : KonoeRarePower
{
    [SavedProperty] public bool Used { get; set; }
    public override Task AfterApplied(Creature? applier, CardModel? source) { Used = Owner.GetPower<KonoeElementsPower>()?.SpellsThisTurn > 0; return Task.CompletedTask; }
    public override Task BeforeSideTurnStart(PlayerChoiceContext c, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState) { Used = false; return Task.CompletedTask; }
}

// 红莲魔女形态：每回合第一次攻击追加一次当前组合魔法。
[RegisterPower]
public sealed class KonoeCrimsonWitchFormPower : KonoeRarePower
{
    [SavedProperty] public bool Used { get; set; }
    public override Task BeforeSideTurnStart(PlayerChoiceContext c, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState) { Used = false; return Task.CompletedTask; }
    public override async Task AfterCardPlayed(PlayerChoiceContext c, CardPlay play)
    {
        if (Used || play.Player != Owner.Player || play.Card.Type != CardType.Attack) return;
        if (Owner.CombatState is not { HittableEnemies: { } enemies } || !enemies.Any()) return;
        Used = true;
        await KonoeRareSupport.Current(c, play.Card, play);
    }
}

[RegisterPower]
public sealed class KonoeWitchScalesPower : KonoeRarePower
{
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;
    [SavedProperty] public int Heat { get; set; }
    public override MegaCrit.Sts2.Core.Localization.LocString Description => new MegaCrit.Sts2.Core.Localization.LocString("powers", Id.Entry + ".description").AddValue("Damage", Amount).AddValue("Heat", Heat);
    public override async Task AfterDamageReceived(PlayerChoiceContext c, Creature target, DamageResult r, ValueProp props, Creature? dealer, CardModel? source)
    {
        if (target != Owner || r.UnblockedDamage <= 0 || !props.IsCardOrMonsterMove() || Owner.Player is not { } p) return;
        if (dealer is { IsAlive:true } && dealer != Owner) await CreatureCmd.Damage(c, dealer, Amount, ValueProp.Unpowered, Owner);
        if (Owner.IsAlive) await KonoeHeat.Gain(p, Heat);
    }
}

[RegisterPower]
public sealed class KonoeSpellSublimationPower : KonoeRarePower
{
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;
    [SavedProperty] public bool Used { get; set; }
    public override Task AfterApplied(Creature? applier, CardModel? source) { Used = Owner.GetPower<KonoeElementsPower>()?.SpellsThisTurn > 0; return Task.CompletedTask; }
    internal async Task Trigger(PlayerChoiceContext c, int degree, bool retainDrawnCards = false)
    {
        if (Used || Owner.Player is not { } p) return;
        Used = true;
        await PlayerCmd.GainEnergy(1, p);
        if (degree == 3)
            foreach (var card in await CardPileCmd.Draw(c, Amount, p))
                if (retainDrawnCards) CardCmd.ApplySingleTurnRetain(card);
    }
    public override Task BeforeSideTurnStart(PlayerChoiceContext c, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState) { Used = false; return Task.CompletedTask; }
}

[RegisterPower]
public sealed class KonoeWitchProtectionPower : KonoeRarePower, IAttackHitHookListener
{
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;
    [SavedProperty] public bool Used { get; set; }
    public async Task BeforeAttackHit(AttackHitContext context)
    {
        if (Used || !Owner.IsAlive || context.Dealer?.Side == Owner.Side) return;
        var ally = context.Targets.FirstOrDefault(t => t.IsAlive && t.Player != null && t.Player != Owner.Player && t.Side == Owner.Side);
        if (ally == null) return;
        Used = true;
        var shield = (KonoeProtectedAttackPower)ModelDb.Power<KonoeProtectedAttackPower>().ToMutable(); shield.Protect(context.Attack);
        await PowerCmd.Apply(new BlockingPlayerChoiceContext(), shield, ally, 1, Owner, null);
        await CreatureCmd.Damage(new BlockingPlayerChoiceContext(), Owner, Amount, ValueProp.Unblockable | ValueProp.Unpowered, Owner);
    }
    public override Task BeforeSideTurnStart(PlayerChoiceContext c, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState) { Used = false; return Task.CompletedTask; }
}

[RegisterPower]
public sealed class KonoeProtectedAttackPower : KonoeRarePower, IAttackHitHookListener
{
    private AttackCommand? _attack;
    private bool _active;
    private Stack<bool> _scopes = new();
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;
    protected override bool IsVisibleInternal => false;
    internal void Protect(AttackCommand attack) { _attack = attack; _active = true; }
    public Task BeforeAttackHit(AttackHitContext context)
    {
        _scopes.Push(_active); _active = ReferenceEquals(_attack, context.Attack);
        return Task.CompletedTask;
    }
    public Task AfterAttackHit(AttackHitContext context)
    {
        _active = _scopes.Count > 0 ? _scopes.Pop() : false;
        return Task.CompletedTask;
    }
    public override decimal ModifyDamageCap(Creature? target, ValueProp props, Creature? dealer, CardModel? source, CardPlay? play)
        => target == Owner && _active && dealer == _attack?.Attacker && props.IsCardOrMonsterMove() ? 0 : decimal.MaxValue;
    public override async Task AfterAttack(PlayerChoiceContext c, AttackCommand command) { if (ReferenceEquals(_attack, command)) await PowerCmd.Remove(this); }
    protected override void DeepCloneFields() { base.DeepCloneFields(); _scopes = new(); _active = false; _attack = null; }
}
