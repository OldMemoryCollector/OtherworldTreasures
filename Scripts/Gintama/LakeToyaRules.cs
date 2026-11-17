namespace OtherworldTreasures.Scripts.Gintama;

internal readonly record struct LakeToyaAttack(int Damage, int Hits);

internal static class LakeToyaRules
{
    internal const int BaseDamage = 14;
    internal static int MissingHealthBonus(int maxHp, int currentHp) =>
        (int)(Math.Max(0L, (long)maxHp - Math.Max(0, currentHp)) / 2);

    // A read-only forecast: carry Block, Buffer and absorption across every hit.
    // Never call committed HP-loss hooks here; some relics spend their protection in those hooks.
    internal static bool IsLethal(int hp, int block, int absorption, int buffer,
        IEnumerable<LakeToyaAttack> attacks)
    {
        if (hp <= 0) return false;
        long remainingHp = hp;
        long remainingBlock = Math.Max(0, block), remainingAbsorption = Math.Max(0, absorption);
        int remainingBuffer = Math.Max(0, buffer);
        foreach (var attack in attacks)
        {
            if (attack.Damage <= 0) continue;
            for (int hit = 0; hit < attack.Hits; hit++)
            {
                long blocked = Math.Min(remainingBlock, attack.Damage);
                remainingBlock -= blocked;
                long loss = attack.Damage - blocked;
                if (loss <= 0) continue;
                if (remainingBuffer > 0) { remainingBuffer--; continue; }
                long absorbed = Math.Min(remainingAbsorption, loss);
                remainingAbsorption -= absorbed;
                remainingHp -= loss - absorbed;
                if (remainingHp <= 0) return true;
            }
        }
        return false;
    }
}
