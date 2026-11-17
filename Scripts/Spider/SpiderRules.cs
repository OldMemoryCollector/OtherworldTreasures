namespace OtherworldTreasures.Scripts.Spider;

internal static class SpiderRules
{
    internal const int EchoInterval = 3;
    internal static int SilkFromVulnerable(int stacks) => (int)Math.Min(int.MaxValue, Math.Max(0L, stacks) * 4);
    internal static int SpendSilk(int available, int hpLoss) => Math.Min(Math.Max(0, available), Math.Max(0, hpLoss));
    internal static double Chance(int stacks) => Math.Clamp(stacks / 10d, 0, 1);
    internal static bool Roll(int stacks, double roll) => roll < Chance(stacks);
    internal static int SenseStacks(int attackers) => (int)Math.Min(int.MaxValue, Math.Max(0L, attackers) * 3);
    internal static bool EchoDue(ref int count) { count = (Math.Max(0, count) + 1) % EchoInterval; return count == 0; }
}
