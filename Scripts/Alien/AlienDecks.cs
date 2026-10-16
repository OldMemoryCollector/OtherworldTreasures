using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using OtherworldTreasures.Scripts.Cards.Alien;
using OtherworldTreasures.Scripts.Powers;
using OtherworldTreasures.Scripts.Relics;

namespace OtherworldTreasures.Scripts.Alien;

/// <summary>
/// 外星英雄牌组数据：每位英雄的专属牌（按流派组合）与专属被动。
/// 火焰人：8 通用 + 选定专精 7 张 = 15 张普通牌，另 1 张先古【炎狱天降】直接置入手牌。
/// </summary>
internal static class AlienDecks
{
    // ===== 火焰人通用牌（8 张，每套流派都带）=====
    private static readonly Type[] CommonCards =
    {
        typeof(HeatblastFireball),
        typeof(HeatblastFlameJet),
        typeof(HeatblastSparkSplash),
        typeof(HeatblastMagmaEruption),
        typeof(HeatblastMagmaArmor),
        typeof(HeatblastFlameDash),
        typeof(HeatblastHeatWave),
        typeof(HeatblastSearingAbsorb),
    };

    // ===== 烈焰爆破专精（7 张）=====
    private static readonly Type[] BlastCards =
    {
        typeof(HeatblastIgnite),
        typeof(HeatblastCombustionReaction),
        typeof(HeatblastMagmaBurst),
        typeof(HeatblastDetonate),
        typeof(HeatblastChainCombustion),
        typeof(HeatblastSolarFlare),
        typeof(HeatblastSupernova),
    };

    // ===== 熔岩塑形专精（7 张）=====
    private static readonly Type[] ShapingCards =
    {
        typeof(HeatblastFlameWall),
        typeof(HeatblastScorchingFeedback),
        typeof(HeatblastMagmaFist),
        typeof(HeatblastScorchingDomain),
        typeof(HeatblastEmberBackflow),
        typeof(HeatblastMagmaShaping),
        typeof(HeatblastScorchingCore),
    };

    // ===== 喷射推进专精（7 张）=====
    private static readonly Type[] PropulsionCards =
    {
        typeof(HeatblastFlamePropulsion),
        typeof(HeatblastJetDash),
        typeof(HeatblastAerialSpray),
        typeof(HeatblastFlameGlide),
        typeof(HeatblastBurstTakeoff),
        typeof(HeatblastSupercombustion),
        typeof(HeatblastMeteorImpact),
    };

    private static readonly Type[] FourArmsCommon =
    [typeof(FourArmsHeavyPunch), typeof(FourArmsDoublePunch), typeof(FourArmsQuickJab),
     typeof(FourArmsSweepingStrike), typeof(FourArmsShoulderCharge), typeof(FourArmsGuard),
     typeof(FourArmsGrapple), typeof(FourArmsCombatBreathing)];
    private static readonly Type[] FourArmsCombo =
    [typeof(FourArmsGaleUppercut), typeof(FourArmsTriplePunch), typeof(FourArmsPursuingStrike),
     typeof(FourArmsAlternatingPunches), typeof(FourArmsComboRhythm), typeof(FourArmsRapidOffensive), typeof(FourArmsFrenzy)];
    private static readonly Type[] FourArmsDefense =
    [typeof(FourArmsCounterPunch), typeof(FourArmsElbowChain), typeof(FourArmsCrossGuard),
     typeof(FourArmsGuardingFootwork), typeof(FourArmsIronSuplex), typeof(FourArmsDefenseToOffense), typeof(FourArmsOffenseDefense)];
    private static readonly Type[] FourArmsHeavy =
    [typeof(FourArmsRockBreaker), typeof(FourArmsGroundClap), typeof(FourArmsMightyThrow),
     typeof(FourArmsConcussiveCharge), typeof(FourArmsTitanPunch), typeof(FourArmsSiegeStance), typeof(FourArmsAftershock)];

