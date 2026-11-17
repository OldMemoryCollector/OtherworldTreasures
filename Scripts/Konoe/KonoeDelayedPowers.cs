using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Konoe;

[RegisterPower(Inherit = true)]
public abstract class KonoeDelayedSpellPower : ModPowerTemplate
{
    [SavedProperty] public int StoredDamage { get; set; }
    [SavedProperty] public int DamageElementTypes { get; set; }
    [SavedProperty] public int TargetCombatId { get; set; }
    [SavedProperty] public int DealerCombatId { get; set; }
    [SavedProperty] public int FollowupDamage { get; set; }
    [SavedProperty] public int StatusStacks { get; set; }
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;
    public override PowerAssetProfile AssetProfile => KonoePowerArt.For("delayed");
    public override LocString Description => new LocString("powers", Id.Entry + ".description").AddValue("Damage", StoredDamage).AddValue("Turns", Amount).AddValue("Followup", FollowupDamage).AddValue("Stacks", StatusStacks);
    protected Creature[] Enemies() => Owner.CombatState?.GetOpponentsOf(Owner).Where(e => e.IsAlive).ToArray() ?? [];
    protected Creature? LockedTarget() => Owner.CombatState?.Enemies.FirstOrDefault(e => e.IsAlive && e.CombatId == unchecked((uint)TargetCombatId));
    protected async Task Damage(PlayerChoiceContext c, IEnumerable<Creature> targets, int damage, bool unblockable = false, KonoeDamageType type = KonoeDamageType.None)
        => await CreatureCmd.Damage(c, targets, damage, KonoeDamage.WithTypes(ValueProp.Unpowered | (unblockable ? ValueProp.Unblockable : 0), type | (KonoeDamageType)DamageElementTypes), Owner);
}

// The electric orb is attached to its target, resolving immediately before its move.
public sealed class KonoeAmethystEchoPower : KonoeDelayedSpellPower
{
    public override PowerType Type => PowerType.Debuff;
    internal async Task ResolveBeforeMove(PlayerChoiceContext c)
    {
        var dealer = Owner.CombatState?.Players.Select(p => p.Creature).FirstOrDefault(p => p.CombatId == unchecked((uint)DealerCombatId));
        int damage = StoredDamage, stacks = StatusStacks;
        await PowerCmd.Remove(this);
        if (!Owner.IsAlive) return;
        await CreatureCmd.Damage(c, Owner, damage, KonoeDamage.WithTypes(ValueProp.Unpowered, (KonoeDamageType)DamageElementTypes), dealer!);
        if (Owner.IsAlive) await PowerCmd.Apply<KonoeKnockdownPower>(c, Owner, stacks, dealer, null);
    }
}

public sealed class KonoeAndraditePower : KonoeDelayedSpellPower
{
    [SavedProperty] public int Growth { get; set; } = 4;
    [SavedProperty] public int Delays { get; set; }
    public override int DisplayAmount => StoredDamage;
    public override async Task BeforeSideTurnEnd(PlayerChoiceContext c, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != Owner.Side || !participants.Contains(Owner) || !Owner.IsAlive || Owner.Player is not { } player || Owner.CombatState is not { } combat) return;
        var detonate = combat.CreateCard(ModelDb.Card<KonoeDetonateChoice>(), player);
        detonate.DynamicVars.Damage.BaseValue = StoredDamage;
        detonate.DynamicVars.Damage.Props = ValueProp.Unpowered;
        var delay = combat.CreateCard(ModelDb.Card<KonoeDelayChoice>(), player);
        delay.DynamicVars.Damage.BaseValue = StoredDamage + Growth;
        delay.DynamicVars.Damage.Props = ValueProp.Unpowered;
        var choice = await CardSelectCmd.FromChooseACardScreen(c, new[] { detonate, delay }, player);
        if (choice is KonoeDetonateChoice) { await PowerCmd.Remove(this); await Damage(c, Enemies(), StoredDamage); }
        else if (choice is KonoeDelayChoice) { StoredDamage += Growth; Delays++; }
    }
    public override async Task AfterDamageReceived(PlayerChoiceContext c, Creature target, DamageResult r, ValueProp props, Creature? dealer, CardModel? source)
    {
        if (target == Owner && r.UnblockedDamage > 0 && props.IsCardOrMonsterMove()) await PowerCmd.Remove(this);
    }
}

