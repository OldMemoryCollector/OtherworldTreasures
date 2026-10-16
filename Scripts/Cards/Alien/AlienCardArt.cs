using System;
using System.Text.RegularExpressions;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Cards.Alien;

internal static class AlienCardArt
{
    internal static CardAssetProfile Profile(CardModel card)
    {
        var (prefix, folder) = card switch
        {
            HeatblastCardBase => ("Heatblast", "heatblast"),
            FourArmsCardBase => ("FourArms", "four_arms"),
            WildmuttCardBase => ("Wildmutt", "wildmutt"),
            GhostfreakCardBase => ("Ghostfreak", "ghostfreak"),
            _ => throw new ArgumentException("Unsupported alien card", nameof(card)),
        };
        var name = card.GetType().Name[prefix.Length..];
        var file = Regex.Replace(name, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant();
        var portrait = $"res://OtherworldTreasures/images/cards/{folder}/{file}.png";
        return card.Rarity == CardRarity.Ancient
            ? new(PortraitPath: portrait, VisualStyle: CardVisualStyle.Ancient)
            : new(PortraitPath: portrait);
    }
}
