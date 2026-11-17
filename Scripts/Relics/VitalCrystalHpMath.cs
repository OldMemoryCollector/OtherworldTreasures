namespace OtherworldTreasures.Scripts.Relics;

internal static class VitalCrystalHpMath
{
    internal const int MaxHp = 999999999;

    // Keep full precision between acquisitions; round only the final displayed maximum.
    internal static int Scale(decimal baseMaxHp, int copies)
    {
        decimal result = Math.Clamp(baseMaxHp, 0m, MaxHp);
        for (int i = 0; i < copies && result > 0m && result < MaxHp; i++)
            result = Math.Min(MaxHp, result * 1.2m);
        return (int)decimal.Floor(result);
    }

    internal static decimal InferBase(int displayedMaxHp, int copies)
    {
        if (displayedMaxHp <= 0) return 0m;
        decimal lower = Math.Clamp(displayedMaxHp, 0, MaxHp);
        decimal upper = lower + 1m;
        for (int i = 0; i < copies && upper > 0m; i++)
        {
            lower /= 1.2m;
            upper /= 1.2m;
        }
        // Prefer an integer baseline if the old rounded total permits one.
        decimal integer = decimal.Ceiling(lower);
        if (Scale(integer, copies) == displayedMaxHp) return integer;
        return (lower + upper) / 2m;
    }

    internal static decimal AfterLoss(decimal baseMaxHp, decimal amount) => Math.Max(1m, baseMaxHp - amount);
}
