namespace OtherworldTreasures.Scripts.Konoe;

public enum KonoeElement { Water = 1, Wind = 2, Fire = 3 }
public enum KonoeSpellKind { BlackStone, Lapis, Sapphire, Tanzanite, Emerald, Peridot, Malachite, Coral, Garnet, Rubellite, Chrysoberyl, Amethyst, Citrine, Azurite, Taaffeite, Andradite, Uvarovite, Kunzite, Morganite, Nephrite }

// The queue preserves acquisition order; the resolver uses only element counts.
public static class KonoeElementRules
{
    public static int Append(int packed, KonoeElement element)
    {
        if (element is < KonoeElement.Water or > KonoeElement.Fire) throw new ArgumentOutOfRangeException(nameof(element));
        return ((packed & 15) << 2) | (int)element;
    }
    public static IReadOnlyList<KonoeElement> Decode(int packed)
    {
        var result = new List<KonoeElement>(3);
        for (int shift = 4; shift >= 0; shift -= 2)
        {
            int value = (packed >> shift) & 3;
            if (value != 0) result.Add((KonoeElement)value);
        }
        return result;
    }
    public static KonoeSpellKind Resolve(int packed)
    {
        int water = 0, wind = 0, fire = 0;
        foreach (var e in Decode(packed))
            if (e == KonoeElement.Water) water++; else if (e == KonoeElement.Wind) wind++; else fire++;
        return (water, wind, fire) switch
        {
            (0,0,0) => KonoeSpellKind.BlackStone,
            (1,0,0) => KonoeSpellKind.Lapis, (2,0,0) => KonoeSpellKind.Sapphire, (3,0,0) => KonoeSpellKind.Tanzanite,
            (0,1,0) => KonoeSpellKind.Emerald, (0,2,0) => KonoeSpellKind.Peridot, (0,3,0) => KonoeSpellKind.Malachite,
            (0,0,1) => KonoeSpellKind.Coral, (0,0,2) => KonoeSpellKind.Garnet, (0,0,3) => KonoeSpellKind.Rubellite,
            (1,1,0) => KonoeSpellKind.Chrysoberyl, (1,0,1) => KonoeSpellKind.Amethyst, (0,1,1) => KonoeSpellKind.Citrine,
            (2,1,0) => KonoeSpellKind.Azurite, (2,0,1) => KonoeSpellKind.Taaffeite, (1,2,0) => KonoeSpellKind.Andradite,
            (0,2,1) => KonoeSpellKind.Uvarovite, (1,0,2) => KonoeSpellKind.Kunzite, (0,1,2) => KonoeSpellKind.Morganite,
            (1,1,1) => KonoeSpellKind.Nephrite,
            _ => throw new InvalidOperationException("Invalid Konoe element combination")
        };
    }
}

