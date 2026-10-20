using MegaCrit.Sts2.Core.Random;
namespace OtherworldTreasures.Scripts.Potions;

internal enum BrewCurve { Rising, Falling, Peak, Valley }
public sealed partial class SCP294Potion
{
    internal static BrewCurve CurveFor(BrewEffect effect) => effect switch
    {
        BrewEffect.Poison or BrewEffect.DrawCards or BrewEffect.Absorption or BrewEffect.Weak or
        BrewEffect.AutoPlayTop or BrewEffect.GainStars or BrewEffect.SneckoDraw => BrewCurve.Falling,
        BrewEffect.Buffer or BrewEffect.Artifact or BrewEffect.Radiance or BrewEffect.Focus or
        BrewEffect.Shrink or BrewEffect.ChannelSameOrbs or BrewEffect.HealExtraTurn or BrewEffect.AmbergrisHeal => BrewCurve.Peak,
        BrewEffect.Damage or BrewEffect.Regen or BrewEffect.DexterityDown or BrewEffect.Clarity or
        BrewEffect.Demise or BrewEffect.ExhaustHandDraw or BrewEffect.ChannelRandomOrbs => BrewCurve.Valley,
        _ => BrewCurve.Rising
    };

    internal static int EffectiveDensity(BrewEffect effect, int density)
    {
        density = Math.Clamp(density, 0, 5);
        return CurveFor(effect) switch
        {
            BrewCurve.Falling => 5 - density,
            BrewCurve.Peak => new[] { 1, 2, 5, 5, 2, 1 }[density],
            BrewCurve.Valley => new[] { 5, 3, 1, 1, 3, 5 }[density],
            _ => density
        };
    }

    internal static (int Min, int Max) DensityRange(BrewEffect effect, int concentration)
    {
        int center = EffectiveDensity(effect, concentration);
        return (Math.Max(0, center - 1), center + 1);
    }

    private static int RollDensity(Rng rng, BrewEffect effect, int concentration)
    {
        var range = DensityRange(effect, concentration);
        return rng.NextInt(range.Min, range.Max + 1);
    }

    internal static (int Min, int Max) ConcentrationRange(BrewEffect effect, int concentration)
    {
        var density = DensityRange(effect, concentration);
        int min = int.MaxValue, max = int.MinValue;
        for (int level = density.Min; level <= density.Max; level++)
        {
            var range = NormalRange(effect, level);
            min = Math.Min(min, range.Min);
            max = Math.Max(max, range.Max);
        }
        return (min, max);
    }

    private static int NormalMaximum(BrewEffect effect) =>
        Enumerable.Range(0, 6).Max(concentration => ConcentrationRange(effect, concentration).Max);

    internal static bool HasAdjustableAmount(BrewEffect effect) => effect is not
        (BrewEffect.AttackChoice or BrewEffect.SkillChoice or BrewEffect.PowerChoice or BrewEffect.ColorlessChoice or
         BrewEffect.RetrieveDiscard or BrewEffect.GambleDiscardDraw or BrewEffect.UpgradeHand or BrewEffect.ExhaustHand or
         BrewEffect.TripleBlock or BrewEffect.DrawPileChoice or BrewEffect.FreeHandCard or BrewEffect.ThreeFreeCards or
         BrewEffect.RandomizeHandCosts or BrewEffect.ChannelDarkOrbs or BrewEffect.ShuffleAndDrawSame or BrewEffect.FillPotionSlots);

    // Vanilla potion values; effects without a native potion counterpart use their normal-mode maximum.
    internal static int VanillaBaseline(BrewEffect effect) => effect switch
    {
        BrewEffect.Damage => 20,
        BrewEffect.Heal => NormalMaximum(effect),
        BrewEffect.HealPercent => 20,
        BrewEffect.HealExtraTurn or BrewEffect.AmbergrisHeal => 50,
        BrewEffect.FairyRevive => 30,
        BrewEffect.FoulSplash => 12,
        BrewEffect.Doom => 33,
        BrewEffect.Block => 12,
        BrewEffect.NextTurnBlock => 10,
        BrewEffect.Absorption => NormalMaximum(effect),
        BrewEffect.MaxHp => 5,
        BrewEffect.Regen => 5,
        BrewEffect.Thorns => 3,
        BrewEffect.Plating => 7,
        BrewEffect.Strength or BrewEffect.Dexterity or BrewEffect.Focus => 2,
        BrewEffect.StrengthDown => 7,
        BrewEffect.DexterityDown or BrewEffect.Poison or BrewEffect.Frail => 6,
        BrewEffect.Weak or BrewEffect.Vulnerable => 3,
        BrewEffect.DrawCards or BrewEffect.AutoPlayTop or BrewEffect.UpgradedColorless => 3,
        BrewEffect.SoulCards or BrewEffect.Artifact => 2,
        BrewEffect.SneckoDraw => 7,
        BrewEffect.ExhaustHandDraw => 10,
        BrewEffect.GainEnergy or BrewEffect.AddOrbSlots or BrewEffect.RetainHand => 2,
        BrewEffect.ShuffleHandIntoDraw => NormalMaximum(effect),
        BrewEffect.NextTurnDraw or BrewEffect.NextTurnEnergy or BrewEffect.ReplayStrikes => 2,
        BrewEffect.Clarity or BrewEffect.Radiance => 3,
        BrewEffect.Shrink => 4,
        BrewEffect.Demise => 9,
        BrewEffect.GainStars => 3,
        BrewEffect.ShivPlus => 3,
        BrewEffect.ChannelSameOrbs or BrewEffect.ChannelRandomOrbs => NormalMaximum(effect),
        _ => 1
    };

