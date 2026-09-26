using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;

namespace OtherworldTreasures.Scripts.Monsters;

// 豌豆射手：血量中等，每回合射出一颗豌豆，对"第一位敌人"造成伤害。
[RegisterMonster]
public class PeaShooterPlant : PlantSummonBase
{
    // 每回合对第一名敌人造成的伤害
    public const int DamagePerTurn = 7;

    // 身体贴图 / 豌豆贴图；文件不存在时跳过对应视觉，功能不受影响
    private const string BodyPath = "res://OtherworldTreasures/images/monsters/pea_shooter.png";
    private const string PeaPath = "res://OtherworldTreasures/images/fx/pea.png";

    // 命中特效（原版 VFX）
    private const string HitVfx = "vfx/vfx_slime_impact";

    // 发射音效：复用原版植物怪 Chomper 的攻击音
    private const string ShotSfx = "event:/sfx/enemy/enemy_attacks/chomper/chomper_attack";

    // 豌豆在画面上的目标边长（像素）
    private const float PeaSizePx = 90f;

    // 生命值：遗物/卡牌文本里的数字都读这个常量
    public const int MaxHpValue = 20;

    // 生命值（中等）
    public override int MinInitialHp => MaxHpValue;

    public override int MaxInitialHp => MaxHpValue;

    protected override string BodyTexturePath => BodyPath;

    // 贴图里可见豌豆射手的中心（按 alpha 范围量取），供原版特效落点对齐
    protected override Vector2 VisualCenterRatio => new(0.509f, 0.549f);

    protected override async Task OnPlantTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        var combatState = CombatManager.Instance.DebugOnlyGetState();
        if (combatState == null)
        {
            return;
        }

        // 第一位敌人（按原版站位顺序）
        var target = combatState.HittableEnemies.FirstOrDefault();
        if (target == null)
        {
            return;
        }

        SfxCmd.Play(ShotSfx);
        await ShootPea(target);

        // ValueProp.Move = 攻击伤害（可被格挡、可被"伤害减免"等效果影响）
        await CreatureCmd.Damage(choiceContext, target, DamagePerTurn, ValueProp.Move, Creature);

        VfxCmd.PlayOnCreatureCenter(target, HitVfx);
    }

    // 射出一颗豌豆：从植物飞到目标身上
    private async Task ShootPea(Creature target)
    {
        var peaTexture = PlantFx.LoadTexture(PeaPath);
        var creatureNode = Creature.GetCreatureNode();
        var targetNode = target.GetCreatureNode();
        if (peaTexture == null || creatureNode == null || targetNode == null)
        {
            return;
        }

        // 从植物"嘴部"高度射出
        var muzzle = creatureNode.GlobalPosition + new Vector2(0f, -BodyHeightPx * 0.55f);
        var pea = PlantFx.SpawnSprite(creatureNode.Visuals, peaTexture, muzzle, PeaSizePx / peaTexture.GetWidth());
        var destination = targetNode.VfxSpawnPosition;
        try
        {
            var fly = pea.CreateTween().SetParallel();
            fly.TweenProperty(pea, "global_position", destination, 0.3f)
                .SetTrans(Tween.TransitionType.Quad)
                .SetEase(Tween.EaseType.Out);
            // 让豌豆朝向飞行方向
            fly.TweenProperty(pea, "rotation", (destination - muzzle).Angle(), 0.1f);
            await PlantFx.AwaitTween(pea, fly, 0.6f);
        }
        catch (Exception e)
        {
            Entry.Logger.Info($"[PeaShooter] 豌豆表现异常（已忽略）：{e.Message}");
        }
        finally
        {
            if (GodotObject.IsInstanceValid(pea))
            {
                pea.QueueFree();
            }
        }
    }
}
