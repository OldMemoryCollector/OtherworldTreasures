using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Models;
using STS2RitsuLib.Combat.SecondaryResources;

namespace OtherworldTreasures.Scripts.Konoe;

[RegisterPower]
public sealed class KonoeElementsPower : ModPowerTemplate, ISecondaryResourceHookListener
{
    public override PowerType Type => PowerType.None;
    public override PowerStackType StackType => PowerStackType.Single;
    protected override bool IsVisibleInternal => false;
    [SavedProperty] public int PackedElements { get; set; }
    [SavedProperty] public bool StarterChoiceMade { get; set; }
    [SavedProperty] public bool FirstCardElementEchoUsed { get; set; }
    [SavedProperty] public int SpellsThisTurn { get; set; }
    [SavedProperty] public int HeatSpendsThisTurn { get; set; }
    public Task AfterSecondaryResourceSpent(SecondaryResourceSpendContext context) { if (context.Player == Owner.Player && context.Definition.Id == KonoeHeat.ResourceId && context.Amount > 0) HeatSpendsThisTurn++; return Task.CompletedTask; }
    public override Task BeforeSideTurnStart(PlayerChoiceContext context, MegaCrit.Sts2.Core.Combat.CombatSide side, IReadOnlyList<MegaCrit.Sts2.Core.Entities.Creatures.Creature> participants, MegaCrit.Sts2.Core.Combat.ICombatState combatState) { SpellsThisTurn = 0; HeatSpendsThisTurn = 0; return Task.CompletedTask; }
    // Presentation only: even F/F/F -> F/F/F must show the newly gained ribbon.
    public int RibbonGainSequence { get; set; }
}

public interface IKonoeElementSource
{
    IReadOnlyList<KonoeElement> GrantedElements { get; }
}

public static class KonoeElements
{
    public static int Get(Player? player) => player?.Creature.GetPower<KonoeElementsPower>()?.PackedElements ?? 0;
    public static int GainSequence(Player? player) => player?.Creature.GetPower<KonoeElementsPower>()?.RibbonGainSequence ?? 0;
    internal static async Task<KonoeElementsPower?> EnsureState(PlayerChoiceContext context, Player player)
    {
        if (player.Creature.CombatState == null || !player.Creature.IsAlive) return null;
        return player.Creature.GetPower<KonoeElementsPower>() ?? await PowerCmd.Apply<KonoeElementsPower>(context, player.Creature, 1, player.Creature, null, true);
    }
    public static async Task Gain(PlayerChoiceContext context, Player player, KonoeElement element, CardModel? source = null)
    {
        if (player.Creature.CombatState == null || !player.Creature.IsAlive) return;
        var state = await EnsureState(context, player);
        if (state == null) return;
        await Append();
        if (source?.Type == CardType.Attack && KonoeRareSupport.Overdrive(player)) await Append();
        // The combat power preserves this flag across save/continue and spell consumption.
        // Appending directly also prevents the extra element from triggering itself.
        if (source != null && !state.FirstCardElementEchoUsed && player.Relics.OfType<KonoeSagesBook>().FirstOrDefault() is { } relic)
        {
            state.FirstCardElementEchoUsed = true;
            await Append();
            relic.Flash();
        }
        Refresh(player);

        async Task Append()
        {
            bool replaced = KonoeElementRules.Decode(state.PackedElements).Count == 3;
            state.PackedElements = KonoeElementRules.Append(state.PackedElements, element);
            state.RibbonGainSequence++;
            if (replaced && player.Creature.GetPower<KonoeManaOverflowPower>() is { } overflow) await overflow.Trigger(context);
        }
    }
    internal static void Consume(Player player)
    {
        if (player.Creature.GetPower<KonoeElementsPower>() is { } power) power.PackedElements = 0;
        Refresh(player);
    }
    internal static void Refresh(Player player)
    {
        if (player.PlayerCombatState is not { } combat) return;
        foreach (var card in combat.AllPiles.SelectMany(p => p.Cards).OfType<KonoeBlackStone>()) card.RefreshForm();
        foreach (var card in combat.AllPiles.SelectMany(p => p.Cards).OfType<KonoeAbyssalFormula>()) card.RefreshForm();
        combat.RecalculateCardValues();
    }
}

[RegisterSingleton]
public sealed class KonoeElementCardHooks : HookedSingletonModel
{
    public KonoeElementCardHooks() : base(HookType.Combat) { }
    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
    {
        if (play.Card is IKonoeElementSource source)
            foreach (var element in source.GrantedElements) await KonoeElements.Gain(context, play.Player, element, play.Card);
    }
}
