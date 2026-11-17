using System.Runtime.CompilerServices;
using System.Threading;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using OtherworldTreasures.Scripts.Powers;
using STS2RitsuLib.Models.Capabilities;

namespace OtherworldTreasures.Scripts.Spider;

internal sealed class SpiderMoveScope : IDisposable
{
    private static readonly AsyncLocal<SpiderMoveScope?> Current = new();
    private readonly SpiderMoveScope? _previous;
    private readonly Creature _enemy;
    private readonly Dictionary<Creature, AttackCommand> _misses = new();
    internal SpiderMoveScope(Creature enemy) { _previous = Current.Value; _enemy = enemy; Current.Value = this; }
    public void Dispose() => Current.Value = _previous;
    internal static void Record(Creature? enemy, Creature target, AttackCommand? missedAttack)
    {
        if (Current.Value is not { } scope || scope._enemy != enemy) return;
        if (missedAttack == null) scope._misses.Remove(target); else scope._misses[target] = missedAttack;
    }
    internal static bool Missed(Creature? enemy, Creature target)
    {
        if (Current.Value is not { } scope || scope._enemy != enemy || !scope._misses.TryGetValue(target, out var attack)) return false;
        // A later successful segment overrides a previous miss, even if the last Dodge stack was consumed.
        return attack.Results.LastOrDefault()?.All(result => result.Receiver != target) != false;
    }
}

[HarmonyPatch(typeof(MonsterModel), nameof(MonsterModel.PerformMove))]
internal static class SpiderMonsterAction
{
    private static bool Prefix(MonsterModel __instance, ref Task __result)
    {
        if (__instance.Creature is not { Side: CombatSide.Enemy, IsAlive: true } enemy ||
            enemy.CombatState is not { } combat) return true;
        if (!enemy.HasPower<SpiderVenomPower>() && !enemy.HasPower<WebbedPower>() &&
            !combat.Players.Any(player => player.Creature.HasPower<DodgePower>())) return true;
        __result = Perform(__instance);
        return false;
    }
    private static async Task Perform(MonsterModel monster)
    {
        var enemy = monster.Creature;
        var context = new ThrowingPlayerChoiceContext();
        using var scope = new SpiderMoveScope(enemy);
        // Venom always resolves before Webbed can cancel this move.
        if (enemy.GetPower<SpiderVenomPower>() is { } venom) await venom.OnAction(context);
        var web = enemy.GetPower<WebbedPower>();
        if (enemy.IsAlive && !CombatManager.Instance.IsOverOrEnding)
        {
            if (web != null) await web.CheckAction(context);
            await Original(monster);
        }
        if (enemy.GetPower<WebbedPower>() is { } remaining) await PowerCmd.Remove(remaining);
    }
    [HarmonyReversePatch, MethodImpl(MethodImplOptions.NoInlining)]
    private static Task Original(MonsterModel instance) => throw new NotSupportedException("Harmony reverse patch");
}

[HarmonyPatch(typeof(PowerCmd), nameof(PowerCmd.Apply),
    [typeof(PlayerChoiceContext), typeof(PowerModel), typeof(Creature), typeof(decimal), typeof(Creature), typeof(CardModel), typeof(bool)])]
internal static class SpiderDodgeDebuff
{
    private static bool Prefix(PowerModel power, Creature target, Creature? applier, ref Task __result)
    {
        if (power.Type != PowerType.Debuff || applier == null || applier.Side == target.Side || !SpiderMoveScope.Missed(applier, target)) return true;
        __result = Task.CompletedTask;
        return false;
    }
}

[HarmonyPatch(typeof(Creature), nameof(Creature.LoseHpInternal))]
internal static class SpiderSilkHpLoss
{
    private static void Prefix(Creature __instance, ref decimal amount, out int __state)
    {
        int loss = (int)Math.Clamp(amount, 0m, 999999999m);
        __state = SpiderSilkPower.Spend(__instance, loss);
        amount = Math.Max(0m, amount - __state);
    }
    private static void Postfix(DamageResult __result, int __state) => __result.BlockedDamage += __state;
}

