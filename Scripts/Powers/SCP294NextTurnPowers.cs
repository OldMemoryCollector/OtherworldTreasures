using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Potions;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Powers;

public abstract class SCP294NextTurnPower : ModPowerTemplate
{
    private bool _triggered;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override bool AllowNegative => true;
    protected abstract string IconName { get; }
    public override LocString Title => new("powers", "OTHERWORLD_TREASURES_POWER_SCP294_NEXT_TURN_" + IconName.ToUpperInvariant() + ".title");
    public override LocString Description => new("powers", "OTHERWORLD_TREASURES_POWER_SCP294_NEXT_TURN_" + IconName.ToUpperInvariant() + ".description");
    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"res://images/powers/{IconName}_power.png",
        BigIconPath: $"res://images/powers/{IconName}_power.png");
    protected abstract Task ApplyStoredAmount(PlayerChoiceContext context, Creature target, decimal amount);

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (_triggered || !participants.Contains(Owner)) return;
        _triggered = true;
        Flash();
        await ApplyStoredAmount(new ThrowingPlayerChoiceContext(), Owner, Amount);
        await PowerCmd.Remove(this);
    }
}

[RegisterPower]
public sealed class SCP294NextTurnStrengthPower : SCP294NextTurnPower
{
    protected override string IconName => "strength";
    protected override Task ApplyStoredAmount(PlayerChoiceContext context, Creature target, decimal amount) =>
        PowerCmd.Apply<StrengthPower>(context, target, amount, target, null);
}

[RegisterPower]
public sealed class SCP294NextTurnDexterityPower : SCP294NextTurnPower
{
    protected override string IconName => "dexterity";
    protected override Task ApplyStoredAmount(PlayerChoiceContext context, Creature target, decimal amount) =>
        PowerCmd.Apply<DexterityPower>(context, target, amount, target, null);
}

[RegisterPower]
public sealed class SCP294NextTurnBufferPower : SCP294NextTurnPower
{
    protected override string IconName => "buffer";
    protected override Task ApplyStoredAmount(PlayerChoiceContext context, Creature target, decimal amount) =>
        PowerCmd.Apply<BufferPower>(context, target, amount, target, null);
}

[RegisterPower]
public sealed class SCP294NextTurnArtifactPower : SCP294NextTurnPower
{
    protected override string IconName => "artifact";
    protected override Task ApplyStoredAmount(PlayerChoiceContext context, Creature target, decimal amount) =>
        PowerCmd.Apply<ArtifactPower>(context, target, amount, target, null);
}

[RegisterPower]
public sealed class SCP294NextTurnIntangiblePower : SCP294NextTurnPower
{
    protected override string IconName => "intangible";
    protected override Task ApplyStoredAmount(PlayerChoiceContext context, Creature target, decimal amount) =>
        PowerCmd.Apply<IntangiblePower>(context, target, amount, target, null);
}

[RegisterPower]
public sealed class SCP294NextTurnAbsorptionPower : SCP294NextTurnPower
{
    protected override string IconName => "temporary_health";
    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://OtherworldTreasures/images/powers/temporary_health.png",
        BigIconPath: "res://OtherworldTreasures/images/powers/temporary_health.png");
    protected override Task ApplyStoredAmount(PlayerChoiceContext context, Creature target, decimal amount) =>
        PowerCmd.Apply<TemporaryHealthPower>(context, target, amount, target, null);
}

public abstract class SCP294TemporaryStrengthPower : TemporaryStrengthPower
{
    public override AbstractModel OriginModel => ModelDb.Potion<OtherworldTreasures.Scripts.Potions.SCP294Potion>();
}

[RegisterPower]
public sealed class SCP294ThisTurnStrengthPower : SCP294TemporaryStrengthPower;

[RegisterPower]
public sealed class SCP294ThisTurnStrengthDownPower : SCP294TemporaryStrengthPower
{
    protected override bool IsPositive => false;
}

public abstract class SCP294TemporaryDexterityPower : TemporaryDexterityPower
{
    public override AbstractModel OriginModel => ModelDb.Potion<OtherworldTreasures.Scripts.Potions.SCP294Potion>();
}

[RegisterPower]
public sealed class SCP294ThisTurnDexterityPower : SCP294TemporaryDexterityPower;

[RegisterPower]
public sealed class SCP294ThisTurnDexterityDownPower : SCP294TemporaryDexterityPower
{
    protected override bool IsPositive => false;
}

// Charge-like effects keep their native behavior during this turn. Only the
// remaining charges granted by this ingredient expire at its owner's turn end.
public abstract class SCP294ThisTurnChargePower : ModPowerTemplate
{
    private bool _expired;
    protected abstract string IconName { get; }
    protected abstract Task Expire(PlayerChoiceContext context, Creature target, int remaining);
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override LocString Title => new("powers", "OTHERWORLD_TREASURES_POWER_SCP294_THIS_TURN.title");
    public override LocString Description => new("powers", "OTHERWORLD_TREASURES_POWER_SCP294_THIS_TURN.description");
    public override PowerAssetProfile AssetProfile => new(
        IconPath: $"res://images/powers/{IconName}_power.png",
        BigIconPath: $"res://images/powers/{IconName}_power.png");

    public override async Task AfterSideTurnEnd(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants)
    {
        // Defensive charges must remain through the opposing side's action.
        if (_expired || side == Owner.Side) return;
        _expired = true;
        await Expire(context, Owner, Amount);
        await PowerCmd.Remove(this);
    }
}

[RegisterPower]
public sealed class SCP294ThisTurnBufferPower : SCP294ThisTurnChargePower
{
    protected override string IconName => "buffer";
    protected override Task Expire(PlayerChoiceContext context, Creature target, int remaining) =>
        PowerCmd.Apply<BufferPower>(context, target, -Math.Min(remaining, target.GetPower<BufferPower>()?.Amount ?? 0), target, null);
}

[RegisterPower]
public sealed class SCP294ThisTurnArtifactPower : SCP294ThisTurnChargePower
{
    protected override string IconName => "artifact";
    protected override Task Expire(PlayerChoiceContext context, Creature target, int remaining) =>
        PowerCmd.Apply<ArtifactPower>(context, target, -Math.Min(remaining, target.GetPower<ArtifactPower>()?.Amount ?? 0), target, null);
}

[RegisterPower]
public sealed class SCP294ThisTurnIntangiblePower : SCP294ThisTurnChargePower
{
    protected override string IconName => "intangible";
    protected override Task Expire(PlayerChoiceContext context, Creature target, int remaining) =>
        PowerCmd.Apply<IntangiblePower>(context, target, -Math.Min(remaining, target.GetPower<IntangiblePower>()?.Amount ?? 0), target, null);
}

[RegisterPower]
public sealed class SCP294ThisTurnAbsorptionPower : SCP294ThisTurnChargePower
{
    protected override string IconName => "temporary_health";
    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://OtherworldTreasures/images/powers/temporary_health.png",
        BigIconPath: "res://OtherworldTreasures/images/powers/temporary_health.png");
    protected override Task Expire(PlayerChoiceContext context, Creature target, int remaining)
    {
        TemporaryHealthPower.Spend(target, Math.Min(remaining, TemporaryHealthPower.Available(target)));
        return Task.CompletedTask;
    }
}
