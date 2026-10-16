using System.Text.Json;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using OtherworldTreasures.Scripts.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Powers;

public abstract class MartialTrainingPower : ModPowerTemplate
{
    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://OtherworldTreasures/images/placeholders/missing_power.png",
        BigIconPath: "res://OtherworldTreasures/images/placeholders/missing_power.png");
}

[RegisterPower]
public sealed class KaiokenPower : MartialTrainingPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? source, CardPlay? play) => dealer == Owner ? 1 + Amount / 100m : 1;

    // 气焰挂在角色节点上，随战斗场景销毁；能力移除或战斗结束时主动熄灭。
    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        UI.MartialArtsVfx.EnsureKaiokenAura(Owner);
        return Task.CompletedTask;
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        UI.MartialArtsVfx.RemoveKaiokenAura(oldOwner);
        return Task.CompletedTask;
    }

    // 战斗场景重建（读档等）后气焰节点可能已丢失，回合开始时补挂。
    public override Task AfterPlayerTurnStartEarly(PlayerChoiceContext context, Player player)
    {
        if (player.Creature == Owner) UI.MartialArtsVfx.EnsureKaiokenAura(Owner);
        return Task.CompletedTask;
    }
}

[RegisterPower]
public sealed class KaiokenDoomPower : MartialTrainingPower
{
    // A mandatory payment consequence, not an affliction Artifact can prevent.
    public override PowerType Type => PowerType.None;
    public override PowerStackType StackType => PowerStackType.Single;
    public override async Task AfterPlayerTurnStartEarly(PlayerChoiceContext context, Player player)
    {
        if (player.Creature == Owner) await CreatureCmd.Kill(Owner, force: true);
    }
}

public sealed class PendingSpiritBomb
{
    public int Id { get; set; }
    public int Energy { get; set; }
    public List<ulong> Waiting { get; set; } = new();
}

