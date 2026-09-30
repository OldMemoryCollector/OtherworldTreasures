using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using OtherworldTreasures.Scripts.FourSouls;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Ui.Toast;

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
            // 本层地图节点早已建好，这里主动补挂一次光芒，避免"获得后要退出重进才看得到四魂怪"
            Patch_NNormalMapPoint_FourSoulsGlow.RefreshGlows(Owner.RunState);
            // 非侵入式提示：说明"破碎 → 碎片四散 → 敌人被强化"这一效果
            RitsuToastService.ShowInfo(
                Loc("OTHERWORLD_TREASURES_TOAST_FOUR_SOULS_BODY"),
                Loc("OTHERWORLD_TREASURES_TOAST_FOUR_SOULS_TITLE"));
            Entry.Logger.Info("[FourSouls] 四魂之玉破碎，已换成四魂之玉碎片");
        }
        await base.AfterObtained();
    }

    private static string Loc(string key) => new LocString("gameplay_ui", key).GetFormattedText();
}