    private static int RollBiased(Rng rng, int min, int max, int density)
    {
        int first = rng.NextInt(min, max + 1), second = rng.NextInt(min, max + 1);
        return rng.NextInt(6) < Math.Clamp(density, 0, 6) ? Math.Max(first, second) : Math.Min(first, second);
    }

    private static int RollAmount(Rng rng, BrewEffect effect, int density, BrewRecipient recipient = BrewRecipient.Self)
    {
        if (!HasAdjustableAmount(effect)) return 1;
        if (Scp294Settings.CrazyMode)
            return rng.NextInt(0, VanillaBaseline(effect) * 10 + 1);
        density = RollDensity(rng, effect, density);
        if (effect == BrewEffect.Doom && recipient is BrewRecipient.SelectedEnemy or BrewRecipient.AllEnemies or BrewRecipient.BothSides or BrewRecipient.SelfAndEnemy)
            return RollBiased(rng, 20, 50, density);
        var (min, max) = NormalRange(effect, density);
        return rng.NextInt(min, max + 1);
    }

    private static int RollFoulSplashMultiplier(Rng rng, int density) => Scp294Settings.CrazyMode
        ? rng.NextInt(0, 1001)
        : RollBiased(rng, 50, 150, RollDensity(rng, BrewEffect.FoulSplash, density));

    internal static (int Min, int Max) NormalRange(BrewEffect effect, int density) => effect switch
        {
            BrewEffect.Heal => (4 + density * 2, 6 + density * 2),
            BrewEffect.HealPercent => (10 + density * 3, 15 + density * 3),
            BrewEffect.FairyRevive => (15 + density * 3, 25 + density * 3),
            BrewEffect.HealExtraTurn or BrewEffect.AmbergrisHeal => (30 + density * 3, 40 + density * 3),
            BrewEffect.SneckoDraw => (3 + density, 5 + density),
            BrewEffect.ExhaustHandDraw => (5 + density, 7 + density),
            BrewEffect.FoulSplash => (6 + density * 2, 10 + density * 2),
            BrewEffect.Absorption => (5 + density * 2, 7 + density * 2),
            BrewEffect.MaxHp => (1 + density, 2 + density),
            BrewEffect.NextTurnBlock => (4 + density * 2, 6 + density * 2),
            BrewEffect.NextTurnDraw or BrewEffect.NextTurnEnergy => (1, density >= 4 ? 2 : 1),
            BrewEffect.Clarity or BrewEffect.Radiance or BrewEffect.AutoPlayTop or BrewEffect.RetainHand => (1 + density / 2, 2 + density / 2),
            BrewEffect.Shrink => (2 + density / 2, 3 + density / 2),
            BrewEffect.Demise => (4 + density * 2, 6 + density * 2),
            BrewEffect.GainStars or BrewEffect.AddOrbSlots => (1, density >= 3 ? 2 : 1),
            BrewEffect.ShivPlus or BrewEffect.SoulCards or BrewEffect.UpgradedColorless or BrewEffect.ChannelSameOrbs or BrewEffect.ChannelRandomOrbs => (1 + density / 2, 2 + density / 2),
            BrewEffect.ShuffleHandIntoDraw => (0, 1 + density),
            BrewEffect.Damage => (4 + density * 3, 6 + density * 3),
            BrewEffect.Block => (4 + density * 2, 6 + density * 2),
            BrewEffect.Poison => (2 + density, 3 + density),
            BrewEffect.Regen => (1 + density, 2 + density),
            BrewEffect.DrawCards => (1, Math.Clamp(1 + density / 2, 1, 4)),
            BrewEffect.GainEnergy or BrewEffect.Buffer or BrewEffect.Artifact or BrewEffect.Intangible or BrewEffect.Ritual or BrewEffect.Focus or BrewEffect.ReplayStrikes => (1, density >= 4 ? 2 : 1),
            BrewEffect.ExtraTurn or BrewEffect.DuplicateNextCard or BrewEffect.TripleNextAttack => (1, 1),
            _ => StatRange(density)
        };

    private static (int Min, int Max) StatRange(int density) => density switch
    {
        0 => (1, 1), 1 => (1, 2), 2 => (2, 3), 3 => (2, 4), 4 => (3, 5), _ => (4, 6)
    };
}