[HarmonyPatch(typeof(OrbCmd), "Evoke")]
internal static class SteelSpiderWebEvoke
{
    private static bool Prefix(PlayerChoiceContext choiceContext, Player player, OrbModel evokedOrb, bool dequeue, ref Task __result)
    {
        if (CombatManager.Instance.IsOverOrEnding || player.PlayerCombatState is not { } playerCombat || player.Creature.GetPower<SteelSpiderWebPower>() is not { Amount: > 0 } web ||
            !playerCombat.OrbQueue.Orbs.Contains(evokedOrb)) return true;
        __result = Trigger(choiceContext, player, evokedOrb, dequeue, web.Amount);
        return false;
    }
    private static async Task Trigger(PlayerChoiceContext context, Player player, OrbModel evokedOrb, bool dequeue, int count)
    {
        // Capture before vanilla dequeues the evoked orb. Every later evoke captures its own current queue.
        var playerCombat = player.PlayerCombatState;
        if (playerCombat == null) return;
        var orbs = playerCombat.OrbQueue.Orbs.ToArray();
        for (int i = 0; i < count && !CombatManager.Instance.IsOverOrEnding; i++)
            foreach (var orb in orbs)
                if (playerCombat.OrbQueue.Orbs.Contains(orb))
                    await OrbCmd.Passive(context, orb, null);
        await Original(context, player, evokedOrb, dequeue);
    }
    [HarmonyReversePatch, MethodImpl(MethodImplOptions.NoInlining)]
    private static Task Original(PlayerChoiceContext context, Player player, OrbModel evokedOrb, bool dequeue) => throw new NotSupportedException("Harmony reverse patch");
}

[HarmonyPatch(typeof(CardCmd), nameof(CardCmd.Exhaust))]
internal static class SpiderEtherealAutoPlay
{
    private static bool Prefix(PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal,
        bool skipVisuals, ref Task<CardPileAddResult?> __result)
    {
        if (!causedByEthereal || card.Capability<SpiderEtherealMarkCapability>() is not { } mark) return true;
        mark.RemoveFromOwner();
        __result = Play(choiceContext, card, skipVisuals);
        return false;
    }
    private static async Task<CardPileAddResult?> Play(PlayerChoiceContext context, CardModel card, bool skipVisuals)
    {
        await CardCmd.AutoPlay(context, card, null, skipCardPileVisuals: skipVisuals);
        return null;
    }
}

[HarmonyPatch(typeof(CardModel), nameof(CardModel.Title), MethodType.Getter)]
internal static class SpiderBasicTitle
{
    private static void Postfix(CardModel __instance, ref string __result)
    {
        if (!__instance.IsMutable || __instance.Capability<SpiderStrikeCapability>() == null &&
            __instance.Capability<SpiderDefendCapability>() == null) return;
        var text = new LocString("cards", "OTHERWORLD_TREASURES_SPIDER_BASIC.title");
        text.Add("Title", __result);
        __result = text.GetFormattedText();
    }
}

// Preserve X-card execution while the echo copy pays no energy or stars.
[HarmonyPatch(typeof(CardEnergyCost), nameof(CardEnergyCost.GetAmountToSpend))]
internal static class SpiderEchoFreeCost
{
    private static void Postfix(CardModel ____card, ref int __result)
    { if (____card.IsMutable && ____card.Capability<SpiderEchoCopyCapability>() != null) __result = 0; }
}

[HarmonyPatch(typeof(CardModel), nameof(CardModel.IsUpgradable), MethodType.Getter)]
internal static class SpiderEchoCopyUpgrade
{
    private static void Postfix(CardModel __instance, ref bool __result)
    { if (__result && __instance.IsMutable && __instance.Capability<SpiderEchoCopyCapability>() != null) __result = false; }
}