public sealed class KonoeMorganitePower : KonoeDelayedSpellPower
{
    [SavedProperty] public int Phase { get; set; }
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext c, Player player)
    {
        if (player != Owner.Player || Phase != 0) return;
        Phase = 1;
        await Damage(c, Enemies(), StoredDamage);
    }
    public override async Task BeforeSideTurnEnd(PlayerChoiceContext c, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != Owner.Side || !participants.Contains(Owner) || Phase != 1) return;
        await PowerCmd.Remove(this);
        await Damage(c, Enemies(), FollowupDamage);
    }
}

public sealed class KonoeNephritePower : KonoeDelayedSpellPower
{
    public override PowerType Type => PowerType.Debuff;
    public override async Task AfterSideTurnEnd(PlayerChoiceContext c, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Enemy) return;
        if (Amount > 1) { await PowerCmd.Decrement(this); return; }
        var dealer = Owner.CombatState?.Players.Select(p => p.Creature).FirstOrDefault(p => p.CombatId == unchecked((uint)DealerCombatId));
        int damage = StoredDamage;
        await PowerCmd.Remove(this);
        if (Owner.IsAlive) await CreatureCmd.Damage(c, Owner, damage, KonoeDamage.WithTypes(ValueProp.Unpowered | ValueProp.Unblockable, (KonoeDamageType)DamageElementTypes), dealer!);
    }
    public override async Task AfterDamageReceived(PlayerChoiceContext c, Creature target, DamageResult r, ValueProp props, Creature? dealer, CardModel? source)
    {
        if (target.CombatId == unchecked((uint)DealerCombatId) && r.UnblockedDamage > 0 && props.IsCardOrMonsterMove()) await PowerCmd.Remove(this);
    }
}

public sealed class KonoeFireRainPower : KonoeDelayedSpellPower
{
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext c, Player player)
    {
        if (player != Owner.Player) return;
        var locked = LockedTarget();
        if (locked?.Powers.OfType<KonoeRainMarkPower>().FirstOrDefault(p => p.CasterId == unchecked((int)Owner.CombatId!.Value) && p.Amount == FollowupDamage) is { } mark) await PowerCmd.Remove(mark);
        await PowerCmd.Remove(this);
        await Damage(c, Enemies(), StoredDamage, type: KonoeDamageType.Fire);
        if (locked is { IsAlive: true }) await Damage(c, new[] { locked }, FollowupDamage, type: KonoeDamageType.Fire);
    }
}

[RegisterPower]
public sealed class KonoeGravityFieldPower : ModPowerTemplate
{
    [SavedProperty] public int Reduction { get; set; } = 4;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerAssetProfile AssetProfile => KonoePowerArt.For("knockdown");
    public override LocString Description => new LocString("powers", Id.Entry + ".description").AddValue("Turns", Amount).AddValue("Reduction", Reduction);
    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? source, MegaCrit.Sts2.Core.Entities.Cards.CardPlay? play)
        => dealer != null && dealer.Side != Owner.Side && props.IsPoweredAttack() ? -Reduction : 0;
    public override bool TryModifyPowerAmountReceived(PowerModel power, Creature target, decimal amount, Creature? applier, out decimal modified)
    {
        modified = amount;
        if (target.Side == Owner.Side || power is not KonoeAirbornePower || amount <= 0) return false;
        modified = 0; return true;
    }
    public override async Task AfterSideTurnEnd(PlayerChoiceContext c, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Enemy) await PowerCmd.Decrement(this);
    }
}

[RegisterPower]
public sealed class KonoeFairyPower : ModPowerTemplate
{
    [SavedProperty] public int StoredDamage { get; set; }
    [SavedProperty] public int DamageElementTypes { get; set; }
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;
    public override PowerAssetProfile AssetProfile => KonoePowerArt.For("fairy");
    public override LocString Description => new LocString("powers", Id.Entry + ".description").AddValue("Damage", StoredDamage).AddValue("Turns", Amount);
    public override async Task BeforeSideTurnEnd(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != Owner.Side || !participants.Contains(Owner) || Owner.CombatState is not { } combat) return;
        var targets = combat.Enemies.Where(e => e.IsAlive).ToArray();
        if (targets.Length != 0)
        {
            var target = combat.RunState.Rng.CombatTargets.NextItem(targets)!;
            await CreatureCmd.Damage(context, target, StoredDamage, KonoeDamage.WithTypes(ValueProp.Unpowered, (KonoeDamageType)DamageElementTypes), Owner);
        }
        await PowerCmd.Decrement(this);
    }
}

[RegisterPower]
public sealed class KonoeRainMarkPower : ModPowerTemplate
{
    [SavedProperty] public int CasterId { get; set; }
    public override PowerType Type => PowerType.None;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;
    public override PowerAssetProfile AssetProfile => KonoePowerArt.For("counter");
}
