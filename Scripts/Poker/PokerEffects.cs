using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using OtherworldTreasures.Scripts.UI;
using STS2RitsuLib.Combat.HandSize;

namespace OtherworldTreasures.Scripts.Poker;

internal static class PokerEffects
{
    internal static string Summary(PokerGroup group, IReadOnlyList<PokerFace> cards)
    {
        int recover = PokerRules.StraightRecoveryCount(group.Indexes.Select(i => cards[i]));
        string flush = FlushSummary(cards[group.Indexes[0]].Suit);
        string effect = group.Pattern switch {
            PokerPattern.HighCard => "造成15点伤害",
            PokerPattern.OnePair => "造成12点伤害；抽2张牌，其中1张本回合耗能变为0",
            PokerPattern.TwoPair => "造成18点伤害；获得12点格挡；抽2张牌，其中1张本回合耗能变为0",
            PokerPattern.ThreeOfAKind => "造成20点伤害；获得2点能量；抽3张牌",
            PokerPattern.Straight => $"造成15点群体伤害；回收至多{recover}张弃牌，本回合耗能变为0",
            PokerPattern.Flush => flush,
            PokerPattern.FullHouse => "获得3点能量、15点格挡；抽5张牌，其中3张本回合耗能变为0",
            PokerPattern.FourOfAKind => "造成80点无视格挡的伤害；获得3点力量",
            PokerPattern.StraightFlush => $"触发顺子（回收{recover}张）与同花效果；造成40点无视格挡的群体伤害。" + flush,
            PokerPattern.RoyalFlush => "触发同花顺全部效果；额外造成150点无视格挡的群体伤害，获得3点能量，手牌本回合耗能全部变为0。" + flush,
            _ => "" };
        return PokerRules.Name(group.Pattern) + "：" + effect;
    }
    private static string FlushSummary(int suit) => suit switch {
        0 => "清除所有敌人格挡，造成30点群体伤害并施加3层易伤",
        1 => "回复15点生命，获得3层再生，清除自身负面状态",
        2 => "获得3点能量；抽4张牌，其中2张本回合耗能变为0",
        _ => "获得35点格挡、2点敏捷" };
    private static bool Active(CardPlay p) => p.Card.Owner.Creature.IsAlive && p.Card.Owner.Creature.CombatState != null && !CombatManager.Instance.IsOverOrEnding;

