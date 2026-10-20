using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Powers;

[RegisterPower]
public sealed class ShimmerMetallicizePower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override LocString Title => new("powers", "OTHERWORLD_TREASURES_POWER_SHIMMER_METALLICIZE.title");
    public override LocString Description => new("powers", "OTHERWORLD_TREASURES_POWER_SHIMMER_METALLICIZE.description");
    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://OtherworldTreasures/images/relics/AegisFruit_85.png",
        BigIconPath: "res://OtherworldTreasures/images/relics/AegisFruit.png");
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => base.AdditionalHoverTips.Append(HoverTipFactory.Static(StaticHoverTip.Block));
    public override async Task BeforeSideTurnEndEarly(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants)
    {
        if (Owner.IsAlive && participants.Contains(Owner))
        {
            Flash();
            await CreatureCmd.GainBlock(Owner, Amount, ValueProp.Unpowered, null);
        }
    }
    public override Task AfterCombatEnd(CombatRoom room) => PowerCmd.Remove(this);
}
