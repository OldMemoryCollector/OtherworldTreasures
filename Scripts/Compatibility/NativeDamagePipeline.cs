using System.Reflection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace OtherworldTreasures.Scripts.Compatibility;

// 0.113's async damage pipeline takes DamageTargetData and an explicit hook mask.
// The IEnumerable<Creature> overload is now only a synchronous forwarding wrapper.
internal static class NativeDamagePipeline
{
#if STS2_113
    internal const int HpLossCallSites = 3;
    internal const int HpGuardCallSites = 4;
#else
    internal const int HpLossCallSites = 2;
    internal const int HpGuardCallSites = 3;
#endif
    internal static MethodInfo Method => typeof(CreatureCmd).GetMethods().Single(method =>
        method.Name == nameof(CreatureCmd.Damage) &&
#if STS2_113
        method.GetParameters().Length == 8 &&
        method.GetParameters()[1].ParameterType == typeof(IEnumerable<DamageTargetData>) &&
#else
        method.GetParameters().Length == 7 &&
        method.GetParameters()[1].ParameterType == typeof(IEnumerable<Creature>) &&
#endif
        method.GetParameters()[2].ParameterType == typeof(decimal));
}
