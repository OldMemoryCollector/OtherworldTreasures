using OtherworldTreasures.Scripts.Enchantments;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Saves.Runs;
using OtherworldTreasures.Scripts.Relics;

namespace OtherworldTreasures.Scripts.Events;

internal static class ShimmerCardActions
{
    internal static IReadOnlyList<CardModel> TransformCandidates(CardModel source) => !source.IsTransformable
        ? Array.Empty<CardModel>()
        : source.Pool.GetUnlockedCards(source.Owner.UnlockState, source.Owner.RunState.CardMultiplayerConstraint)
            .Where(c => c.Rarity == source.Rarity && c.Id != source.Id).ToArray();

    internal static async Task<CardModel?> Transform(CardModel source, Rng rng)
    {
        var candidates = TransformCandidates(source);
        if (candidates.Count == 0) return null;
        var replacement = source.Owner.RunState.CreateCard(rng.NextItem(candidates), source.Owner);
        var result = await CardCmd.Transform(source, replacement);
        return result?.cardAdded;
    }

    internal static bool IsEnchantableTarget(CardModel card) => card.Enchantment == null &&
        card.Pile?.Type == PileType.Deck && card.Type is not (CardType.Curse or CardType.Status or CardType.Quest) &&
        card.Rarity != CardRarity.Event && !card.Keywords.Contains(CardKeyword.Unplayable) &&
        EnchantingTable.GetLegalEnchantments(card).Count != 0;

    internal static bool CanTransfer(CardModel source, CardModel target)
    {
        if (source == target || source.Owner != target.Owner || source.Pile?.Type != PileType.Deck ||
            source.CurrentUpgradeLevel == 0 && source.Enchantment == null || !IsEnchantableTarget(target)) return false;
        if (target.CurrentUpgradeLevel + source.CurrentUpgradeLevel > target.MaxUpgradeLevel) return false;
        return source.Enchantment == null || EnchantingTableCompatibility.CanApply(EnchantmentModel.FromSerializable(source.Enchantment.ToSerializable()), target);
    }

    internal static bool Transfer(CardModel source, CardModel target)
    {
        if (!CanTransfer(source, target)) return false;
        var sourceSave = source.ToSerializable();
        var targetSave = target.ToSerializable();
        bool sourceTouched = false;
        try
        {
            for (int i = 0; i < sourceSave.CurrentUpgradeLevel; i++) CardCmd.Upgrade(target);
            if (sourceSave.Enchantment is { } savedEnchantment)
            {
                var moved = EnchantmentModel.FromSerializable(savedEnchantment);
                CardCmd.Enchant(moved, target, moved.Amount);
            }
            sourceTouched = true;
            CardCmd.ClearEnchantment(source);
            CardCmd.Downgrade(source);
            return true;
        }
        catch (Exception error)
        {
            Restore(target, targetSave);
            if (sourceTouched) Restore(source, sourceSave);
            Entry.Logger.Warn($"[ShimmerLake] Transfer failed and card states were restored: {error.Message}");
            return false;
        }
    }

    private static void Restore(CardModel card, SerializableCard snapshot)
    {
        CardCmd.ClearEnchantment(card);
        snapshot.Props?.Fill(card);
        CardCmd.Downgrade(card);
        if (snapshot.Enchantment is { } saved)
        {
            var enchantment = EnchantmentModel.FromSerializable(saved);
            CardCmd.Enchant(enchantment, card, enchantment.Amount);
        }
        for (int i = 0; i < snapshot.CurrentUpgradeLevel; i++) CardCmd.Upgrade(card);
    }
}
