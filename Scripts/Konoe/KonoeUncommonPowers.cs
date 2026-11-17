using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Konoe;

public abstract class KonoeBluePower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerAssetProfile AssetProfile => KonoePowerArt.For("guard");
}

[RegisterPower]
public sealed class KonoeStoragePower : KonoeBluePower
{
    [SavedProperty] public int PackedElements { get; set; }
    public override PowerStackType StackType => PowerStackType.Single;
    public override LocString Description { get { var text = new LocString("powers", Id.Entry + ".description"); text.Add("Elements", PackedElements == 0 ? new LocString("cards", "KONOE_STORAGE_EMPTY").GetFormattedText() : KonoeBlueSupport.ElementText(PackedElements)); return text; } }
}

[RegisterPower]
public sealed class KonoeDelayedElementPower : KonoeBluePower
{
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;
    [SavedProperty] public int Element { get; set; }
    [SavedProperty] public int DueTurn { get; set; }
    public override LocString Description { get { var text = new LocString("powers", Id.Entry + ".description"); text.Add("Element", KonoeBlueSupport.ElementText(Element)); return text; } }
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext c, Player p)
    {
        if (p != Owner.Player || p.PlayerCombatState!.TurnNumber < DueTurn) return;
        int amount = Amount; var element = (KonoeElement)Element;
        await PowerCmd.Remove(this);
        for (int i = 0; i < amount; i++) await KonoeElements.Gain(c, p, element);
    }
}

[RegisterPower]
public sealed class KonoeTeleportDrawPower : KonoeBluePower
{
    [SavedProperty] public bool Damaged { get; set; }
    [SavedProperty] public int DueTurn { get; set; }
    public override Task AfterApplied(Creature? applier, CardModel? source)
    {
        DueTurn = Owner.Player!.PlayerCombatState!.TurnNumber + 1;
        Damaged = CombatManager.Instance.History.Entries.OfType<DamageReceivedEntry>().Any(e => e.Receiver == Owner && e.HappenedThisTurn(CombatState) && e.Result.UnblockedDamage > 0 && e.Result.Props.IsCardOrMonsterMove());
        return Task.CompletedTask;
    }
    public override Task AfterDamageReceived(PlayerChoiceContext c, Creature target, DamageResult r, ValueProp props, Creature? dealer, CardModel? source)
    {
        if (target == Owner && r.UnblockedDamage > 0 && props.IsCardOrMonsterMove()) Damaged = true;
        return Task.CompletedTask;
    }
    public override decimal ModifyHandDraw(Player p, decimal count) => p == Owner.Player && p.PlayerCombatState!.TurnNumber >= DueTurn && !Damaged ? count + Amount : count;
    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (participants.Contains(Owner) && Owner.Player!.PlayerCombatState!.TurnNumber >= DueTurn) await PowerCmd.Remove(this);
    }
}

[RegisterPower]
public sealed class KonoeManaReboundPower : KonoeBluePower
{
    public override async Task AfterDamageReceived(PlayerChoiceContext c, Creature target, DamageResult r, ValueProp props, Creature? dealer, CardModel? source)
    {
        if (target != Owner || dealer is not { IsAlive:true } || dealer.Side == Owner.Side || !props.IsCardOrMonsterMove() || r.BlockedDamage <= 0) return;
        int blocked = r.BlockedDamage * Amount;
        await PowerCmd.Remove(this);
        await CreatureCmd.Damage(c, dealer, blocked, ValueProp.Unpowered, Owner);
    }
    public override async Task AfterSideTurnEnd(PlayerChoiceContext c, CombatSide side, IEnumerable<Creature> participants) { if (side != Owner.Side) await PowerCmd.Remove(this); }
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext c, Player p) { if (p == Owner.Player) await PowerCmd.Remove(this); }
}

[RegisterPower]
public sealed class KonoeSpellTracePower : KonoeBluePower { }

[RegisterPower]
public sealed class KonoeSpellEchoPower : KonoeBluePower
{
    [SavedProperty] public bool Used { get; set; }
    public override Task AfterApplied(Creature? applier, CardModel? source) { Used = Owner.GetPower<KonoeElementsPower>()?.SpellsThisTurn > 0; return Task.CompletedTask; }
    public override Task BeforeSideTurnStart(PlayerChoiceContext c, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState) { Used = false; return Task.CompletedTask; }
}

