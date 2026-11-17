using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models.CardPools;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Cards;

// 【灵犀一指】的抉择选项，只用于弹出选择界面，不会真的被打出。
[RegisterCard(typeof(TokenCardPool))]
public class LingxiFingerStrike : ModCardTemplate
{
    private const string PlaceholderPortrait = "res://images/powers/missing_power.png";

    public LingxiFingerStrike() : base(0, CardType.Skill, CardRarity.Token, TargetType.None, DevelopmentContent.Extras)
    {
    }

    public override bool CanBeGeneratedInCombat => false;
    public override bool CanBeGeneratedByModifiers => false;
    public override CardAssetProfile AssetProfile => new(PortraitPath: PlaceholderPortrait);
}
