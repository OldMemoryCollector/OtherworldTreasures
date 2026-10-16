using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using OtherworldTreasures.Scripts.Powers;

namespace OtherworldTreasures.Scripts.Cards.Alien;

/// <summary>通过 RitsuLib AdditionalHoverTips 添加释义；按卡牌类型选择，不依赖显示语言或战斗中的 Owner。</summary>
internal static class AlienCardHoverTips
{
    internal static IEnumerable<IHoverTip> For(CardModel card)
    {
        if (card is HeatblastFireball or HeatblastFlameJet or HeatblastFlameDash or HeatblastHeatWave
            or HeatblastIgnite or HeatblastCombustionReaction or HeatblastMagmaBurst or HeatblastDetonate
            or HeatblastChainCombustion or HeatblastSolarFlare or HeatblastSupernova or HeatblastHellfireRain)
            yield return Tip("SCORCH", new DynamicVar("DamagePerStack", AlienScorchPower.DamagePerStack));

        // 手牌中的原版灼伤与敌人的自定义灼伤能力同名，单独说明，避免混淆两套结算。
        if (card is HeatblastMagmaEruption or HeatblastMagmaShaping or HeatblastBurstTakeoff or HeatblastHellfireRain)
            yield return Tip("BURN_STATUS");

        if (card is HeatblastMagmaArmor or HeatblastSearingAbsorb or HeatblastScorchingFeedback
            or HeatblastMagmaFist or HeatblastScorchingDomain or HeatblastEmberBackflow
            or HeatblastScorchingCore or HeatblastFlamePropulsion or HeatblastHellfireRain)
            yield return HoverTipFactory.FromKeyword(CardKeyword.Exhaust);

        if (card is HeatblastJetDash or HeatblastMeteorImpact or HeatblastSupercombustion)
            yield return Tip("EXTRA_ENERGY");

        if (card is FourArmsDoublePunch)
            yield return Tip("FOUR_ARM_STRENGTH");

        if (card is WildmuttSavagePounce or WildmuttScentTrail or WildmuttAgileDodge or WildmuttBeastGrowl
            or WildmuttAirbornePounce or WildmuttWallClimbStrike or WildmuttLeapingInstinct or WildmuttBlindSpotClaw
            or WildmuttWindCounter or WildmuttHeatPursuit or WildmuttTasteTrail or WildmuttThermalSense
            or WildmuttRendingClaws or WildmuttThroatBite or WildmuttBloodScent or WildmuttHuntCharge
            or WildmuttFeedingFrenzy or WildmuttPrimalHunt)
            yield return Tip("PREY");

        if (card is WildmuttBloodScent or WildmuttFeedingFrenzy or WildmuttPrimalHunt)
            yield return Tip("WILD_PURSUIT", new EnergyVar(1));

        if (card is GhostfreakGhostClaw or GhostfreakColdTouch or GhostfreakShadowSweep or GhostfreakSoulScream
            or GhostfreakDeadStare or GhostfreakShadowWhisper or GhostfreakManifestation or GhostfreakWallPierce
            or GhostfreakShadowSkim or GhostfreakRealityReverse or GhostfreakInterlace
            or GhostfreakSoulEntangle or GhostfreakWraithWail or GhostfreakPanicGrab or GhostfreakEvilManifest
            or GhostfreakNightmareSpread or GhostfreakDespairEcho or GhostfreakTrueForm or GhostfreakMentalHostage
            or GhostfreakBodySteal or GhostfreakPuppetCounter or GhostfreakMindControl or GhostfreakUltimateTerror)
            yield return FearTip("FEAR");

        if (card is GhostfreakManifestation or GhostfreakNightmareSpread or GhostfreakSoulTentacle
            or GhostfreakSoulDevour or GhostfreakPuppetCounter or GhostfreakMindControl or GhostfreakDominion)
            yield return FearTip("SOUL_FRIGHT");

        if (card is GhostfreakManifestation or GhostfreakUltimateTerror)
            yield return FearTip("DISPERSE");

        if (card is GhostfreakColdInvasion or GhostfreakShadowSkim or GhostfreakSpiritGlide
            or GhostfreakRealityReverse or GhostfreakInterlace)
            yield return HoverTipFactory.FromPower<IntangiblePower>();
    }

    internal static DynamicVar[] FearVariables() => [
        new DynamicVar("SoulFrightThreshold", AlienTerrorPower.SoulFrightThreshold),
        new DynamicVar("DisperseThreshold", AlienTerrorPower.DisperseThreshold),
        new DynamicVar("BossSoulFrightThreshold", AlienTerrorPower.SoulFrightThreshold * 2),
        new DynamicVar("BossDisperseThreshold", AlienTerrorPower.DisperseThreshold * 2),
    ];

    private static IHoverTip FearTip(string key) => Tip(key, FearVariables());

    private static IHoverTip Tip(string key, params DynamicVar[] variables)
    {
        string entry = $"OTHERWORLD_TREASURES_ALIEN_{key}";
        var title = new LocString("static_hover_tips", entry + ".title");
        var description = new LocString("static_hover_tips", entry + ".description");
        foreach (var variable in variables)
        {
            title.Add(variable);
            description.Add(variable);
        }
        return new HoverTip(title, description);
    }
}
