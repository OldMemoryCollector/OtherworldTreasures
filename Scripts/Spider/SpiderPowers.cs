using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using OtherworldTreasures.Scripts.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models.Capabilities;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Spider;

public abstract class SpiderPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerAssetProfile AssetProfile => new(IconPath: "res://images/powers/missing_power.png", BigIconPath: "res://images/powers/missing_power.png");
    public override Task AfterCombatEnd(CombatRoom room) => PowerCmd.Remove(this);
}

[RegisterPower]
public sealed class SpiderWebGeneratorPower : SpiderPower
{
    public override Task AfterPlayerTurnStart(PlayerChoiceContext context, Player player) =>
        player.Creature == Owner ? Give(context) : Task.CompletedTask;
    private async Task Give(PlayerChoiceContext context) =>
        await PowerCmd.Apply<SpiderWebPower>(context, Owner, Amount, Owner, null);
}

[RegisterPower]
public sealed class SpiderWebPower : SpiderPower
{
    private HashSet<CardPlay> _boosted = new();
    protected override void DeepCloneFields() { base.DeepCloneFields(); _boosted = new(); }
    public override Task BeforeCardPlayed(CardPlay play)
    {
        if (play.Card.Owner.Creature == Owner && play.Card.Type == CardType.Attack && Amount > _boosted.Count)
            _boosted.Add(play);
        return Task.CompletedTask;
    }
    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? source, CardPlay? play)
    {
        if (dealer != Owner || source?.Type != CardType.Attack || !props.IsPoweredAttack()) return 0m;
        bool boosted = play == null ? Amount > _boosted.Count : _boosted.Contains(play);
        return boosted ? Owner.GetPower<StrengthPower>()?.Amount ?? 0 : 0m;
    }
    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
    {
        // Keep the power attached until every damage segment and target of this play has resolved.
        if (_boosted.Remove(play)) await PowerCmd.Decrement(this);
    }
}

[RegisterPower]
public sealed class SpiderSilkPower : SpiderPower
{
    internal static int Spend(Creature creature, int loss)
    {
        if (!creature.IsAlive || creature.GetPower<SpiderSilkPower>() is not { } power) return 0;
        int spent = SpiderRules.SpendSilk(power.Amount, loss);
        if (spent <= 0) return 0;
        power.SetAmount(power.Amount - spent);
        if (power.Amount <= 0) power.RemoveInternal();
        return spent;
    }
}

[RegisterPower]
public sealed class SpiderEchoPower : SpiderPower
{
    private int _exhaustCount;
    [SavedProperty] public int ExhaustCount { get => _exhaustCount; set { AssertMutable(); _exhaustCount = value; } }
    public override async Task AfterCardExhausted(PlayerChoiceContext context, CardModel card, bool causedByEthereal)
    {
        if (card.Owner.Creature != Owner) return;
        int count = ExhaustCount;
        bool due = SpiderRules.EchoDue(ref count);
        ExhaustCount = count;
        if (!due) return;
        Flash();
        int copies = Amount;
        for (int i = 0; i < copies && !CombatManager.Instance.IsOverOrEnding; i++)
        {
            var copy = card.CreateClone();
            copy.EnergyCost.SetThisCombat(0);
            copy.SetStarCostThisCombat(0);
            copy.RemoveKeyword(CardKeyword.Exhaust);
            copy.AddKeyword(MyKeywords.Remove);
            copy.GetOrCreateCapability<SpiderEchoCopyCapability>();
            await CardPileCmd.AddGeneratedCardToCombat(copy, PileType.Hand, Owner.Player);
        }
    }
}

[RegisterPower]
public sealed class SpiderVenomPower : SpiderPower
{
    public override PowerType Type => PowerType.Debuff;
    internal async Task OnAction(PlayerChoiceContext context)
    {
        if (!Owner.IsAlive || Amount <= 0) return;
        int venom = Amount;
        Flash();
        await PowerCmd.Apply<PoisonPower>(context, Owner, venom, Applier, null);
        await PowerCmd.Decrement(this);
    }
}

[RegisterPower]
public sealed class SpiderHuntPower : SpiderPower
{
    public override async Task AfterCardDrawn(PlayerChoiceContext context, CardModel card, bool fromHandDraw)
    {
        if (card.Owner.Creature != Owner || !Owner.IsAlive || CombatManager.Instance.IsOverOrEnding ||
            Owner.CombatState is not { } combat || Owner.Player is not { } player) return;
        var enemies = combat.HittableEnemies;
        if (enemies.Count > 0)
        {
            var target = player.RunState.Rng.CombatTargets.NextItem(enemies);
            if (target != null) await PowerCmd.Apply<SpiderVenomPower>(context, target, Amount, Owner, null);
        }
    }
    public override async Task AfterCardDiscarded(PlayerChoiceContext context, CardModel card)
    {
        if (card.Owner.Creature != Owner) return;
        for (int i = 0; i < Amount && !CombatManager.Instance.IsOverOrEnding; i++)
            await CardPileCmd.AddToCombatAndPreview<SpiderShiv>(Owner, PileType.Hand, 1, Owner.Player);
    }
    public override Task AfterSideTurnEnd(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants) =>
        side == Owner.Side && participants.Contains(Owner) ? PowerCmd.Remove(this) : Task.CompletedTask;
}

[RegisterPower]
public sealed class SteelSpiderWebPower : SpiderPower { }
