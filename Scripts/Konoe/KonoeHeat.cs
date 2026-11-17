using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Models;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using STS2RitsuLib;
using STS2RitsuLib.Combat.SecondaryResources;

namespace OtherworldTreasures.Scripts.Konoe;

internal static class KonoeHeat
{
    internal const int AttackIncome = 8;
    internal const int SpellIncome = 10;
    internal static int DamageIncome(int unblockedDamage) => Math.Max(0, unblockedDamage) / 2;
    internal const string ResourceId = "OTHERWORLD_TREASURES_SECONDARY_RESOURCE_KONOE_HEAT";
    internal static SecondaryResourceDefinition Definition { get; private set; } = null!;
    internal static void Register()
    {
        var registry = RitsuLibFramework.GetSecondaryResourceRegistry(Entry.ModId);
        Definition = registry.Register("konoe_heat", new SecondaryResourceDefinition(
            defaultAmount: 0, baseMaxAmount: 100,
            turnStartPolicy: SecondaryResourceTurnStartPolicy.None,
            persistencePolicy: SecondaryResourcePersistencePolicy.Combat,
            smallIconPath: "res://OtherworldTreasures/images/characters/nine/relic_icon.png",
            largeIconPath: "res://OtherworldTreasures/images/characters/nine/relic.png"));
        // 火热值 HUD 改由 Entry.cs 直接挂到 NCombatUi（RegisterReadyChild，与 PokerPileHud 同构）。
        // 之前走 RitsuLib 的 RegisterCombatUi，其挂载策略会让自定义 HUD 在实机里不出现。
        registry.RegisterCardUi("konoe_heat_card_cost", parent =>
        {
            var cost = NSecondaryResourceCardCostUi.Create(ResourceId, new SecondaryResourceCardCostUiStyle
            {
                IconSize = new Vector2(34, 34), FontSize = 20
            });
            cost.Position = parent.GetNode<TextureRect>("%EnergyIcon").Position + new Vector2(0, 64);
            return cost;
        }, context => context.Node.Refresh(context));
        registry.AlwaysShowInCombatUiForCharacter<Characters.Konoe>(Definition.LocalId);
    }
    internal static int Get(Player player) => SecondaryResourceCmd.Get(player, Definition.Id);
    // Fixed costs use RitsuLib's actual payment ledger. Optional in-effect payments belong
    // to the exact CardPlay, so nested plays and other players never consume each other's mark.
    private static readonly ConditionalWeakTable<CardPlay, object> OptionalSpending = new();
    internal static async Task<bool> Spend(CardPlay play, int amount)
    {
        bool spent = await SecondaryResourceCmd.Spend(play.Player, Definition.Id, amount, play.Card, play.Card);
        if (spent && amount > 0) OptionalSpending.GetValue(play, _ => new object());
        return spent;
    }
    internal static bool SpentHeat(CardPlay play) =>
        play.SecondaryResources().Spent(ResourceId) > 0 || OptionalSpending.TryGetValue(play, out _);
    internal static Task GainFromPlay(CardPlay play, int amount) =>
        SpentHeat(play) ? Task.CompletedTask : Gain(play.Player, amount);
    internal static async Task Gain(Player player, int amount)
    {
        foreach (var debt in player.Creature.Powers.OfType<KonoeHeatDebtPower>().ToArray()) amount = await debt.ReduceGain(amount);
        if (amount > 0) await SecondaryResourceCmd.Gain(player, Definition.Id, amount);
    }
}

// Income shares the normal resource path: cap and Heat Debt apply.
[RegisterSingleton]
public sealed class KonoeHeatIncomeHooks : HookedSingletonModel
{
    public KonoeHeatIncomeHooks() : base(HookType.Combat) { }
    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
    {
        if (play.Player.Character is Characters.Konoe && play.Player.Creature.IsAlive && play.Card.Type == CardType.Attack)
            await KonoeHeat.GainFromPlay(play, KonoeHeat.AttackIncome);
    }
    public override async Task AfterDamageReceived(PlayerChoiceContext context, Creature target, DamageResult result,
        ValueProp props, Creature? dealer, CardModel? source)
    {
        if (target.Player is { Character: Characters.Konoe } player && target.IsAlive && target.CombatState != null)
            await KonoeHeat.Gain(player, KonoeHeat.DamageIncome(result.UnblockedDamage));
    }
}
