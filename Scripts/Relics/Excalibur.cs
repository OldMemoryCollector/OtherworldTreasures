using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Relics;

[RegisterRelic(typeof(SharedRelicPool))]
public class Excalibur : ModRelicTemplate
{
    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://OtherworldTreasures/images/relics/Excalibur_85.png",
        IconOutlinePath: "res://OtherworldTreasures/images/relics/Excalibur_85.png",
        BigIconPath: "res://OtherworldTreasures/images/relics/Excalibur.png");
    public override RelicRarity Rarity => RelicRarity.Event;
    internal static decimal Resolve(decimal damage, int block, bool awakened) =>
        damage < block ? block + (awakened ? Math.Floor(damage * .5m) : 0) : damage;
}

[RegisterRelic(typeof(SharedRelicPool))]
public sealed class TrueExcalibur : Excalibur
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<StrengthPower>(3)];
    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is CombatRoom)
            await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), Owner.Creature,
                DynamicVars.Strength.BaseValue, Owner.Creature, null);
    }
}

// This adapter changes a single call site in CreatureCmd.Damage, after the entire
// original modifier pipeline. It neither patches Hook nor alters other attacks.
// Native block/HP guards, damage history, hit hooks and kill handling still run once.
[HarmonyPatch]
internal static class ExcaliburFinalDamageAdapter
{
    private static MethodBase TargetMethod()
    {
        var method = typeof(CreatureCmd).GetMethods().Single(m => m.Name == nameof(CreatureCmd.Damage) &&
            m.GetParameters().Length == 7 && m.GetParameters()[1].ParameterType == typeof(IEnumerable<Creature>) &&
            m.GetParameters()[2].ParameterType == typeof(decimal));
        return AccessTools.Method(method.GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType, "MoveNext");
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var original = AccessTools.Method(typeof(Hook), nameof(Hook.ModifyDamage));
        int count = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(original))
            {
                instruction.operand = AccessTools.Method(typeof(ExcaliburFinalDamageAdapter), nameof(ModifyFinal));
                count++;
            }
            yield return instruction;
        }
        if (count != 1) throw new InvalidOperationException($"Excalibur: expected one final damage call, found {count}.");
    }

    internal static decimal ModifyFinal(IRunState run, ICombatState? combat, Creature? target, Creature? dealer,
        decimal damage, ValueProp props, CardModel? source, CardPlay? play, ModifyDamageHookType type,
        CardPreviewMode preview, out IEnumerable<AbstractModel> modifiers)
    {
        var final = Hook.ModifyDamage(run, combat, target, dealer, damage, props, source, play, type, preview, out modifiers);
        if (target == null || dealer?.Player is not { } player || !props.HasFlag(ValueProp.Move) ||
            props.HasFlag(ValueProp.Unblockable) || target.Side == dealer.Side) return final;
        var swords = player.Relics.OfType<Excalibur>().ToList();
        return swords.Count == 0 ? final : Excalibur.Resolve(final, target.Block, swords.Any(r => r is TrueExcalibur));
    }
}
