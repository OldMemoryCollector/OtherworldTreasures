using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace OtherworldTreasures.Scripts.Enchantments;

public enum MonsterTypeTag { Humanoid, Mechanical, Insect, Beast, SeaMonster, Plant, Slime, Other }

/// <summary>Shared monster tags for Slayer enchantments and future type-based cards.</summary>
public interface IMonsterTypeProvider
{
    IEnumerable<MonsterTypeTag> GetMonsterTypeTags();
}

public static class MonsterTypeTags
{
    private static readonly Dictionary<string, MonsterTypeTag[]> Vanilla = new(StringComparer.Ordinal)
    {
        ["Architect"]=[MonsterTypeTag.Humanoid],["AssassinRubyRaider"]=[MonsterTypeTag.Humanoid],["AxeRubyRaider"]=[MonsterTypeTag.Humanoid],["BruteRubyRaider"]=[MonsterTypeTag.Humanoid],["CalcifiedCultist"]=[MonsterTypeTag.Humanoid],["CrossbowRubyRaider"]=[MonsterTypeTag.Humanoid],["DampCultist"]=[MonsterTypeTag.Humanoid],["DevotedSculptor"]=[MonsterTypeTag.Humanoid],["Fabricator"]=[MonsterTypeTag.Humanoid],["FatGremlin"]=[MonsterTypeTag.Humanoid],["FlailKnight"]=[MonsterTypeTag.Humanoid],["FrogKnight"]=[MonsterTypeTag.Humanoid],["GremlinMerc"]=[MonsterTypeTag.Humanoid],["KinFollower"]=[MonsterTypeTag.Humanoid],["KinPriest"]=[MonsterTypeTag.Humanoid],["MagiKnight"]=[MonsterTypeTag.Humanoid],["MysteriousKnight"]=[MonsterTypeTag.Humanoid],["Queen"]=[MonsterTypeTag.Humanoid],["SneakyGremlin"]=[MonsterTypeTag.Humanoid],["SpectralKnight"]=[MonsterTypeTag.Humanoid],["FakeMerchantMonster"]=[MonsterTypeTag.Humanoid],["TrackerRubyRaider"]=[MonsterTypeTag.Humanoid],["TurretOperator"]=[MonsterTypeTag.Humanoid],
        ["Axebot"]=[MonsterTypeTag.Mechanical],["BattleFriendV1"]=[MonsterTypeTag.Mechanical],["BattleFriendV2"]=[MonsterTypeTag.Mechanical],["BattleFriendV3"]=[MonsterTypeTag.Mechanical],["Chomper"]=[MonsterTypeTag.Mechanical],["Crusher"]=[MonsterTypeTag.Mechanical],["CubexConstruct"]=[MonsterTypeTag.Mechanical],["GasBomb"]=[MonsterTypeTag.Mechanical],["GlobeHead"]=[MonsterTypeTag.Mechanical],["Guardbot"]=[MonsterTypeTag.Mechanical],["HunterKiller"]=[MonsterTypeTag.Mechanical],["LivingShield"]=[MonsterTypeTag.Mechanical],["MechaKnight"]=[MonsterTypeTag.Mechanical],["Noisebot"]=[MonsterTypeTag.Mechanical],["PunchConstruct"]=[MonsterTypeTag.Mechanical],["Rocket"]=[MonsterTypeTag.Mechanical],["Stabbot"]=[MonsterTypeTag.Mechanical],["TheAdversaryMkOne"]=[MonsterTypeTag.Mechanical],["TheAdversaryMkThree"]=[MonsterTypeTag.Mechanical],["TheAdversaryMkTwo"]=[MonsterTypeTag.Mechanical],["Zapbot"]=[MonsterTypeTag.Mechanical],
        ["BowlbugEgg"]=[MonsterTypeTag.Insect],["BowlbugNectar"]=[MonsterTypeTag.Insect],["BowlbugRock"]=[MonsterTypeTag.Insect],["BowlbugSilk"]=[MonsterTypeTag.Insect],["CorpseSlug"]=[MonsterTypeTag.Insect],["Entomancer"]=[MonsterTypeTag.Insect],["DecimillipedeSegment"]=[MonsterTypeTag.Insect],["DecimillipedeSegmentBack"]=[MonsterTypeTag.Insect],["DecimillipedeSegmentFront"]=[MonsterTypeTag.Insect],["DecimillipedeSegmentMiddle"]=[MonsterTypeTag.Insect],["Exoskeleton"]=[MonsterTypeTag.Insect],["FuzzyWurmCrawler"]=[MonsterTypeTag.Insect],["LagavulinMatriarch"]=[MonsterTypeTag.Insect],["LouseProgenitor"]=[MonsterTypeTag.Insect],["Myte"]=[MonsterTypeTag.Insect],["Ovicopter"]=[MonsterTypeTag.Insect],["PhrogParasite"]=[MonsterTypeTag.Insect],["ShrinkerBeetle"]=[MonsterTypeTag.Insect],["SlumberingBeetle"]=[MonsterTypeTag.Insect],["ThievingHopper"]=[MonsterTypeTag.Insect],["Tunneler"]=[MonsterTypeTag.Insect],["Wriggler"]=[MonsterTypeTag.Insect],
        ["Byrdonis"]=[MonsterTypeTag.Beast],["Byrdpip"]=[MonsterTypeTag.Beast],["CeremonialBeast"]=[MonsterTypeTag.Beast],["Mawler"]=[MonsterTypeTag.Beast],["Nibbit"]=[MonsterTypeTag.Beast],["OwlMagistrate"]=[MonsterTypeTag.Beast],["SlitheringStrangler"]=[MonsterTypeTag.Beast],["SpinyToad"]=[MonsterTypeTag.Beast],["TheInsatiable"]=[MonsterTypeTag.Beast],["Toadpole"]=[MonsterTypeTag.Beast],["TwoTailedRat"]=[MonsterTypeTag.Beast],["WaterfallGiant"]=[MonsterTypeTag.Beast],
        ["HauntedShip"]=[MonsterTypeTag.SeaMonster],["PhantasmalGardener"]=[MonsterTypeTag.SeaMonster],["SewerClam"]=[MonsterTypeTag.SeaMonster],["SkulkingColony"]=[MonsterTypeTag.SeaMonster],["SludgeSpinner"]=[MonsterTypeTag.SeaMonster],["SoulFysh"]=[MonsterTypeTag.SeaMonster],["TerrorEel"]=[MonsterTypeTag.SeaMonster],
        ["Flyconid"]=[MonsterTypeTag.Plant],["Fogmog"]=[MonsterTypeTag.Plant],["Seapunk"]=[MonsterTypeTag.Plant],["SnappingJaxfruit"]=[MonsterTypeTag.Plant],["VineShambler"]=[MonsterTypeTag.Plant],
        ["LeafSlimeM"]=[MonsterTypeTag.Slime],["LeafSlimeS"]=[MonsterTypeTag.Slime],["SlimedBerserker"]=[MonsterTypeTag.Slime],["TwigSlimeM"]=[MonsterTypeTag.Slime],["TwigSlimeS"]=[MonsterTypeTag.Slime],
        ["Aeonglass"]=[MonsterTypeTag.Other],["BigDummy"]=[MonsterTypeTag.Other],["BygoneEffigy"]=[MonsterTypeTag.Other],["EyeWithTeeth"]=[MonsterTypeTag.Other],["FossilStalker"]=[MonsterTypeTag.Other],["InfestedPrism"]=[MonsterTypeTag.Other],["Inklet"]=[MonsterTypeTag.Other],["KnowledgeDemon"]=[MonsterTypeTag.Other],["LivingFog"]=[MonsterTypeTag.Other],["Osty"]=[MonsterTypeTag.Other],["PaelsLegion"]=[MonsterTypeTag.Other],["Parafright"]=[MonsterTypeTag.Other],["ScrollOfBiting"]=[MonsterTypeTag.Other],["SoulNexus"]=[MonsterTypeTag.Other],["TheForgotten"]=[MonsterTypeTag.Other],["TheLost"]=[MonsterTypeTag.Other],["TheObscura"]=[MonsterTypeTag.Other],["TorchHeadAmalgam"]=[MonsterTypeTag.Other],["ToughEgg"]=[MonsterTypeTag.Other],["Vantom"]=[MonsterTypeTag.Other]
    };

    public static IEnumerable<MonsterTypeTag> For(Creature creature)
    {
        if (creature.Monster is IMonsterTypeProvider provider)
            return provider.GetMonsterTypeTags().Distinct().ToArray();
        return Vanilla.TryGetValue(creature.Monster?.GetType().Name ?? string.Empty, out var tags) ? tags : [MonsterTypeTag.Other];
    }
}
