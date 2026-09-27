using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Powers;

/// <summary>
/// 孢子囊面板：纯展示能力，没有任何效果。
/// 挂在玩家身上，数量 = 当前回合受到攻击时会给攻击者施加的中毒层数
/// （基础层数 + 战斗内加成），由 SporeSac 在战斗开始时挂上、每个玩家回合开始刷新。
/// 大图标复用遗物图，小图标用像素版孢子囊。
/// </summary>
[RegisterPower]
public class SporeSacPower : ModPowerTemplate
{
    public override PowerType Type => PowerType.Buff;

    // 数量直接显示在图标上，随回合增长
    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://OtherworldTreasures/images/powers/spore_sac.png",
        BigIconPath: "res://OtherworldTreasures/images/relics/Spore_Sac.png");

    // 本地化：显式指定 key（与 TamedPet 等保持一致，不依赖模板 Id 推导）
    public override LocString Title => new LocString("powers", "OTHERWORLD_TREASURES_POWER_SPORE_SAC_POWER.title");

    public override LocString Description => new LocString("powers", "OTHERWORLD_TREASURES_POWER_SPORE_SAC_POWER.description");
}