[RegisterPower]
public sealed class SpiritBombPower : MartialTrainingPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    [SavedProperty] public string PendingJson { get; set; } = "[]";
    private List<PendingSpiritBomb> Read() => JsonSerializer.Deserialize<List<PendingSpiritBomb>>(PendingJson)!;
    private void Write(List<PendingSpiritBomb> bombs) => PendingJson = JsonSerializer.Serialize(bombs);

    public override Task AfterPlayerTurnStartEarly(PlayerChoiceContext context, Player player)
    {
        if (player.Creature == Owner)
            foreach (var bomb in Read()) UI.MartialArtsVfx.BombBegin(player, bomb.Id, bomb.Energy);
        return Task.CompletedTask;
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        UI.MartialArtsVfx.BombCancelAll(oldOwner);
        return Task.CompletedTask;
    }

    public override async Task BeforeDeath(Creature creature)
    {
        if (creature != Owner || creature.CombatState == null) return;
        UI.MartialArtsVfx.BombCancelAll(Owner);
        Write(new());
        foreach (var player in creature.CombatState.Players)
        {
            var offers = player.PlayerCombatState?.AllCards.OfType<DonateEnergy>()
                .Where(c => c.CasterId == Owner.Player!.NetId).ToList();
            if (offers?.Count > 0)
                foreach (var id in offers.Select(c => c.BombId).Distinct()) await RemoveOffers(player, Owner.Player!.NetId, id);
        }
    }

    internal static async Task Begin(PlayerChoiceContext context, Player caster, CardModel source)
    {
        var power = caster.Creature.GetPower<SpiritBombPower>() ??
            await PowerCmd.Apply<SpiritBombPower>(context, caster.Creature, 1, caster.Creature, source);
        if (power == null) return;
        var bombs = power.Read();
        var bomb = new PendingSpiritBomb { Id = Events.TrainingProgress.NewBombId(caster) };
        foreach (var teammate in caster.Creature.CombatState!.Players.Where(p => p != caster && p.Creature.IsAlive))
        {
            if (CombatManager.Instance.IsPlayerReadyToEndTurn(teammate)) continue;
            bomb.Waiting.Add(teammate.NetId);
        }
        bombs.Add(bomb);
        power.Write(bombs);
        UI.MartialArtsVfx.BombBegin(caster, bomb.Id);
        foreach (var netId in bomb.Waiting)
        {
            var teammate = caster.RunState.Players.Single(p => p.NetId == netId);
            var card = caster.Creature.CombatState!.CreateCard<DonateEnergy>(teammate);
            card.CasterId = caster.NetId;
            card.BombId = bomb.Id;
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, caster);
        }
        await power.ReleaseReady(context);
    }

    internal static async Task Contribute(PlayerChoiceContext context, Player player, ulong casterId, int bombId, int energy)
    {
        var power = player.RunState.Players.FirstOrDefault(p => p.NetId == casterId)?.Creature.GetPower<SpiritBombPower>();
        if (power == null) return;
        var bombs = power.Read();
        var bomb = bombs.FirstOrDefault(b => b.Id == bombId);
        if (bomb == null || !bomb.Waiting.Remove(player.NetId)) return;
        bomb.Energy += Math.Max(energy, 0);
        power.Write(bombs);
        UI.MartialArtsVfx.BombContribute(casterId, bombId, player.Creature, energy);
        await RemoveOffers(player, casterId, bombId);
        await power.ReleaseReady(context);
    }

    public override async Task AfterAutoPostPlayPhaseEntered(PlayerChoiceContext context, Player player)
    {
        var bombs = Read();
        var ended = bombs.Where(b => b.Waiting.Remove(player.NetId)).Select(b => b.Id).ToList();
        Write(bombs);
        foreach (var id in ended) await RemoveOffers(player, Owner.Player!.NetId, id);
        await ReleaseReady(context);
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
    {
        var player = play.Player;
        if (player.PlayerCombatState!.Hand.Cards.Count >= CardPile.MaxCardsInHand) return;
        var waiting = Read().Where(b => b.Waiting.Contains(player.NetId)).Select(b => b.Id).ToHashSet();
        // Native hand overflow sends a generated offer to discard. Bring it back as
        // soon as playing another card makes room, within the same contribution turn.
        var offers = player.PlayerCombatState.AllCards.OfType<DonateEnergy>().Where(c =>
            c.CasterId == Owner.Player!.NetId && waiting.Contains(c.BombId) &&
            (c.Pile?.Type == PileType.Discard || c.Pile?.Type == Entry.OmnitrixStashPile)).ToList();
        foreach (var offer in offers)
        {
            if (player.PlayerCombatState.Hand.Cards.Count >= CardPile.MaxCardsInHand) break;
            await CardPileCmd.Add(offer, PileType.Hand);
        }
    }

    private static async Task RemoveOffers(Player player, ulong casterId, int bombId)
    {
        var offers = player.PlayerCombatState!.AllCards.OfType<DonateEnergy>()
            .Where(c => c.CasterId == casterId && c.BombId == bombId && c.Pile?.Type != PileType.Play).ToList();
        if (offers.Count == 0) return;
        var handCards = offers.Where(c => c.Pile?.Type == PileType.Hand).ToList();
        await CardPileCmd.RemoveFromCombat(offers, skipVisuals: true);
        Alien.HandUiSync.RemoveFromHandUi(handCards);
    }

    internal static async Task EndContributionTurn(PlayerChoiceContext context, Player player)
    {
        if (player.Creature.CombatState is not { } combat) return;
        foreach (var caster in combat.Players.ToList())
            if (caster.Creature.GetPower<SpiritBombPower>() is { } power)
                await power.AfterAutoPostPlayPhaseEntered(context, player);
    }

    public override async Task AfterDeath(PlayerChoiceContext context, Creature creature, bool prevented, float duration)
    {
        if (creature.IsAlive) return;
        var bombs = Read();
        foreach (var bomb in bombs) if (creature.Player != null) bomb.Waiting.Remove(creature.Player.NetId);
        Write(bombs);
        await ReleaseReady(context);
    }

    private async Task ReleaseReady(PlayerChoiceContext context)
    {
        var combat = Owner.CombatState;
        if (combat == null) return;
        var bombs = Read();
        var ready = bombs.Where(b => b.Waiting.Count == 0).ToList();
        foreach (var bomb in ready) bombs.Remove(bomb);
        Write(bombs); // Claim before damage/hooks can re-enter the coordinator.
        foreach (var bomb in ready)
        {
            foreach (var player in combat.Players)
            {
                await RemoveOffers(player, Owner.Player!.NetId, bomb.Id);
            }
            await UI.MartialArtsVfx.BombRelease(Owner.Player!, bomb.Id, bomb.Energy);
            // No dealer/source: Strength, Vulnerable-on-dealer and Kaioken cannot be added again.
            if (bomb.Energy > 0 && !CombatManager.Instance.IsOverOrEnding)
                await CreatureCmd.Damage(context, combat.Enemies.Where(e => e.IsAlive).ToList(), bomb.Energy * 20m,
                    ValueProp.Unpowered, null, null, null);
        }
        if (Read().Count == 0 && Owner.Powers.Contains(this)) await PowerCmd.Remove(this);
    }
}

// EndPlayerTurnAction is already a synchronized native action. Finish the player's
// contribution inside that action, not in an async-void UI callback or a waiting
// card play. Other players can continue acting while the bomb is pending.
[HarmonyLib.HarmonyPatch(typeof(MegaCrit.Sts2.Core.GameActions.EndPlayerTurnAction), "ExecuteAction")]
internal static class SpiritBombTurnFinished
{
    private static void Postfix(MegaCrit.Sts2.Core.GameActions.EndPlayerTurnAction __instance, ref Task __result)
        => __result = Finish(__result, __instance.OwnerId);

    private static async Task Finish(Task original, ulong playerId)
    {
        await original;
        var combat = CombatManager.Instance.DebugOnlyGetState();
        var player = combat?.Players.FirstOrDefault(p => p.NetId == playerId);
        if (player != null && CombatManager.Instance.IsPlayerReadyToEndTurn(player))
            await SpiritBombPower.EndContributionTurn(new ThrowingPlayerChoiceContext(), player);
    }
}