public sealed record KonoeSpellValues(int Damage = 0, int Block = 0, int Hits = 1, int Stacks = 0, int Bonus = 0, int Turns = 0, int Followup = 0)
{
    public static KonoeSpellValues For(KonoeSpellKind kind, bool upgraded) => (kind, upgraded) switch
    {
        (KonoeSpellKind.BlackStone, false) => new(Damage:6), (KonoeSpellKind.BlackStone, true) => new(Damage:9),
        (KonoeSpellKind.Lapis, false) => new(Damage:8,Stacks:1), (KonoeSpellKind.Lapis, true) => new(Damage:11,Stacks:2),
        (KonoeSpellKind.Sapphire, false) => new(Damage:12,Stacks:2), (KonoeSpellKind.Sapphire, true) => new(Damage:16,Stacks:3),
        (KonoeSpellKind.Tanzanite, false) => new(Damage:16,Stacks:3), (KonoeSpellKind.Tanzanite, true) => new(Damage:21,Stacks:4),
        (KonoeSpellKind.Emerald, false) => new(Damage:8,Stacks:1), (KonoeSpellKind.Emerald, true) => new(Damage:11,Stacks:2),
        (KonoeSpellKind.Peridot, false) => new(Damage:12,Stacks:2), (KonoeSpellKind.Peridot, true) => new(Damage:16,Stacks:3),
        (KonoeSpellKind.Malachite, false) => new(Damage:16,Stacks:3), (KonoeSpellKind.Malachite, true) => new(Damage:21,Stacks:4),
        (KonoeSpellKind.Coral, false) => new(Damage:8,Stacks:1), (KonoeSpellKind.Coral, true) => new(Damage:11,Stacks:2),
        (KonoeSpellKind.Garnet, false) => new(Damage:12,Stacks:2), (KonoeSpellKind.Garnet, true) => new(Damage:16,Stacks:3),
        (KonoeSpellKind.Rubellite, false) => new(Damage:16,Stacks:3), (KonoeSpellKind.Rubellite, true) => new(Damage:21,Stacks:4),
        (KonoeSpellKind.Chrysoberyl, false) => new(Stacks:4,Followup:3), (KonoeSpellKind.Chrysoberyl, true) => new(Stacks:7,Followup:4),
        (KonoeSpellKind.Amethyst, false) => new(Damage:12,Stacks:2,Turns:1), (KonoeSpellKind.Amethyst, true) => new(Damage:16,Stacks:3,Turns:1),
        (KonoeSpellKind.Citrine, false) => new(Stacks:12), (KonoeSpellKind.Citrine, true) => new(Stacks:16),
        (KonoeSpellKind.Azurite, false) => new(Stacks:2,Bonus:4,Turns:2), (KonoeSpellKind.Azurite, true) => new(Stacks:3,Bonus:6,Turns:2),
        (KonoeSpellKind.Taaffeite, false) => new(Turns:3,Followup:10), (KonoeSpellKind.Taaffeite, true) => new(Turns:3,Followup:13),
        (KonoeSpellKind.Andradite, false) => new(Damage:16,Bonus:4), (KonoeSpellKind.Andradite, true) => new(Damage:21,Bonus:5),
        (KonoeSpellKind.Uvarovite, false) => new(Damage:12,Bonus:8,Turns:1), (KonoeSpellKind.Uvarovite, true) => new(Damage:16,Bonus:11,Turns:1),
        (KonoeSpellKind.Kunzite, false) => new(Damage:16,Bonus:8,Stacks:2), (KonoeSpellKind.Kunzite, true) => new(Damage:21,Bonus:11,Stacks:3),
        (KonoeSpellKind.Morganite, false) => new(Damage:8,Followup:12,Turns:1), (KonoeSpellKind.Morganite, true) => new(Damage:11,Followup:16,Turns:1),
        (KonoeSpellKind.Nephrite, false) => new(Turns:2,Followup:40), (KonoeSpellKind.Nephrite, true) => new(Turns:2,Followup:52),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
    public static MegaCrit.Sts2.Core.ValueProps.ValueProp DamageProps(KonoeSpellKind kind) => kind is KonoeSpellKind.Amethyst or KonoeSpellKind.Andradite or KonoeSpellKind.Uvarovite or KonoeSpellKind.Morganite
        ? MegaCrit.Sts2.Core.ValueProps.ValueProp.Unpowered : MegaCrit.Sts2.Core.ValueProps.ValueProp.Move;
    public static bool TargetsAll(KonoeSpellKind kind) => kind is KonoeSpellKind.Emerald or KonoeSpellKind.Peridot or KonoeSpellKind.Malachite or KonoeSpellKind.Azurite or KonoeSpellKind.Andradite or KonoeSpellKind.Morganite;
    public static bool NeedsTarget(KonoeSpellKind kind) => !TargetsAll(kind) && kind is not (KonoeSpellKind.Chrysoberyl or KonoeSpellKind.Citrine or KonoeSpellKind.Taaffeite);
}