[RegisterPower]
public sealed class KonoeMagicCurtainPower : KonoeBluePower
{
    public override bool ShouldClearBlock(Creature creature) => creature != Owner;
    public override async Task AfterPreventingBlockClear(AbstractModel preventer, Creature creature)
    {
        // A full-retain effect must never be weakened by this limited-retain power.
        if (preventer != this || creature != Owner || Owner.Powers.Any(p => p != this && !p.ShouldClearBlock(Owner))) return;
        if (Owner.Block > Amount) await CreatureCmd.LoseBlock(new ThrowingPlayerChoiceContext(), Owner, Owner.Block - Amount, null);
    }
}

[RegisterPower]
public sealed class KonoeHeatDebtPower : KonoeBluePower
{
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;
    public override PowerType Type => PowerType.Debuff;
    [SavedProperty] public int DueTurn { get; set; }
    internal async Task<int> ReduceGain(int amount)
    {
        if (Owner.Player?.PlayerCombatState?.TurnNumber != DueTurn) return amount;
        int reduction = Math.Min(amount, Amount);
        if (reduction > 0) await PowerCmd.ModifyAmount(new ThrowingPlayerChoiceContext(), this, -reduction, null, null);
        return amount - reduction;
    }
    public override async Task AfterSideTurnEnd(PlayerChoiceContext c, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == Owner.Side && Owner.Player?.PlayerCombatState?.TurnNumber >= DueTurn) await PowerCmd.Remove(this);
    }
}

[RegisterPower]
public sealed class KonoeOffensiveAfterimagePower : KonoeBluePower
{
    private CardModel? _lastAttack;
    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        _lastAttack = CombatManager.Instance.History.CardPlaysFinished.LastOrDefault(e => e.CardPlay.Player == Owner.Player && e.CardPlay.Card.Type == CardType.Attack && e.HappenedThisTurn(CombatState))?.CardPlay.Card.CreateClone();
        return Task.CompletedTask;
    }
    public override Task AfterCardPlayed(PlayerChoiceContext c, CardPlay p)
    {
        if (p.Player == Owner.Player && p.Card.Type == CardType.Attack) _lastAttack = p.Card.CreateClone();
        return Task.CompletedTask;
    }
    public override async Task BeforeHandDraw(Player p, PlayerChoiceContext c, ICombatState combatState)
    {
        if (p != Owner.Player) return;
        var last = _lastAttack; _lastAttack = null;
        if (last == null) return;
        for (int i = 0; i < Amount; i++) await CardPileCmd.AddGeneratedCardToCombat(last.CreateClone(), PileType.Hand, p);
    }
    protected override void DeepCloneFields() { base.DeepCloneFields(); _lastAttack = _lastAttack?.CreateClone(); }
}

[RegisterPower]
public sealed class KonoeManaOverflowPower : KonoeBluePower
{
    internal async Task Trigger(PlayerChoiceContext c)
    {
        if (Owner.CombatState is not { } combat) return;
        await CreatureCmd.Damage(c, combat.HittableEnemies.ToArray(), Amount, ValueProp.Unpowered, Owner, null, null);
    }
}

// 使魔契约：每回合结束由使魔补一击。
[RegisterPower]
public sealed class KonoeFamiliarPactPower : KonoeBluePower
{
    public override async Task BeforeSideTurnEnd(PlayerChoiceContext c, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != Owner.Side || Owner.Player is not { } p) return;
        if (p.Creature.CombatState is not { HittableEnemies: { } enemies } || !enemies.Any()) return;
        var target = p.RunState.Rng.CombatTargets.NextItem(enemies.ToArray());
        if (target == null) return;
        await CreatureCmd.Damage(c, target, Amount, ValueProp.Unpowered, Owner);
    }
}

