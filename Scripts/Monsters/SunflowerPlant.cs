using System;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using STS2RitsuLib.Interop.AutoRegistration;

namespace OtherworldTreasures.Scripts.Monsters;

// 向日葵：血量最低，每回合提供 1 点能量（阳光掉落 → 飞向能量计数器 → 加能量）。
[RegisterMonster]
public class SunflowerPlant : PlantSummonBase
{
    // 每回合提供的能量
    public const int EnergyPerTurn = 1;

    // 生命值（偏低）：遗物/卡牌文本里的数字都读这个常量
    public const int MaxHpValue = 15;

    // 身体贴图 / 阳光贴图；文件不存在时跳过对应视觉，功能不受影响
    private const string BodyPath = "res://OtherworldTreasures/images/monsters/sunflower.png";
    private const string SunPath = "res://OtherworldTreasures/images/fx/sun.png";

    // 阳光在画面上的目标边长（像素）
    private const float SunSizePx = 200f;

    // 生命值（偏低）
    public override int MinInitialHp => MaxHpValue;

    public override int MaxInitialHp => MaxHpValue;

    protected override string BodyTexturePath => BodyPath;

    // 贴图里可见向日葵的中心（按 alpha 范围量取），供原版特效落点对齐
    protected override Vector2 VisualCenterRatio => new(0.507f, 0.502f);

    protected override async Task OnPlantTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        await PlaySun();
        await PlayerCmd.GainEnergy(EnergyPerTurn, player);
    }

    // 阳光：先从向日葵上方掉下来，再飞向能量计数器
    private async Task PlaySun()
    {
        var sunTexture = PlantFx.LoadTexture(SunPath);
        var creatureNode = Creature.GetCreatureNode();
        if (sunTexture == null || creatureNode == null)
        {
            return;
        }

        var sunScale = SunSizePx / sunTexture.GetWidth();
        var sun = PlantFx.SpawnSprite(
            creatureNode.Visuals,
            sunTexture,
            creatureNode.GlobalPosition + new Vector2(0f, -BodyHeightPx - 40f),
            sunScale);
        try
        {
            // 1) 掉落一小段
            var drop = sun.CreateTween();
            drop.TweenProperty(sun, "global_position", sun.GlobalPosition + new Vector2(0f, 55f), 0.3f)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.In);
            drop.TweenInterval(0.08f);
            await PlantFx.AwaitTween(sun, drop, 0.6f);

            // 2) 飞向能量计数器（边飞边缩小）
            var target = PlantFx.GetEnergyCounterCanvasPosition(creatureNode.Visuals);
            if (target.HasValue)
            {
                var fly = sun.CreateTween().SetParallel();
                fly.TweenProperty(sun, "global_position", target.Value, 0.45f)
                    .SetTrans(Tween.TransitionType.Cubic)
                    .SetEase(Tween.EaseType.InOut);
                fly.TweenProperty(sun, "scale", Vector2.One * (sunScale * 0.45f), 0.45f);
                await PlantFx.AwaitTween(sun, fly, 0.8f);
            }
        }
        catch (Exception e)
        {
            Entry.Logger.Info($"[Sunflower] 阳光表现异常（已忽略）：{e.Message}");
        }
        finally
        {
            if (GodotObject.IsInstanceValid(sun))
            {
                sun.QueueFree();
            }
        }
    }
}