    internal static async Task Resolve(PlayerChoiceContext context, CardPlay play, IReadOnlyList<PokerFace> snapshot, PokerSelection selection, bool scatterDamage = true)
    {
        var card = play.Card; var player = card.Owner;
        var consumed = selection.AllIndexes.Select(i => snapshot[i]).ToArray();
        // Shield only receives pattern bonuses; ungrouped cards and high cards give it no base damage.
        int baseCards = scatterDamage ? selection.Scatter.Count(i => !snapshot[i].IsJoker) : 0;
        foreach (var group in selection.Groups)
        {
            if (!Active(play)) return;
            var face = snapshot[group.Indexes[0]];
            switch (group.Pattern)
            {
                case PokerPattern.HighCard:
                    if (scatterDamage) await Hit(context, play, Target(play), 15, face);
                    break;
                case PokerPattern.OnePair:
                    await Hit(context, play, Target(play), 12, face);
                    await DrawAndFree(context, play, 2, 1); break;
                case PokerPattern.TwoPair:
                    await Hit(context, play, Target(play), 18, face);
                    if (Active(play)) await CreatureCmd.GainBlock(player.Creature, 12, ValueProp.Move, play);
                    await DrawAndFree(context, play, 2, 1); break;
                case PokerPattern.ThreeOfAKind:
                    await Hit(context, play, Target(play), 20, face);
                    if (Active(play)) { await PlayerCmd.GainEnergy(2, player); await CardPileCmd.Draw(context, 3, player); }
                    break;
                case PokerPattern.Straight: await Straight(context, play, group, snapshot); break;
                case PokerPattern.Flush: await Flush(context, play, face.Suit); break;
                case PokerPattern.FullHouse:
                    await PlayerCmd.GainEnergy(3, player);
                    await DrawAndFree(context, play, 5, 3);
                    if (Active(play)) await CreatureCmd.GainBlock(player.Creature, 15, ValueProp.Move, play);
                    break;
                case PokerPattern.FourOfAKind:
                    await Hit(context, play, Target(play), 80, face, true);
                    if (Active(play)) await PowerCmd.Apply<StrengthPower>(context, player.Creature, 3, player.Creature, card);
                    break;
                case PokerPattern.StraightFlush:
                case PokerPattern.RoyalFlush:
                    await Straight(context, play, group, snapshot);
                    if (Active(play)) await Flush(context, play, face.Suit);
                    if (Active(play)) await HitAll(context, play, 40, face, true);
                    if (group.Pattern == PokerPattern.RoyalFlush && Active(play)) {
                        await HitAll(context, play, 150, face, true);
                        if (Active(play)) {
                            await PlayerCmd.GainEnergy(3, player);
                            foreach (var handCard in PileType.Hand.GetPile(player).Cards) MakeFreeForTurn(handCard);
                        }
                    }
                    break;
            }
        }
        if (!Active(play)) return;
        if (baseCards > 0) await Hit(context, play, Target(play), baseCards * 3, consumed.First(f => !f.IsJoker));
        // Jokers never enter the five-card classifier. Every physical joker counts every other consumed card.
        foreach (var joker in consumed.Where(f => f.Value == 52).Concat(consumed.Where(f => f.Value == 53)))
        {
            int others = consumed.Length - 1;
            for (int i = 0; i < others && Active(play); i++)
            {
                if (joker.Value == 52)
                {
                    var target = RandomEnemy(play); if (target == null) break;
                    await Hit(context, play, target, 1, joker);
                    if (!Active(play) || !target.IsAlive) continue;
                    PowerModel power = player.RunState.Rng.CombatCardGeneration.NextInt(5) switch {
                        0 => ModelDb.Power<WeakPower>(), 1 => ModelDb.Power<VulnerablePower>(), 2 => ModelDb.Power<PoisonPower>(),
                        3 => ModelDb.Power<FrailPower>(), _ => ModelDb.Power<DoomPower>() };
                    await PowerCmd.Apply(context, power.ToMutable(), target, 3, player.Creature, card);
                }
                else
                {
                    PowerModel power = player.RunState.Rng.CombatCardGeneration.NextInt(6) switch {
                        0 => ModelDb.Power<StrengthPower>(), 1 => ModelDb.Power<DexterityPower>(), 2 => ModelDb.Power<RegenPower>(),
                        3 => ModelDb.Power<ArtifactPower>(), 4 => ModelDb.Power<ThornsPower>(), _ => ModelDb.Power<PlatingPower>() };
                    await PowerCmd.Apply(context, power.ToMutable(), player.Creature, 3, player.Creature, card);
                }
            }
            if (joker.Value == 53 && others > 0 && Active(play)) await HitAll(context, play, others, joker);
        }
        if (Active(play) && consumed.Any(f => f.Value == 52) && consumed.Any(f => f.Value == 53))
            await HitAll(context, play, 60, new PokerFace(53), true);
    }
    private static void MakeFreeForTurn(CardModel card)
    {
        card.EnergyCost.SetThisTurn(0, reduceOnly: true);
        card.SetStarCostThisTurn(0);
    }
    private static async Task DrawAndFree(PlayerChoiceContext context, CardPlay play, int count, int freeCount)
    {
        if (!Active(play)) return;
        var drawn = (await CardPileCmd.Draw(context, count, play.Card.Owner)).Where(c => c.Pile?.Type == PileType.Hand && !c.EnergyCost.CostsX).ToList();
        // Prefer useful discounts, and only modify cards actually drawn by this reward.
        var candidates = drawn.Where(c => c.EnergyCost.GetAmountToSpend() > 0).ToList();
        for (int i = 0; i < freeCount && candidates.Count > 0; i++) {
            int slot = play.Card.Owner.RunState.Rng.CombatCardSelection.NextInt(candidates.Count);
            MakeFreeForTurn(candidates[slot]); candidates.RemoveAt(slot);
        }
    }
    private static async Task Straight(PlayerChoiceContext context, CardPlay play, PokerGroup group, IReadOnlyList<PokerFace> cards)
    {
        await HitAll(context, play, 15, cards[group.Indexes[0]]);
        if (!Active(play)) return;
        var player = play.Card.Owner;
        int slots = Math.Max(0, MaxHandSizeCalculator.Calculate(player) - PileType.Hand.GetPile(player).Cards.Count);
        var discard = PileType.Discard.GetPile(player).Cards.ToArray();
        int count = Math.Min(Math.Min(PokerRules.StraightRecoveryCount(group.Indexes.Select(i => cards[i])), slots), discard.Length);
        if (count == 0) return;
        var prefs = new CardSelectorPrefs(new LocString("cards", "OTHERWORLD_TREASURES_CARD_FLYING_CARDS.selectionScreenPrompt"), 0, count) { Cancelable = true, RequireManualConfirmation = true };
        var chosen = (await CardSelectCmd.FromSimpleGrid(context, discard, player, prefs)).ToArray();
        foreach (var recovered in chosen) {
            if (!Active(play)) break;
            await CardPileCmd.Add(recovered, PileType.Hand);
            if (recovered.Pile?.Type == PileType.Hand) MakeFreeForTurn(recovered);
        }
    }
    private static async Task Flush(PlayerChoiceContext context, CardPlay play, int suit)
    {
        if (!Active(play)) return;
        var player = play.Card.Owner;
        switch (suit) {
            case 0:
                var enemies = player.Creature.CombatState!.HittableEnemies.ToArray();
                foreach (var enemy in enemies) await CreatureCmd.LoseBlock(context, enemy, enemy.Block, player.Creature);
                await HitAll(context, play, 30, new PokerFace(0));
                foreach (var enemy in enemies.Where(e => e.IsAlive)) {
                    if (!Active(play)) break;
                    await PowerCmd.Apply<VulnerablePower>(context, enemy, 3, player.Creature, play.Card);
                }
                break;
            case 1:
                await CreatureCmd.Heal(player.Creature, 15);
                if (!Active(play)) return;
                await PowerCmd.Apply<RegenPower>(context, player.Creature, 3, player.Creature, play.Card);
                foreach (var debuff in player.Creature.Powers.Where(p => p.TypeForCurrentAmount == PowerType.Debuff).ToArray()) {
                    if (!Active(play)) break;
                    await PowerCmd.Remove(debuff);
                }
                break;
            case 2:
                await PlayerCmd.GainEnergy(3, player); await DrawAndFree(context, play, 4, 2); break;
            case 3:
                await CreatureCmd.GainBlock(player.Creature, 35, ValueProp.Move, play);
                if (Active(play)) await PowerCmd.Apply<DexterityPower>(context, player.Creature, 2, player.Creature, play.Card);
                break;
        }
    }
    internal static Creature? RandomEnemy(CardPlay p)
    {
        var enemies = p.Card.Owner.Creature.CombatState?.HittableEnemies;
        return enemies is { Count: > 0 } ? enemies[p.Card.Owner.RunState.Rng.CombatTargets.NextInt(enemies.Count)] : null;
    }
    private static Creature? Target(CardPlay p) => p.Target is { IsHittable: true } ? p.Target : RandomEnemy(p);
    internal static async Task Hit(PlayerChoiceContext c, CardPlay p, Creature? target, int damage, PokerFace face, bool unblockable = false)
    {
        if (target == null || !target.IsHittable || !Active(p)) return;
        await PokerVfx.Fly(p.Card.Owner.Creature, target, face);
        if (!target.IsHittable || !Active(p)) return;
        if (unblockable) await CreatureCmd.Damage(c, target, damage, ValueProp.Move | ValueProp.Unblockable, p.Card.Owner.Creature, p.Card, p);
        else await DamageCmd.Attack(damage).FromCard(p.Card, p).Targeting(target).WithNoAttackerAnim().WithHitFx("vfx/vfx_attack_slash").Execute(c);
    }
    private static async Task HitAll(PlayerChoiceContext c, CardPlay p, int damage, PokerFace face, bool unblockable = false)
    {
        if (!Active(p) || p.Card.Owner.Creature.CombatState is not { } combat) return;
        var enemies = combat.HittableEnemies.ToArray();
        await Task.WhenAll(enemies.Select(t => PokerVfx.Fly(p.Card.Owner.Creature, t, face)));
        if (enemies.Length == 0 || !Active(p)) return;
        if (unblockable) await CreatureCmd.Damage(c, enemies.Where(e => e.IsHittable).ToArray(), damage, ValueProp.Move | ValueProp.Unblockable, p.Card.Owner.Creature, p.Card, p);
        else await DamageCmd.Attack(damage).FromCard(p.Card, p).TargetingAllOpponents(combat).WithNoAttackerAnim().WithHitFx("vfx/vfx_attack_slash").Execute(c);
    }
}