    private static readonly Type[] WildmuttCommon = [typeof(WildmuttSwiftClaw), typeof(WildmuttFangBite), typeof(WildmuttSavagePounce), typeof(WildmuttQuadrupedSweep), typeof(WildmuttGroundRush), typeof(WildmuttScentTrail), typeof(WildmuttAgileDodge), typeof(WildmuttBeastGrowl)];
    private static readonly Type[] WildmuttOne = [typeof(WildmuttAirbornePounce), typeof(WildmuttWallClimbStrike), typeof(WildmuttRapidReturn), typeof(WildmuttHighDive), typeof(WildmuttVaultDodge), typeof(WildmuttParkour), typeof(WildmuttLeapingInstinct)];
    private static readonly Type[] WildmuttTwo = [typeof(WildmuttBlindSpotClaw), typeof(WildmuttWindCounter), typeof(WildmuttHeatPursuit), typeof(WildmuttGillSniff), typeof(WildmuttSoundLocation), typeof(WildmuttTasteTrail), typeof(WildmuttThermalSense)];
    private static readonly Type[] WildmuttThree = [typeof(WildmuttRendingClaws), typeof(WildmuttThroatBite), typeof(WildmuttBonePounce), typeof(WildmuttTripleBite), typeof(WildmuttBloodScent), typeof(WildmuttHuntCharge), typeof(WildmuttFeedingFrenzy)];
    private static readonly Type[] GhostfreakCommon = [typeof(GhostfreakGhostClaw), typeof(GhostfreakColdTouch), typeof(GhostfreakPhasePierce), typeof(GhostfreakShadowSweep), typeof(GhostfreakSoulScream), typeof(GhostfreakDeadStare), typeof(GhostfreakShadowWhisper), typeof(GhostfreakManifestation)];
    private static readonly Type[] GhostfreakOne = [typeof(GhostfreakSilentAmbush), typeof(GhostfreakWallPierce), typeof(GhostfreakColdInvasion), typeof(GhostfreakShadowSkim), typeof(GhostfreakSpiritGlide), typeof(GhostfreakRealityReverse), typeof(GhostfreakInterlace)];
    private static readonly Type[] GhostfreakTwo = [typeof(GhostfreakSoulEntangle), typeof(GhostfreakWraithWail), typeof(GhostfreakPanicGrab), typeof(GhostfreakEvilManifest), typeof(GhostfreakNightmareSpread), typeof(GhostfreakDespairEcho), typeof(GhostfreakTrueForm)];
    private static readonly Type[] GhostfreakThree = [typeof(GhostfreakSoulTentacle), typeof(GhostfreakSoulDevour), typeof(GhostfreakMentalHostage), typeof(GhostfreakBodySteal), typeof(GhostfreakPuppetCounter), typeof(GhostfreakMindControl), typeof(GhostfreakDominion)];
    internal static Type[] OtherHeroDeck(AlienHero hero, AlienSpecialization spec)
    {
        var common = hero == AlienHero.Wildmutt ? WildmuttCommon : GhostfreakCommon;
        var specialized = spec switch
        {
            AlienSpecialization.Senses => WildmuttTwo,
            AlienSpecialization.Predator => WildmuttThree,
            AlienSpecialization.Terror => GhostfreakTwo,
            AlienSpecialization.Dominion => GhostfreakThree,
            _ => hero == AlienHero.Wildmutt ? WildmuttOne : GhostfreakOne,
        };
        return common.Concat(specialized).ToArray();
    }

    internal static bool HasDeck(AlienHero hero)
        => hero is AlienHero.Heatblast or AlienHero.FourArms or AlienHero.Wildmutt or AlienHero.Ghostfreak;

    internal static Type[] FourArmsDeck(AlienSpecialization spec) => FourArmsCommon.Concat(spec switch
    {
        AlienSpecialization.Frenzy => FourArmsCombo,
        AlienSpecialization.OffenseDefense => FourArmsDefense,
        AlienSpecialization.Destruction => FourArmsHeavy,
        _ => FourArmsCombo,
    }).ToArray();

    /// <summary>根据流派取对应的 7 张专精牌。</summary>
    private static Type[] SpecCards(AlienSpecialization spec) => spec switch
    {
        AlienSpecialization.Blast => BlastCards,
        AlienSpecialization.Shaping => ShapingCards,
        AlienSpecialization.Propulsion => PropulsionCards,
        _ => BlastCards,
    };

    /// <summary>把该英雄的牌组洗入战斗，应用起手保底，并把先古置入手牌。</summary>
    internal static async Task BuildDeck(PlayerChoiceContext choiceContext, Player player, HeroSelection selection)
    {
        var combatState = player.PlayerCombatState;
        if (combatState == null)
        {
            Entry.Logger.Warn($"[Omnitrix] 战斗状态为空，变身只换形象");
            return;
        }
        // 注意：PlayerCombatState 只提供各牌堆（Hand/DrawPile/...），它并不是 ICombatState。
        // 造牌要用 Creature.CombatState（ICombatState）——对 PlayerCombatState 做强转会在运行时抛异常。
        var combat = player.Creature?.CombatState;
        if (combat == null)
        {
            Entry.Logger.Warn($"[Omnitrix] 战斗状态为空，变身只换形象");
            return;
        }

        Type[] cardTypes;
        if (selection.Hero == AlienHero.Heatblast)
        {
            // 8 通用 + 7 专精 = 15 张
            cardTypes = CommonCards.Concat(SpecCards(selection.Specialization)).ToArray();
        }
        else if (selection.Hero == AlienHero.FourArms)
        {
            cardTypes = FourArmsDeck(selection.Specialization);
        }
        else if (selection.Hero is AlienHero.Wildmutt or AlienHero.Ghostfreak)
        {
            cardTypes = OtherHeroDeck(selection.Hero, selection.Specialization);
        }
        else
        {
            Entry.Logger.Warn($"[Omnitrix] {selection.Hero} 没有配牌组，变身只换形象");
            return;
        }

        // 全部加入抽牌堆
        foreach (var type in cardTypes)
        {
            var canonical = ModelDb.AllCards.FirstOrDefault(c => c.GetType() == type);
            if (canonical == null)
            {
                Entry.Logger.Warn($"[Omnitrix] 找不到卡牌模型 {type.Name}，已跳过");
                continue;
            }
            var card = combat.CreateCard(canonical, player);
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Draw, player, CardPilePosition.Random);
        }

