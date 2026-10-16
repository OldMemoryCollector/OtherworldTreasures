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

        _ => null,
    };

    internal static decimal Transform(RelicModel relic, string key, decimal original) => (relic, key) switch
    {
        (TheCourier, "Discount") => Math.Min(100m, original * 2m),
        // MembershipCard stores the percentage of the price still paid.
        (MembershipCard, "Discount") => Math.Max(0m, original * 2m - 100m),
        // GoldIncrease is a multiplier with a baseline of 1. Double only the
        // bonus above baseline: 1.25x becomes 1.5x.
        (BowlerHat, "GoldIncrease") => 1m + (original - 1m) * 2m,
        // DamageDecrease is the remaining damage multiplier, so double the
        // reduction rather than the multiplier itself: 0.5x becomes 0x.
        (UndyingSigil, "DamageDecrease") => Math.Max(0m, original * 2m - 1m),
        _ => original * 2m,
    };
}
