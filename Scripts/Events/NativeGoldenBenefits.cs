using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;

namespace OtherworldTreasures.Scripts.Events;

// Explicit native-only whitelist. It names values whose positive gameplay
// effect remains reachable after enhancement; costs, counters, thresholds,
// one-time pickup fields and effects that cannot be safely doubled stay out.
internal static class NativeGoldenBenefits
{
    internal static string[]? Variables(RelicModel relic) => relic switch
    {
        // The original reviewed cases. These use locals, arithmetic or paired
        // apply/remove behavior, so they are kept as explicit model exceptions.
        MeatOnTheBone or EternalFeather or BookRepairKnife or BurningBlood or BlackBlood or
            BloodVial or BookOfFiveRings or FakeBloodVial or LizardTail or
            MealTicket or Pantograph or Planisphere or RegalPillow or TeaOfDiscourtesy => ["Heal"],
        Pendulum or PollinousCore or BagOfPreparation or BigHat or CentennialPuzzle or Fiddle or
            GamePiece or PaelsBlood or Pocketwatch or PowerCell or RingOfTheDrake or RingOfTheSnake or
            SneckoEye or JossPaper or FuneraryMask or OrangeDough => ["Cards"],
        RedSkull => ["StrengthPower"],
        MercuryHourglass or CharonsAshes or LostWisp or ForgottenSoul or FestivePopper or
            ScreamingFlagon or Tingsha or Kusarigama or LetterOpener or Metronome or
            StoneCalendar or ParryingShield or MysticLighter => ["Damage"],

        // Direct, positive fixed rewards.
        AmethystAubergine or LuckyFysh => ["Gold"],
        Anchor or BoneFlute or CaptainsWheel or CloakClasp or DaughterOfTheWind or DiamondDiadem or
            FakeAnchor or FakeOrichalcum or GalacticDust or HornCleat or
            Orichalcum or OrnamentalFan or Permafrost or Regalite or RippleBasin or Sai or
            SturdyClamp or TheAbacus or ToughBandages or TuningFork => ["Block"],
        // Gold is the payment cost (LoseGold); only Energy is a positive yield.
        SealOfGold => ["Energy"],
        IntimidatingHelmet => ["Block", "Energy"],
        BoomingConch or GremlinHorn => ["Cards", "Energy"],
        ArtOfWar or BlessedAntler or BloodSoakedRose or Candelabra or Chandelier or
            Ectoplasm or FakeHappyFlower or FakeVenerableTeaSet or HappyFlower or
            IvoryTile or Lantern or Nunchaku or PaelsFlesh or PaelsTears or
            PhilosophersStone or PrismaticGem or PumpkinCandle or Sozu or
            SpikedGauntlets or VelvetChoker or VenerableTeaSet or VeryHotCocoa or WhisperingEarring => ["Energy"],
        // Beneficial powers. The selected named field is isolated where the
        // relic also applies a harmful counterpart (Brimstone).
        Akabeko => ["VigorPower"],
        BagOfMarbles or HandDrill => ["VulnerablePower"],
        BeltBuckle or HelicalDart or Kunai or OddlySmoothStone => ["DexterityPower"],
        BronzeScales => ["ThornsPower"],
        Brimstone => ["SelfStrength"],
        DataDisk => ["FocusPower"],
        Gorget => ["PlatingPower"],
        MiniRegent or ReptileTrinket or Shuriken or SlingOfCourage or Vajra or SwordOfJade or
            ToastyMittens or EmberTea => ["StrengthPower"],
        RedMask => ["WeakPower"],
        SneckoSkull or TwistedFunnel => ["PoisonPower"],
        SparklingRouge or RainbowRing => ["StrengthPower", "DexterityPower"],

        // Explicit scalar effects and additive damage modifiers.
        StrikeDummy or FakeStrikeDummy or MiniatureCannon => ["ExtraDamage"],
        InfusedCore => ["Lightning", "ExtraDamage"],
        CrackedCore => ["Lightning"],
        NinjaScroll => ["Shivs"],
        SymbioticVirus => ["Dark"],
        TheBoot => ["DamageMinimum"],
        ChemicalX => ["Increase"],
        SelfFormingClay => ["BlockNextTurn"],
        FresnelLens => ["NimbleAmount"],
        WingCharm => ["SwiftAmount"],
        TungstenRod => ["HpLossReduction"],
        UndyingSigil => ["DamageDecrease"],
        BowlerHat => ["GoldIncrease"],
        TheCourier or MembershipCard => ["Discount"],
        // These use untyped dynamic values but both quantities are positive
        // reward counts: potion slots and the potions granted on pickup.
        AlchemicalCoffer or PotionBelt => ["PotionSlots"],
        PhialHolster => ["PotionSlots", "Potions"],
        BoundPhylactery => ["Summon"],

        _ => null,
    };

    internal static decimal Transform(RelicModel relic, string key, decimal original) => (relic, key) switch
    {
        (TheCourier, "Discount") => Math.Max(original, Math.Min(80m, original * 1.5m)),
        // MembershipCard stores the percentage of the price still paid.
        // Strengthen the discount by half its original magnitude, capped at 80%.
        (MembershipCard, "Discount") => 100m - Math.Max(100m - original, Math.Min(80m, (100m - original) * 1.5m)),
        // GoldIncrease is a multiplier with a baseline of 1. Add half the
        // original bonus above baseline: 1.25x becomes 1.375x.
        (BowlerHat, "GoldIncrease") => 1m + Math.Max(original - 1m, Math.Min(0.8m, (original - 1m) * 1.5m)),
        // DamageDecrease stores the remaining damage multiplier. Strengthen
        // the reduction by half, with damage reduction allowed up to 100%.
        (UndyingSigil, "DamageDecrease") => Math.Min(original, 1m - Math.Min(1m, (1m - original) * 1.5m)),
        _ => original * 2m,
    };
}