        // 洗牌
        await CardPileCmd.Shuffle(choiceContext, player);

        if (selection.Hero != AlienHero.Heatblast)
        {
            await CardPileCmd.Draw(choiceContext, 5, player);
            CardModel canonicalAncient = selection.Hero switch
            {
                AlienHero.Wildmutt => ModelDb.Card<WildmuttPrimalHunt>(),
                AlienHero.Ghostfreak => ModelDb.Card<GhostfreakUltimateTerror>(),
                _ => ModelDb.Card<FourArmsAncient>(),
            };
            var ancient = combat.CreateCard(canonicalAncient, player);
            await CardPileCmd.AddGeneratedCardToCombat(ancient, PileType.Hand, player, CardPilePosition.Random);
            Entry.Logger.Info($"[Omnitrix] {selection.Hero}({selection.Specialization})：15 张牌组 + 1 张先古");
            return;
        }

        // ===== 起手保底 =====
        // 1) 从抽牌堆取出 1 张【火球】或【火焰喷射】（随机）置入手牌
        var drawPile = combatState.DrawPile.Cards;
        var fireball = drawPile.FirstOrDefault(c => c.GetType() == typeof(HeatblastFireball));
        var flameJet = drawPile.FirstOrDefault(c => c.GetType() == typeof(HeatblastFlameJet));
        var firstGuarantee = (fireball != null && flameJet != null)
            ? (player.PlayerRng.Transformations.NextDouble() < 0.5 ? fireball : flameJet)
            : (fireball ?? flameJet);
        if (firstGuarantee != null)
        {
            // 这张牌已在抽牌堆里，属于"跨牌堆搬牌"，必须用 Add（AddGeneratedCardToCombat 只收新生成的牌，会抛异常）
            await CardPileCmd.Add(firstGuarantee, PileType.Hand, CardPilePosition.Random);
        }

        // 2) 从抽牌堆取出 1 张【岩浆喷涌】置入手牌
        var magmaEruption = drawPile.FirstOrDefault(c => c.GetType() == typeof(HeatblastMagmaEruption));
        if (magmaEruption != null)
        {
            // 同上：已在抽牌堆的牌搬到手牌，用 Add 而非 AddGeneratedCardToCombat
            await CardPileCmd.Add(magmaEruption, PileType.Hand, CardPilePosition.Random);
        }

        // 3) 剩余牌洗匀后抽 3 张（凑齐 5 张起手）
        await CardPileCmd.Shuffle(choiceContext, player);
        await CardPileCmd.Draw(choiceContext, 3, player);

        // 4) 额外将 1 张【炎狱天降】置入手牌
        var hellfire = ModelDb.AllCards.FirstOrDefault(c => c.GetType() == typeof(HeatblastHellfireRain));
        if (hellfire != null)
        {
            var ancientCard = combat.CreateCard(hellfire, player);
            await CardPileCmd.AddGeneratedCardToCombat(ancientCard, PileType.Hand, player, CardPilePosition.Random);
        }

        Entry.Logger.Info($"[Omnitrix] {selection.Hero}({selection.Specialization}) 牌组已洗入（{cardTypes.Length} 张），起手已保底");
    }

    /// <summary>挂上该英雄的专属被动（变回时由快照一并清除），并记录当前流派。</summary>
    internal static async Task ApplyPassive(PlayerChoiceContext choiceContext, Player player, HeroSelection selection)
    {
        var creature = player.Creature;
        if (creature == null)
        {
            return;
        }
        switch (selection.Hero)
        {
            case AlienHero.Heatblast:
                await PowerCmd.Apply<HeatblastPassivePower>(choiceContext, creature, 1m, creature, null, silent: true);
                await creature.GetPower<HeatblastPassivePower>()!.AfterPlayerTurnStart(choiceContext, player);
                break;
            case AlienHero.FourArms:
                await PowerCmd.Apply<FourArmsPassivePower>(choiceContext, creature, 1m, creature, null, silent: true);
                await PowerCmd.Apply<StrengthPower>(choiceContext, creature, 2m, creature, null);
                break;
            case AlienHero.Wildmutt:
                await PowerCmd.Apply<WildmuttPassivePower>(choiceContext, creature, 1m, creature, null, silent: true);
                await creature.GetPower<WildmuttPassivePower>()!.AfterPlayerTurnStart(choiceContext, player);
                break;
            case AlienHero.Ghostfreak:
                await PowerCmd.Apply<GhostfreakPassivePower>(choiceContext, creature, 1m, creature, null, silent: true);
                await creature.GetPower<GhostfreakPassivePower>()!.AfterPlayerTurnStart(choiceContext, player);
                break;
        }
    }
}
