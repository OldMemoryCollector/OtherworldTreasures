using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace OtherworldTreasures.Scripts.Konoe;

internal static class KonoeRareSupport
{
    internal static bool Overdrive(Player p) => p.Creature.GetPower<KonoeOverdrivePower>() is { Amount: > 0 };
    internal static KonoeSpellKind[] Degree(int degree) => Enum.GetValues<KonoeSpellKind>().Where(k => KonoeBlueSupport.SpellElements(k).Count == degree).ToArray();
    internal static int RandomThird(Player p)
    {
        var spells = Degree(3);
        return KonoeBlueSupport.Pack(KonoeBlueSupport.SpellElements(spells[p.RunState.Rng.CombatCardGeneration.NextInt(spells.Length)]));
    }
    internal static Creature? RandomTarget(Player p) => p.Creature.CombatState?.HittableEnemies.Any() == true ? p.RunState.Rng.CombatTargets.NextItem(p.Creature.CombatState.HittableEnemies.ToArray()) : null;
    internal static async Task Current(PlayerChoiceContext c, CardModel source, CardPlay play, int repeat = 1, bool retainSpellDraws = false)
    {
        var spell = KonoeElementRules.Resolve(KonoeElements.Get(source.Owner));
        for (int i = 0; i < repeat; i++)
            await KonoeSpellCasting.Release(c, source, play, spell, play.Target, i == 0, false, retainSpellDraws: retainSpellDraws);
    }
    internal static async Task Stored(PlayerChoiceContext c, CardModel source, CardPlay play, int packed)
    {
        if (packed == 0 || source.Owner.Creature.GetPower<KonoeStoragePower>() is not { } slot) return;
        // Read what is actually in storage now, rather than refunding a stale pre-cast recipe.
        packed = slot.PackedElements;
        if (packed == 0) return;
        var consumed = KonoeElementRules.Decode(packed);
        slot.PackedElements = 0;
        await KonoeSpellCasting.Release(c, source, play, KonoeElementRules.Resolve(packed), play.Target, false, false, consumedElements: consumed);
        KonoeElements.Refresh(source.Owner);
    }
    internal static CardPlay AutomaticPlay(CardModel source, Creature? target) => new()
    {
        Card = source, Player = source.Owner, Target = target, ResultPile = PileType.None,
        Resources = new ResourceInfo { EnergySpent = 0, EnergyValue = 0, StarsSpent = 0, StarValue = 0 },
        IsAutoPlay = true, PlayIndex = 0, PlayCount = 1
    };
}
