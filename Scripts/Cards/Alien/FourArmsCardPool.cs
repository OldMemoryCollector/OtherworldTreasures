using Godot;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Cards.Alien;

[RegisterSharedCardPool]
public class FourArmsCardPool : TypeListCardPoolModel
{
    public override string Title => "four_arms";
    public override string EnergyColorName => "ironclad";
    public override string CardFrameMaterialPath => "card_frame_red";
    public override Color DeckEntryCardColor => new("D62000");
    public override Color EnergyOutlineColor => new("802020");
    public override bool IsColorless => false;
}
