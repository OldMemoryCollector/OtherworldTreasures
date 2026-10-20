using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using OtherworldTreasures.Scripts.Cards;

namespace OtherworldTreasures.Scripts.Monsters;

/// <summary>Base-game type tags used by the XX必须死 cards and Slayer enchantments.</summary>
public static class MonsterTypeTags
{
    private static readonly IReadOnlyDictionary<MustDieType, HashSet<string>> Tags =
        new Dictionary<MustDieType, HashSet<string>>
        {
            [MustDieType.Humanoid] = Set("Architect AssassinRubyRaider AxeRubyRaider BruteRubyRaider CalcifiedCultist CrossbowRubyRaider DampCultist DevotedSculptor Fabricator FatGremlin FlailKnight FrogKnight GremlinMerc KinFollower KinPriest MagiKnight MysteriousKnight Queen SneakyGremlin SpectralKnight FakeMerchantMonster TrackerRubyRaider TurretOperator"),
            [MustDieType.Machine] = Set("Axebot BattleFriendV1 BattleFriendV2 BattleFriendV3 Chomper Crusher CubexConstruct GasBomb GlobeHead Guardbot HunterKiller LivingShield MechaKnight Noisebot PunchConstruct Rocket Stabbot TheAdversaryMkOne TheAdversaryMkThree TheAdversaryMkTwo Zapbot"),
            [MustDieType.Insect] = Set("BowlbugEgg BowlbugNectar BowlbugRock BowlbugSilk CorpseSlug Entomancer DecimillipedeSegment DecimillipedeSegmentBack DecimillipedeSegmentFront DecimillipedeSegmentMiddle Exoskeleton FuzzyWurmCrawler LagavulinMatriarch LouseProgenitor Myte Ovicopter PhrogParasite ShrinkerBeetle SlumberingBeetle ThievingHopper Tunneler Wriggler"),
            [MustDieType.Beast] = Set("Byrdonis Byrdpip CeremonialBeast Mawler Nibbit OwlMagistrate SlitheringStrangler SpinyToad TheInsatiable Toadpole TwoTailedRat WaterfallGiant"),
            [MustDieType.SeaMonster] = Set("HauntedShip PhantasmalGardener SewerClam SkulkingColony SludgeSpinner SoulFysh TerrorEel"),
            [MustDieType.Plant] = Set("Flyconid Fogmog Seapunk SnappingJaxfruit VineShambler"),
            [MustDieType.Slime] = Set("LeafSlimeM LeafSlimeS SlimedBerserker TwigSlimeM TwigSlimeS"),
            [MustDieType.Other] = Set("Aeonglass BigDummy BygoneEffigy EyeWithTeeth FossilStalker InfestedPrism Inklet KnowledgeDemon LivingFog Osty PaelsLegion Parafright ScrollOfBiting SoulNexus TheForgotten TheLost TheObscura TorchHeadAmalgam ToughEgg Vantom")
        };

    public static bool Matches(Creature creature, MustDieType type)
    {
        if (creature.Monster == null) return false;
        string name = creature.Monster.GetType().Name;
        // Boss models commonly append "Boss" to the underlying monster type.
        string baseName = name.EndsWith("Boss", StringComparison.Ordinal) ? name[..^4] : name;
        foreach (var (tag, names) in Tags)
            if (names.Contains(name) || names.Contains(baseName))
                return tag == type;
        // Unknown and third-party monsters use the documented catch-all category.
        return type == MustDieType.Other;
    }

    public static bool IsBossBody(Creature creature) =>
        creature.Monster != null && !creature.IsSecondaryEnemy &&
        creature.Monster.GetType().Name.EndsWith("Boss", StringComparison.Ordinal);

    private static HashSet<string> Set(string names) =>
        names.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
}
