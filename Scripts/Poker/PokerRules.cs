using System;
using System.Collections.Generic;
using System.Linq;

namespace OtherworldTreasures.Scripts.Poker;

// Card identities are inventory positions. Equal faces are independent physical cards.
public readonly record struct PokerFace(int Value)
{
    public bool IsJoker => Value >= 52;
    public int Suit => IsJoker ? -1 : Value / 13;
    public int Rank => IsJoker ? 0 : Value % 13 + 1;
    public int SequenceRank => Rank == 1 ? 14 : Rank;
    public string ImagePath => "res://OtherworldTreasures/images/cards/poker/" + (Value == 52 ? "joker_small" : Value == 53 ? "joker_big" : new[] { "spade", "heart", "club", "diamond" }[Suit] + "_" + (Rank switch { 1 => "A", 11 => "J", 12 => "Q", 13 => "K", _ => Rank.ToString() })) + ".png";
    public string Name => Value switch { 52 => "小王", 53 => "大王", _ => new[] { "♠", "♥", "♣", "♦" }[Suit] + (Rank switch { 1 => "A", 11 => "J", 12 => "Q", 13 => "K", _ => Rank.ToString() }) };
}

public enum PokerPattern { Scatter, HighCard, OnePair, TwoPair, ThreeOfAKind, Straight, Flush, FullHouse, FourOfAKind, StraightFlush, RoyalFlush }
public sealed record PokerGroup(PokerPattern Pattern, int[] Indexes);
public sealed record PokerSelection(int[] Scatter, PokerGroup[] Groups)
{
    public static PokerSelection Empty => new([], []);
    public int[] AllIndexes => Scatter.Concat(Groups.SelectMany(g => g.Indexes)).Order().ToArray();
    public List<int> Encode() => Scatter.Order().Concat(Groups.SelectMany((g, n) => g.Indexes.Order().Select(i => i + (n + 1) * 54))).ToList();
    public static PokerSelection Decode(IReadOnlyList<PokerFace> cards, IReadOnlyList<int> payload)
    {
        if (payload.Count > 54 || payload.Any(v => v < 0 || v >= 54 * 55)) return Empty;
        var entries = payload.Select(v => (Index: v % 54, Group: v / 54)).ToArray();
        if (entries.Any(e => e.Index >= cards.Count) || entries.Select(e => e.Index).Distinct().Count() != entries.Length) return Empty;
        var groups = entries.Where(e => e.Group > 0).GroupBy(e => e.Group).OrderBy(g => g.Key)
            .Select(g => new PokerGroup(PokerRules.Classify(g.Select(e => cards[e.Index]).ToArray()), g.Select(e => e.Index).Order().ToArray())).ToArray();
        if (groups.Any(g => g.Pattern == PokerPattern.Scatter)) return Empty;
        return new(entries.Where(e => e.Group == 0).Select(e => e.Index).Order().ToArray(), groups);
    }
}

