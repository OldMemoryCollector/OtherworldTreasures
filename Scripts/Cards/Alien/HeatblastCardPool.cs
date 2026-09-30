using Godot;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Cards.Alien;

/// <summary>
/// 火焰人专属卡池。
/// 作用只有一个：让火焰人的专属牌在卡牌图鉴里有自己的一个分类
/// （注册为共享卡池，再由 Entry.Init 注册图鉴筛选器，两者缺一不可）。
/// 不参与任何随机抽取：卡牌奖励、商店、随机生成都各自按原版的池走，摸不到这里。
/// 外观直接沿用战士（铁甲战士）的原版资源——红卡框、战士能量色与配色，
/// 这样不需要另外做能量图标和卡框图标。
/// </summary>
[RegisterSharedCardPool]
public class HeatblastCardPool : TypeListCardPoolModel
{
    // 卡池 ID，必须唯一防撞车
    public override string Title => "heatblast";

    // 沿用战士的能量色：能量图标会自动取原版 ironclad 那套（无需自制图标）
    public override string EnergyColorName => "ironclad";

    // 沿用战士的红色卡框
    public override string CardFrameMaterialPath => "card_frame_red";

    // 图鉴里查看该类卡牌时的卡背颜色（取战士的）
    public override Color DeckEntryCardColor => new Color("D62000");

    // 能量表盘数字的描边色（取战士的，与上面的能量图标相配）
    public override Color EnergyOutlineColor => new Color("802020");

    // 不是无色池（与无色池、衍生池区分开）
    public override bool IsColorless => false;
}
