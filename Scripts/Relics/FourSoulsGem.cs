using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Relics;

/// <summary>
/// 四魂之玉（先古遗物）：本身没有任何效果，只用于在旧忆收藏家的选项里呈现它的作用。
/// 点击后实际获得的是随机的【四魂之玉碎片】——即"获取时破损"。
/// </summary>
[RegisterRelic(typeof(SharedRelicPool))]
public class FourSoulsGem : ModRelicTemplate
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    // 美术：256×256（根目录投放的源图已移入打包目录并清理）
    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://OtherworldTreasures/images/relics/Four_Souls_Gem.png",
        IconOutlinePath: "res://OtherworldTreasures/images/relics/Four_Souls_Gem.png",
        BigIconPath: "res://OtherworldTreasures/images/relics/Four_Souls_Gem.png");

    public override async Task AfterObtained()
    {
        // 破碎：本体不进遗物栏，直接换成碎片
        if (Owner != null)
        {
            var fragment = (FourSoulsFragment)ModelDb.Relic<FourSoulsFragment>().ToMutable();
            fragment.InitializeAsNew(Owner.RunState.Rng.Shuffle);
            await RelicCmd.Replace(this, fragment);
            Entry.Logger.Info("[FourSouls] 四魂之玉破碎，已换成四魂之玉碎片");
        }
        await base.AfterObtained();
    }
}