public static class PokerRules
{
    public static string Name(PokerPattern pattern) => pattern switch {
        PokerPattern.HighCard => "高牌", PokerPattern.OnePair => "一对", PokerPattern.TwoPair => "两对", PokerPattern.ThreeOfAKind => "三条",
        PokerPattern.Straight => "顺子", PokerPattern.Flush => "同花", PokerPattern.FullHouse => "葫芦", PokerPattern.FourOfAKind => "四条",
        PokerPattern.StraightFlush => "同花顺", PokerPattern.RoyalFlush => "皇家同花顺", _ => "散牌" };
    public static int StraightRecoveryCount(IEnumerable<PokerFace> cards)
    {
        var ranks = cards.Select(c => c.SequenceRank).Order().ToArray();
        bool wheel = ranks.SequenceEqual(new[] {2, 3, 4, 5, 14});
        int points = ranks.Sum() - (wheel ? 13 : 0);
        return Math.Min(5, 1 + points / 15);
    }
    public static PokerPattern Classify(IReadOnlyList<PokerFace> cards)
    {
        if (cards.Count != 5 || cards.Any(c => c.IsJoker)) return PokerPattern.Scatter;
        var counts = cards.GroupBy(c => c.Rank).Select(g => g.Count()).OrderDescending().ToArray();
        var ranks = cards.Select(c => c.SequenceRank).Distinct().Order().ToArray();
        bool straight = ranks.Length == 5 && (ranks[^1] - ranks[0] == 4 || ranks.SequenceEqual(new[] {2, 3, 4, 5, 14}));
        bool flush = cards.All(c => c.Suit == cards[0].Suit);
        if (straight && flush) return ranks[0] == 10 ? PokerPattern.RoyalFlush : PokerPattern.StraightFlush;
        // Duplicate faces are allowed; five equal ranks receive exactly the four-of-a-kind reward.
        if (counts[0] >= 4) return PokerPattern.FourOfAKind;
        if (counts.SequenceEqual(new[] {3, 2})) return PokerPattern.FullHouse;
        if (flush) return PokerPattern.Flush;
        if (straight) return PokerPattern.Straight;
        if (counts[0] == 3) return PokerPattern.ThreeOfAKind;
        if (counts.Count(c => c == 2) == 2) return PokerPattern.TwoPair;
        if (counts[0] == 2) return PokerPattern.OnePair;
        return PokerPattern.HighCard;
    }
    public static PokerSelection AutoGroup(IReadOnlyList<PokerFace> cards, IEnumerable<int> selected)
    {
        var remaining = selected.Distinct().Where(i => i >= 0 && i < cards.Count).Order().ToList();
        var groups = new List<PokerGroup>();
        while (remaining.Count(i => !cards[i].IsJoker) >= 5)
        {
            int[] candidate = StrongestFive(cards, remaining);
            groups.Add(new(Classify(candidate.Select(i => cards[i]).ToArray()), candidate.Order().ToArray()));
            var used = candidate.ToHashSet();
            remaining.RemoveAll(used.Contains);
        }
        return new(remaining.ToArray(), groups.ToArray());
    }
    // Bucket searches are bounded by 4 suits and 13 ranks, not all C(54,5) subsets.
    public static int[] StrongestFive(IReadOnlyList<PokerFace> cards, IEnumerable<int> selected)
    {
        var normal = selected.Distinct().Where(i => i >= 0 && i < cards.Count && !cards[i].IsJoker).Order().ToList();
        if (normal.Count < 5) return [];
        var ranks = normal.GroupBy(i => cards[i].Rank).OrderByDescending(g => cards[g.First()].SequenceRank).Select(g => g.ToArray()).ToArray();
        for (int suit = 0; suit < 4; suit++) {
            var royal = Sequence(cards, normal.Where(i => cards[i].Suit == suit).ToArray(), 14, 14);
            if (royal.Length == 5) return royal;
        }
        for (int suit = 0; suit < 4; suit++) {
            var straightFlush = Sequence(cards, normal.Where(i => cards[i].Suit == suit).ToArray());
            if (straightFlush.Length == 5) return straightFlush;
        }
        var four = ranks.FirstOrDefault(g => g.Length >= 4);
        if (four != null) return four.Take(4).Concat(normal.Except(four.Take(4)).Take(1)).ToArray();
        foreach (var triple in ranks.Where(g => g.Length >= 3)) {
            var pair = ranks.FirstOrDefault(g => g.Length >= 2 && cards[g[0]].Rank != cards[triple[0]].Rank);
            if (pair != null) return triple.Take(3).Concat(pair.Take(2)).ToArray();
        }
        for (int suit = 0; suit < 4; suit++) {
            var flush = normal.Where(i => cards[i].Suit == suit).OrderByDescending(i => cards[i].SequenceRank).ThenBy(i => i).Take(5).ToArray();
            if (flush.Length == 5) return flush;
        }
        var straight = Sequence(cards, normal);
        if (straight.Length == 5) return straight;
        var three = ranks.FirstOrDefault(g => g.Length >= 3);
        if (three != null) return three.Take(3).Concat(normal.Where(i => cards[i].Rank != cards[three[0]].Rank).Take(2)).ToArray();
        var pairs = ranks.Where(g => g.Length >= 2).Take(2).ToArray();
        if (pairs.Length == 2) {
            var used = pairs.SelectMany(g => g.Take(2)).ToArray();
            return used.Concat(normal.Except(used).Take(1)).ToArray();
        }
        if (pairs.Length == 1) return pairs[0].Take(2).Concat(normal.Where(i => cards[i].Rank != cards[pairs[0][0]].Rank).Take(3)).ToArray();
        return normal.OrderByDescending(i => cards[i].SequenceRank).ThenBy(i => i).Take(5).ToArray();
    }
    private static int[] Sequence(IReadOnlyList<PokerFace> cards, IEnumerable<int> indexes, int highest = 14, int lowest = 5)
    {
        var ranks = indexes.GroupBy(i => cards[i].Rank).ToDictionary(g => g.Key, g => g.Min());
        for (int high = highest; high >= lowest; high--) {
            var result = new List<int>();
            for (int rank = high - 4; rank <= high; rank++) {
                int faceRank = rank == 14 ? 1 : rank;
                if (!ranks.TryGetValue(faceRank, out int index)) break;
                result.Add(index);
            }
            if (result.Count == 5) return result.ToArray();
        }
        return [];
    }
}
