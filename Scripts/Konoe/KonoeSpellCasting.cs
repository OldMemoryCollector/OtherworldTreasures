using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace OtherworldTreasures.Scripts.Konoe;

internal static class KonoeSpellCasting
{
    internal static async Task Release(PlayerChoiceContext context, CardModel source, CardPlay play, KonoeSpellKind spell, Creature? target, bool consume, bool upgraded, IReadOnlyList<KonoeElement>? consumedElements = null, bool retainSpellDraws = false)
    {
        var owner = source.Owner;
        var v = KonoeSpellValues.For(spell, upgraded);
        // The recipe determines spell tier; only actually spent current/stored elements can return.
        var consumed = consume ? KonoeElementRules.Decode(KonoeElements.Get(owner)) : consumedElements ?? [];
        int degree = KonoeBlueSupport.SpellElements(spell).Count;
        bool airborne = target?.HasPower<KonoeAirbornePower>() == true;
        if (consume) KonoeElements.Consume(owner);
        bool immediate = spell is KonoeSpellKind.BlackStone or KonoeSpellKind.Lapis or KonoeSpellKind.Sapphire or KonoeSpellKind.Tanzanite
            or KonoeSpellKind.Emerald or KonoeSpellKind.Peridot or KonoeSpellKind.Malachite or KonoeSpellKind.Coral or KonoeSpellKind.Garnet or KonoeSpellKind.Rubellite or KonoeSpellKind.Kunzite;
        if (!immediate) await CreatureCmd.TriggerAnim(owner.Creature, "Cast", owner.Character.CastAnimDelay);
        switch (spell)
        {
            case KonoeSpellKind.BlackStone:
                await Hit(v.Damage);
                await KonoeLinkage.Draw(context, owner, card => card.Type == CardType.Attack, retainSpellDraws);
                break;
            case KonoeSpellKind.Lapis:
            case KonoeSpellKind.Sapphire:
            case KonoeSpellKind.Tanzanite:
                await Hit(v.Damage);
                if (target is { IsAlive: true }) await PowerCmd.Apply<KonoeFrozenPower>(context, target, v.Stacks, owner.Creature, source);
                break;
            case KonoeSpellKind.Emerald:
            case KonoeSpellKind.Peridot:
            case KonoeSpellKind.Malachite:
                foreach (var enemy in await Hit(v.Damage, all: true))
                    if (enemy.IsAlive) await PowerCmd.Apply<KonoeKnockdownPower>(context, enemy, v.Stacks, owner.Creature, source);
                break;
            case KonoeSpellKind.Coral:
            case KonoeSpellKind.Garnet:
            case KonoeSpellKind.Rubellite:
                await Hit(v.Damage);
                if (target is { IsAlive: true }) await PowerCmd.Apply<KonoeAirbornePower>(context, target, v.Stacks, owner.Creature, source);
                break;
            case KonoeSpellKind.Chrysoberyl:
                if (await PowerCmd.Apply<KonoeAttackGuardPower>(context, owner.Creature, v.Stacks, owner.Creature, source) is { } guard)
                {
                    guard.BlockPerAttack = Math.Max(guard.BlockPerAttack, v.Followup);
                    guard.SetGrantingPlay(play);
                }
                break;
            case KonoeSpellKind.Amethyst:
                if (target is { IsAlive: true })
                    await Grant<KonoeAmethystEchoPower>(1, p => { p.StoredDamage = v.Damage; p.StatusStacks = v.Stacks; p.DealerCombatId = checked((int)owner.Creature.CombatId!.Value); }, target);
                break;
            case KonoeSpellKind.Citrine:
                if (owner.Creature.GetPower<KonoeCounterPower>() is { } oldMirror) await PowerCmd.Remove(oldMirror);
                await Grant<KonoeCounterPower>(v.Stacks, _ => { });
                break;
            case KonoeSpellKind.Azurite:
                foreach (var enemy in Enemies())
                {
                    if (enemy.GetPower<KonoeAirbornePower>() is not { } floating) continue;
                    await PowerCmd.Remove(floating);
                    await PowerCmd.Apply<KonoeKnockdownPower>(context, enemy, v.Stacks, owner.Creature, source);
                }
                await Grant<KonoeGravityFieldPower>(v.Turns, p => p.Reduction = v.Bonus);
                break;
            case KonoeSpellKind.Taaffeite:
                await Grant<KonoeFairyPower>(v.Turns, p => p.StoredDamage = v.Followup);
                break;
            case KonoeSpellKind.Andradite:
                await Grant<KonoeAndraditePower>(1, p => { p.StoredDamage = v.Damage; p.Growth = v.Bonus; });
                break;
            case KonoeSpellKind.Uvarovite:
                if (target is { IsAlive: true }) await Grant<KonoeRainMarkPower>(v.Bonus, p => p.CasterId = checked((int)owner.Creature.CombatId!.Value), target);
                await Grant<KonoeFireRainPower>(1, p => { p.StoredDamage = v.Damage; p.FollowupDamage = v.Bonus; p.TargetCombatId = target?.CombatId is { } id ? checked((int)id) : -1; p.DamageElementTypes = (int)KonoeDamageType.Fire; });
                break;
            case KonoeSpellKind.Kunzite:
                await Hit(v.Damage);
                if (airborne && target?.GetPower<KonoeAirbornePower>() is { } lifted) await PowerCmd.Remove(lifted);
                if (target is { IsAlive: true })
                {
                    if (airborne) { await Hit(v.Bonus); if (target.IsAlive) await PowerCmd.Apply<KonoeAirbornePower>(context, target, v.Stacks, owner.Creature, source); }
                    else await PowerCmd.Apply<KonoeKnockdownPower>(context, target, v.Stacks, owner.Creature, source);
                }
                break;
            case KonoeSpellKind.Morganite:
                await Grant<KonoeMorganitePower>(1, p => { p.StoredDamage = v.Damage; p.FollowupDamage = v.Followup; });
                break;
            case KonoeSpellKind.Nephrite:
                if (target is { IsAlive: true }) await Grant<KonoeNephritePower>(v.Turns, p => { p.StoredDamage = v.Followup; p.DealerCombatId = checked((int)owner.Creature.CombatId!.Value); }, target);
                break;
        }
        if (spell != KonoeSpellKind.BlackStone) await KonoeBlueSupport.AfterSpell(context, source, play, degree, consumed, retainSpellDraws);

        Creature[] Enemies() => owner.Creature.CombatState?.HittableEnemies.Where(e => e.IsAlive).ToArray() ?? [];
        async Task<Creature[]> Hit(int damage, bool all = false)
        {
            if (all ? Enemies().Length == 0 : target is not { IsAlive: true }) return [];
            var attack = DamageCmd.Attack(damage).FromCard(source, play);
            KonoeDamage.ConfigureSpell(attack, spell);
            if (spell != KonoeSpellKind.BlackStone) attack.WithAttackerAnim("Cast", owner.Character.CastAnimDelay);
            if (all) attack.TargetingAllOpponents(owner.Creature.CombatState!); else attack.Targeting(target!);
            await attack.WithHitFx("vfx/vfx_attack_blunt").Execute(context);
            return attack.Results.SelectMany(r => r).Select(r => r.Receiver).Distinct().ToArray();
        }
        async Task Grant<T>(int amount, Action<T> configure, Creature? recipient = null) where T : PowerModel
        {
            var power = (T)ModelDb.Power<T>().ToMutable(); configure(power);
            if (power is KonoeDelayedSpellPower delayed) delayed.DamageElementTypes = (int)KonoeDamage.SpellProfile(spell, KonoeDamage.AttachedTypes(source)).DamageTypes;
            await PowerCmd.Apply(context, power, recipient ?? owner.Creature, amount, owner.Creature, source);
        }
    }
}
