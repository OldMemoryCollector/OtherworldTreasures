using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Combat.HealthBars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Powers;

// Combat-only apple effects stay outside transformation / Time Cloth snapshots.
public abstract class GoldenApplePower : ModPowerTemplate
{
    protected abstract string LocKey { get; }
    private string IconName => LocKey.ToLowerInvariant();
    public override LocString Title => new("powers", $"OTHERWORLD_TREASURES_POWER_{LocKey}.title");
    public override LocString Description => new("powers", $"OTHERWORLD_TREASURES_POWER_{LocKey}.description");
    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"res://OtherworldTreasures/images/powers/{IconName}.png",
        BigIconPath: $"res://OtherworldTreasures/images/powers/{IconName}.png");
    public override PowerType Type => PowerType.Buff;
    public override Task AfterCombatEnd(CombatRoom room) => PowerCmd.Remove(this);
}

[RegisterPower]
public sealed class TemporaryHealthPower : GoldenApplePower, IHealthBarForecastSource
{
    protected override string LocKey => "TEMPORARY_HEALTH";
    public override PowerStackType StackType => PowerStackType.Counter;
    internal static int Available(Creature creature) => Math.Max(0, creature.GetPower<TemporaryHealthPower>()?.Amount ?? 0);

    // Called only at committed HP loss, never while calculating a preview.
    // SetAmount raises the native power/UI notification. This resource payment
    // must not be multiplied or prevented by power-application modifiers.
    internal static int Spend(Creature creature, int loss)
    {
        var power = creature.GetPower<TemporaryHealthPower>();
        int spent = Math.Min(Available(creature), Math.Max(0, loss));
        if (power == null || spent == 0) return 0;
        power.SetAmount(power.Amount - spent);
        if (power.Amount == 0) power.RemoveInternal();
        return spent;
    }

    public IEnumerable<HealthBarForecastSegment> GetHealthBarForecastSegments(HealthBarForecastContext context)
    {
        // The separate gold band remains visible at full normal HP. It never
        // changes MaxHp or the framework's lethal-damage forecast/HP label.
        if (context.Creature == Owner && Owner.IsAlive && Amount > 0)
            yield return new HealthBarForecastSegment(Amount, new Color("edc447"),
                HealthBarForecastGrowthDirection.InwardFromMaxHp, 100);
    }
}

[RegisterPower]
public sealed class AppleResistancePower : GoldenApplePower
{
    protected override string LocKey => "APPLE_RESISTANCE";
    public override PowerStackType StackType => PowerStackType.Single;
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? source, CardPlay? play) =>
        target == Owner && (!props.HasFlag(ValueProp.Unblockable) || props.HasFlag(ValueProp.Move)) ? .8m : 1m;
}

[RegisterPower]
public sealed class AppleFireResistancePower : GoldenApplePower
{
    protected override string LocKey => "APPLE_FIRE_RESISTANCE";
    public override PowerStackType StackType => PowerStackType.Single;
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? source, CardPlay? play) => target == Owner && source is Burn ? 0m : 1m;
}

// Only replace the two final HP-loss call sites inside CreatureCmd.Damage.
// Native Block, Buffer, Intangible, Osty, hit hooks and kill handling still run.
// CreatureCmd.Kill and direct HP assignment (e.g. rewinding) are untouched.
[HarmonyPatch]
internal static class TemporaryHealthLossAdapter
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
        var original = AccessTools.Method(typeof(Creature), nameof(Creature.LoseHpInternal));
        int count = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(original))
            {
                instruction.opcode = System.Reflection.Emit.OpCodes.Call;
                instruction.operand = AccessTools.Method(typeof(TemporaryHealthLossAdapter), nameof(LoseHealth));
                count++;
            }
            yield return instruction;
        }
        if (count != 2) throw new InvalidOperationException($"Temporary HP: expected 2 health-loss calls, found {count}.");
    }

    private static DamageResult LoseHealth(Creature creature, decimal amount, ValueProp props)
    {
        int loss = (int)Math.Clamp(amount, 0m, 999999999m);
        int spent = creature.IsAlive ? TemporaryHealthPower.Spend(creature, loss) : 0;
        var result = creature.LoseHpInternal(loss - spent, props);
        if (spent == 0) return result;
        // Temporary HP is real health: hits and life-loss triggers see the full
        // paid amount, while lethality/overkill use only the remaining loss.
        return new DamageResult(creature, props)
        {
            UnblockedDamage = result.UnblockedDamage + spent,
            WasTargetKilled = result.WasTargetKilled,
            OverkillDamage = result.OverkillDamage
        };
    }
}
