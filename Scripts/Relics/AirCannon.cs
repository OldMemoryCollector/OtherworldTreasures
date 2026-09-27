using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models.RelicPools;
using OtherworldTreasures.Scripts.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Relics;

// 空气炮（哆啦A梦的道具之一）：战斗开始时，将一张 1 费【碰】卡牌加入手牌。
[RegisterRelic(typeof(SharedRelicPool))]
public class AirCannon : ModRelicTemplate, IDoraemonItem
{
    // 哆啦A梦道具：自定义稀有度
    public override RelicRarity Rarity => ModRelicRarity.Doraemon;

    // 原版 MerchantCost 的 switch 遇到未知稀有度会抛异常，这里直接给值
    public override int MerchantCost => 999999999;

    protected override string IconBaseName => "air_cannon";

    protected override string BigIconPath => "res://OtherworldTreasures/images/relics/Air_Cannon.png";

    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://OtherworldTreasures/images/relics/Air_Cannon.png",
        IconOutlinePath: "res://OtherworldTreasures/images/relics/Air_Cannon.png",
        BigIconPath: "res://OtherworldTreasures/images/relics/Air_Cannon.png"
    );

    // Late 阶段：初始手牌抽完后再把【碰】放进手牌
    public override async Task BeforeCombatStartLate()
    {
        if (Owner?.Creature != null)
        {
            Flash();
            await CardPileCmd.AddToCombatAndPreview<AirCannonBashCard>(
                Owner.Creature, PileType.Hand, 1, Owner);
        }
        await base.BeforeCombatStartLate();
    }
}
