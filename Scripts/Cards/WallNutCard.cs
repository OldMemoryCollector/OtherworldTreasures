using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using OtherworldTreasures.Scripts.Monsters;

namespace OtherworldTreasures.Scripts.Cards;

// 坚果墙召唤卡：【戴夫的种子】右键弹出的三选一选项之一。
// 选中后由遗物扣掉 3 点能量并召唤【坚果墙】。
// 这张卡不会进入牌库/手牌，只用于选择界面，所以不实现 OnPlay。
[RegisterCard(typeof(TokenCardPool))]
public class WallNutCard : ModCardTemplate, IDerivedCard
{
    // 费用
    public const int CardEnergyCost = 2;

    public WallNutCard() : base(CardEnergyCost, CardType.Skill, CardRarity.Token, TargetType.Self, true)
    {
    }

    // 卡面描述里的数值用 {VarName} 占位，直接读植物上的常量 → 改数值不用同步文本
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("Hp", WallNutPlant.MaxHpValue),
        new DynamicVar("Block", WallNutPlant.BlockPerTurn),
    ];

    // 卡图（244x184 横版）
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: "res://OtherworldTreasures/images/cards/Wall_Nut_Card.png"
    );
}
