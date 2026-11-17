using MegaCrit.Sts2.Core.Models;

namespace OtherworldTreasures.Scripts.Konoe;

// 招式派生判定：按卡牌类名归类，供「连携：下段／对空」定向检索使用。
internal static class KonoeStance
{
    private static readonly HashSet<string> Crouching =
    [
        nameof(KonoeCrouchingLight), nameof(KonoeCrouchingMedium), nameof(KonoeLowSweep)
    ];

    private static readonly HashSet<string> AntiAir =
    [
        nameof(KonoeRisingStrike), nameof(KonoeAntiAirHeavy), nameof(KonoeSkyIntercept)
    ];

    internal static bool IsCrouching(CardModel? card) => Matches(card, Crouching);

    internal static bool IsAntiAir(CardModel? card) => Matches(card, AntiAir);

    private static bool Matches(CardModel? card, HashSet<string> names) => card != null && names.Contains(card.GetType().Name);
}
