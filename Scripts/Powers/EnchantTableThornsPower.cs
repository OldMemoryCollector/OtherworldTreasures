using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Powers;

[RegisterPower]
public sealed class EnchantTableThornsPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override LocString Title => new("powers", "OTHERWORLD_TREASURES_POWER_ENCHANT_TABLE_THORNS.title");
    public override LocString Description => new("powers", "OTHERWORLD_TREASURES_POWER_ENCHANT_TABLE_THORNS.description");
    public override async Task BeforeDamageReceived(PlayerChoiceContext context, Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target == Owner && dealer != null && props.IsPoweredAttack())
            await CreatureCmd.Damage(context, dealer, Amount, ValueProp.Unpowered | ValueProp.SkipHurtAnim, Owner, null, null);
    }
    public override async Task AfterSideTurnEnd(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player && Owner.IsPlayer) await PowerCmd.Remove(this);
    }
}
