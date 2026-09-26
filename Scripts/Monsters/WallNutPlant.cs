using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;

namespace OtherworldTreasures.Scripts.Monsters;

// 坚果墙：血量最高，是一面替玩家挨打的墙。
// 特性：每回合给自己加格挡（有概率翻倍）；不再限制单次伤害，超出它血量的伤害由基类规则吞掉。
[RegisterMonster]
public class WallNutPlant : PlantSummonBase
{
    private const string BodyPath = "res://OtherworldTreasures/images/monsters/wall_nut.png";

    // 生命值（三种植物的最高值）：遗物/卡牌文本里的数字都读这个常量
    public const int MaxHpValue = 25;

    public override int MinInitialHp => MaxHpValue;

    public override int MaxInitialHp => MaxHpValue;

    protected override string BodyTexturePath => BodyPath;

    // 贴图里可见坚果墙的中心（按 alpha 范围量取），供原版特效落点对齐
    protected override Vector2 VisualCenterRatio => new(0.501f, 0.514f);

    // 站位：坚果墙站在我们阵型最前面（至少在豌豆射手/向日葵的两倍位置上）
    public override Vector2 LayoutOffset => new(300f, 10f);

    // 每回合获得的格挡（概率翻倍走基类的 DoubleActionChance，和其他植物同一套 20%）
    public const int BlockPerTurn = 6;

    // 回合开始给自己加格挡（20% 概率由基类的"再行动一次"触发，变成 12 点）
    protected override async Task OnPlantTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        await CreatureCmd.GainBlock(Creature, BlockPerTurn, ValueProp.Move, null);
    }
}