[RegisterPower]
public sealed class KonoeInvertingMirrorPower : KonoeBluePower
{
    private Creature? _applier;
    private decimal _reflected;
    private CardModel? _source;
    public override PowerStackType StackType => PowerStackType.Single;
    public override Task BeforePowerAmountChanged(PowerModel power, decimal amount, Creature target, Creature? applier, CardModel? cardSource)
    {
        if (target == Owner) { _applier = applier; _source = cardSource; }
        return Task.CompletedTask;
    }
    public override bool TryModifyPowerAmountReceived(PowerModel power, Creature target, decimal amount, Creature? applier, out decimal modifiedAmount)
    {
        modifiedAmount = amount;
        if (target != Owner || power.GetTypeForAmount(amount) != PowerType.Debuff || !power.IsVisible) return false;
        _applier = applier; _reflected = amount; modifiedAmount = 0; return true;
    }
    public override async Task AfterModifyingPowerAmountReceived(PowerModel power)
    {
        var applier = _applier; var amount = _reflected; var source = _source;
        await PowerCmd.Remove(this);
        if (applier is not { IsAlive:true } || applier == Owner || amount == 0) return;
        // 玩家专属 debuff（如 RingingPower，AfterApplied 里访问 Owner.Player）反射到怪物身上会空引用卡死。
        // 只反射怪物也能安全承载的通用 debuff；其余已在 TryModify 里置 0，直接吞掉不反弹。
        if (applier.IsMonster && power is not (WeakPower or VulnerablePower or PoisonPower or FrailPower)) return;
        var reflected = ModelDb.GetById<PowerModel>(power.Id).ToMutable();
        await PowerCmd.Apply(new ThrowingPlayerChoiceContext(), reflected, applier, amount, Owner, source);
    }
}

// 支配回响：每次打出技能牌都会削弱一名敌人。
[RegisterPower]
public sealed class KonoeDominionEchoPower : KonoeBluePower
{
    public override async Task AfterCardPlayed(PlayerChoiceContext c, CardPlay p)
    {
        if (p.Player != Owner.Player || p.Card.Type != CardType.Skill) return;
        if (Owner.Player is not { } player) return;
        if (player.Creature.CombatState is not { HittableEnemies: { } enemies } || !enemies.Any()) return;
        var target = player.RunState.Rng.CombatTargets.NextItem(enemies.ToArray());
        if (target != null) await PowerCmd.Apply<WeakPower>(c, target, Amount, Owner, p.Card);
    }
}

[RegisterPower]
public sealed class KonoeCrossPressurePower : KonoeBluePower
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerInstanceType InstanceType => PowerInstanceType.InstancedPerApplier;
    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? source, CardPlay? play)
        => target == Owner && dealer?.Player != null && dealer != Applier && dealer.Side == Applier?.Side && props.IsCardOrMonsterMove() ? Amount : 0;
    public override async Task AfterSideTurnEnd(PlayerChoiceContext c, CombatSide side, IEnumerable<Creature> participants) => await PowerCmd.Remove(this);
}

[RegisterPower]
public sealed class KonoeResonantSpellPower : KonoeBluePower
{
    [SavedProperty] public bool AttackUsed { get; set; }
    [SavedProperty] public bool SpellUsed { get; set; }
    public override Task AfterApplied(Creature? applier, CardModel? source)
    {
        SpellUsed = Owner.GetPower<KonoeElementsPower>()?.SpellsThisTurn > 0;
        AttackUsed = CombatManager.Instance.History.CardPlaysFinished.Any(e => e.CardPlay.Player != Owner.Player && e.CardPlay.Player.Creature.Side == Owner.Side && e.CardPlay.Card.Type == CardType.Attack && e.HappenedThisTurn(CombatState));
        return Task.CompletedTask;
    }
    public override async Task AfterCardPlayed(PlayerChoiceContext c, CardPlay p)
    {
        if (p.Player == Owner.Player || p.Player.Creature.Side != Owner.Side || p.Card.Type != CardType.Attack || AttackUsed || Owner.Player is not { } player) return;
        AttackUsed = true;
        for (int i = 0; i < Math.Max(1, Amount / 4); i++) await KonoeElements.Gain(c, player, KonoeBlueSupport.Random(player));
    }
    public override Task BeforeSideTurnStart(PlayerChoiceContext c, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState) { AttackUsed = false; SpellUsed = false; return Task.CompletedTask; }
}
