using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Audio;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Godot;

namespace OtherworldTreasures.Scripts.Monsters;

// 植物召唤物的公共基类（来自【戴夫的种子】）。
// 以原版宠物（亡灵契约师的骨手 Osty）的形式站在玩家身旁，但完全自制：
// - 不参与原版的"怪物行动"流程（状态机只有一个空动作会重复）
// - 每回合的行为由模型自身的回合钩子驱动（见 AfterPlayerTurnStart）
// - 玩家受到"被强化的攻击"时，由植物替玩家承受伤害（与原版 DieForYou 一致）
// - 植物死亡后会被正常移出战斗（不保留尸体），所以可以再次召唤
// - 形象用静态图（RitsuLib 的 NCreatureVisuals 工厂），不依赖 Spine；图缺失时走游戏本体 fallback
public abstract class PlantSummonBase : ModMonsterTemplate
{
    // 身体贴图路径（缺失时退回原版 fallback 形象，功能不受影响）
    protected abstract string BodyTexturePath { get; }

    // 形象在画面里的相对大小（源图建议 512x512）
    protected virtual float BodyScale => 0.42f;

    // 形象位置微调（正数 = 向下/向右），用来对站位
    protected virtual Vector2 BodyOffset => Vector2.Zero;

    // 贴图里"可见植物本体"的中心在这张图上的相对位置（0~1，从左上角算起）。
    // 原版特效落点（%CenterPos）默认是给常规体型生物定死的固定点，静态图不改就会偏，
    // 所以这里按各植物贴图的 alpha 范围量出真实中心，再把落点挪过去。
    protected virtual Vector2 VisualCenterRatio => new(0.5f, 0.5f);

    // 随从站位：相对"主人（玩家）节点"的偏移，+X = 靠敌人那一侧，+Y = 往下。
    // 原版把非骨手随从统一摆在 玩家位置 + (植物半宽 - 20, +10)，会和玩家/骨手挤在一起，
    // 所以这里自己摆：豌豆射手/向日葵稍微后移一点 + 下移半个身位（半身位≈110px），让骨手露出来。
    public virtual Vector2 LayoutOffset => new Vector2(100f, 120f);

    // 受击特效落点重定向：原版攻击的 HitVfx 打在"攻击目标"（玩家）身上，
    // 但这一击实际由我们的植物承受 → 把受击特效改到植物身上，玩家不再有受击表现。
    // 只处理"打向玩家"的、看起来是受击类的特效；骨手保留原版表现。
    private static readonly string[] HitVfxKeywords =
        ["attack", "impact", "hit", "slash", "blunt", "stab", "claw", "bite", "punch", "crush", "spike"];

    public static void RedirectHitVfx(ref Creature target, string path)
    {
        if (!target.IsPlayer || target.Player == null)
        {
            return;
        }
        if (!LooksLikeHitVfx(path))
        {
            return;
        }
        var absorber = PetAbsorb.PickAbsorber(target.Player);
        // 骨手保留原版表现；植物与驯服的怪物都该在自己身上显示受击
        if (absorber != null && absorber.Monster is not Osty)
        {
            target = absorber;
        }
    }

    private static bool LooksLikeHitVfx(string path)
    {
        var lower = path.ToLowerInvariant();
        foreach (var keyword in HitVfxKeywords)
        {
            if (lower.Contains(keyword))
            {
                return true;
            }
        }
        return false;
    }

    // 由模型状态（如生命值）决定当前该用哪张贴图；默认恒定
    protected virtual string GetBodyTexturePath() => BodyTexturePath;

    // 召唤物不进图鉴（它们没有独立的美术资源，也不是正式敌人）
    public override bool ShouldShowInCompendium => false;

    // 原版血条：显示（Osty 等随从也走同一套 NCreature 场景）
    public override bool IsHealthBarVisible => true;

    // 没有专属死亡音效（我们的怪物在游戏里没有对应音频资源），复用原版植物怪 Chomper 的死亡音效
    public override bool HasDeathSfx => true;

    public override string DeathSfx => "event:/sfx/enemy/enemy_attacks/chomper/chomper_die";

    // 受击音效：原版在"真正掉血的生物"身上播 HurtSfx（见 CreatureCmd 受击段：Hit 火花 + 受击动画 + HurtSfx），
    // 植物替玩家承伤时 receiver 就是植物自己，所以只要给出路径即可，不需要额外补丁。
    // 复用原版植物怪 Chomper 的受击音（与上面的攻击/死亡音同一套）。
    public override string? HurtSfx => "event:/sfx/enemy/enemy_attacks/chomper/chomper_hurt";

    // 空动作状态机：宠物不走原版"怪物行动"，只是一个占位的可重复状态
    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var idle = new MoveState("NOTHING_MOVE", (IReadOnlyList<Creature> _) => Task.CompletedTask);
        idle.FollowUpState = idle;
        return new MonsterMoveStateMachine(new MonsterState[] { idle }, idle);
    }

    // 用静态图构建召唤物形象（不需要 Spine）；图不存在时返回 null，走游戏本体 fallback 形象
    protected override NCreatureVisuals? TryCreateCreatureVisuals()
    {
        var texture = PlantFx.LoadTexture(GetBodyTexturePath());
        if (texture == null)
        {
            return null;
        }
        return RitsuGodotNodeFactories.CreateFromResource<NCreatureVisuals>(texture);
    }

    // 生物加入战斗后：把形象缩放/贴图设置好
    // （注意：MonsterModel.AfterAddedToRoom 只对敌方生物触发，宠物走的是这个钩子）
    public override Task AfterCreatureAddedToCombat(Creature creature)
    {
        if (creature == Creature)
        {
            RefreshBodyVisual();
            EnsureVanillaHealthBar();
            // 登场音效：复用原版召唤随从（骨手）的音效
            SfxCmd.Play(FmodSfx.necrobinderSummon);
        }
        return Task.CompletedTask;
    }

    // 原版血条（%HealthBar 的 NCreatureStateDisplay）有时会处于"已隐藏"状态：
    // visible=false、alpha=0（实测：内部 HpBarContainer 尺寸是对的，只是父节点被藏了）。
    // Osty 等随从是正常显示的，用的是同一个 NCreature 场景，所以这里主动把它重新播放入场。
    protected void EnsureVanillaHealthBar()
    {
        var creatureNode = Creature.GetCreatureNode();
        if (creatureNode == null)
        {
            return;
        }
        if (creatureNode.GetNodeOrNull<NCreatureStateDisplay>("%HealthBar") is not { } stateDisplay)
        {
            Entry.Logger.Info("[Plant] 找不到 %HealthBar(NCreatureStateDisplay)");
            return;
        }
        var owner = Creature.PetOwner;
        if (stateDisplay.Visible && stateDisplay.Modulate.A > 0.05f)
        {
            return; // 已经正常显示，不重复播动画
        }
        Entry.Logger.Info($"[Plant] 原版血条修正前: visible={stateDisplay.Visible} a={stateDisplay.Modulate.A:0.##} pos={stateDisplay.Position} isMe={owner != null && LocalContext.IsMe(owner)} remotePet={owner == null || !LocalContext.IsMe(owner)}");
        stateDisplay.AnimateIn(HealthBarAnimMode.FromHidden);
        Entry.Logger.Info($"[Plant] 原版血条修正后: visible={stateDisplay.Visible} a={stateDisplay.Modulate.A:0.##} pos={stateDisplay.Position}");
    }

    // 按当前状态刷新身体贴图、缩放与占位矩形。
    // 生物的坐标原点在"脚底"，所以这里让图片底边正好落在原点上，
    // 并把 Bounds（血条/名字/能力栏都以它为基准）设成植物实际占用的矩形。
    protected void RefreshBodyVisual()
    {
        var creatureNode = Creature.GetCreatureNode();
        var visuals = creatureNode?.Visuals;
        var sprite = PlantFx.FindSprite(visuals);
        if (creatureNode == null || visuals == null || sprite == null)
        {
            Entry.Logger.Info($"[Plant] RefreshBodyVisual 跳过：node={creatureNode != null} visuals={visuals != null} sprite={sprite != null}");
            return;
        }

        var texture = PlantFx.LoadTexture(GetBodyTexturePath()) ?? sprite.Texture;
        if (texture == null)
        {
            return;
        }

        // 1) 先把父级缩放归位（NCreatureVisuals.SetScaleAndHue 会把 Visuals.Scale 设为 1）
        creatureNode.SetScaleAndHue(1f, 0f);

        // 2) Sprite2D 中心归零 + 向上偏半个图高 → 画出来的"底边中心"正好落在节点原点上；
        //    再把节点原点挪到生物原点（脚底）+ 微调，这样无论工厂内部的父子偏移如何都准
        sprite.Texture = texture;
        sprite.Scale = Vector2.One * BodyScale;
        sprite.Offset = new Vector2(0f, -texture.GetHeight() / 2f);
        sprite.GlobalPosition = creatureNode.GlobalPosition + BodyOffset;
        // 身体不要盖住血条（血条是 NCreature 场景里 Visuals 的兄弟节点，按绘制顺序排）
        sprite.ZIndex = 0;

        // 3) Bounds 贴合植物实际占用的矩形（左上角在原点上方）
        //    血条/名字/能力栏的位置与宽度都以它为基准
        var size = new Vector2(texture.GetWidth(), texture.GetHeight()) * BodyScale;
        var bounds = visuals.Bounds;
        bounds.Size = size;
        bounds.GlobalPosition = creatureNode.GlobalPosition + new Vector2(-size.X / 2f, -size.Y) + BodyOffset;

        // 形象整条链路的 ZIndex 归零：血条是 NCreature 场景里 Visuals 的兄弟节点，
        // 若形象链路带了正的 ZIndex 就会把血条盖住
        visuals.ZIndex = 0;

        // 4) 原版"特效落点"标记（%CenterPos）挪到植物实体中心：
        //    它是给常规体型生物定死的固定点，静态图植物不改就会偏（表现为打击特效偏左上）；
        //    生物的坐标原点在图片底边中心，所以"实体中心"= (比例X-0.5)*宽 、(比例Y-1)*高。
        if (visuals.GetNodeOrNull<Marker2D>("%CenterPos") is { } vfxMarker)
        {
            var visualCenter = new Vector2(
                (VisualCenterRatio.X - 0.5f) * texture.GetWidth() * BodyScale,
                (VisualCenterRatio.Y - 1f) * texture.GetHeight() * BodyScale);
            vfxMarker.GlobalPosition = creatureNode.GlobalPosition + visualCenter + BodyOffset;
            Entry.Logger.Info($"[Plant] {GetType().Name} 特效落点: {vfxMarker.GlobalPosition}（中心偏移={visualCenter}）");
        }

        // 5) 让游戏用新的 Bounds 重算 Hitbox 与血条位置
        creatureNode.SetScaleAndHue(1f, 0f);

        Entry.Logger.Info($"[Plant] {GetType().Name} 形象: scale={BodyScale} offset={BodyOffset} size={size} bounds={bounds.Position} creature={creatureNode.GlobalPosition} spriteZ={sprite.ZIndex}");
    }

    // 形象在画面上的高度（像素），供特效定位用
    protected float BodyHeightPx => (PlantFx.LoadTexture(GetBodyTexturePath())?.GetHeight() ?? 0) * BodyScale;

    // === 承伤链 ===
    // 顺序（坚果墙 > 骨手 > 驯服的怪物 > 豌豆射手 > 向日葵 > 玩家）统一由 PetAbsorb 实现：
    // 玩家可能同时拥有我们的植物和【桃太郎丸子】驯服的怪物，两边都调用同一份逻辑才能保证顺序一致。

    public override Creature ModifyUnblockedDamageTarget(Creature target, decimal amount, ValueProp props, Creature? dealer)
        => PetAbsorb.ModifyTarget(Creature.PetOwner, target, props);

    public override decimal ModifyHpLostBeforeOsty(Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        PetAbsorb.ResetBeforeDamage(Creature.PetOwner, target);
        return amount;
    }

    public override decimal ModifyHpLostAfterOsty(Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
        => PetAbsorb.ModifyOverflow(Creature.PetOwner, target, amount, props, dealer);

    // 每回合的行动：必定 1 次；另有 DoubleActionChance 的概率再行动 1 次。
    // 向日葵/豌豆射手是回能或攻击翻倍，坚果墙是格挡翻倍，三种植物的概率一致。
    protected virtual float DoubleActionChance => 0.2f;

    // 玩家回合开始时行动（只在该植物主人的回合触发）
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        var owner = Creature.PetOwner;
        if (owner == null || owner != player || !Creature.IsAlive)
        {
            return;
        }
        // 兜底：万一原版血条又被隐藏（例如中途读档/悬停事件），这里恢复一次
        EnsureVanillaHealthBar();
        await PerformTurnActions(choiceContext, player);
    }

    // 召唤当回合立刻行动一次（由【戴夫的种子】在召唤后调用，因为回合开始的钩子已经过去了）
    public async Task ActOnSummonTurn(PlayerChoiceContext choiceContext, Player player)
    {
        if (!Creature.IsAlive)
        {
            return;
        }
        await PerformTurnActions(choiceContext, player);
    }

    // 行动一次 + DoubleActionChance 概率再来一次
    private async Task PerformTurnActions(PlayerChoiceContext choiceContext, Player player)
    {
        await OnPlantTurnStart(choiceContext, player);
        if (!Creature.IsAlive)
        {
            return;
        }
        // 用 run 的随机数，联机下各端结果一致
        if (player.RunState.Rng.Niche.NextFloat() < DoubleActionChance)
        {
            await OnPlantTurnStart(choiceContext, player);
        }
    }

    // 【施肥】卡给的成长值（遗物/卡牌文本里的数字都读这个常量）
    public const int GrowthAmount = 5;

    // 找主人场上活着的植物（每场战斗至多召唤一只）
    public static Creature? FindLivingPlant(Player owner)
    {
        var pets = owner.PlayerCombatState?.Pets;
        if (pets == null)
        {
            return null;
        }
        foreach (var pet in pets)
        {
            if (pet != null && pet.IsAlive && pet.Monster is PlantSummonBase)
            {
                return pet;
            }
        }
        return null;
    }

    // 【施肥】的效果：给场上的植物 +amount 最大生命并回复等量生命（等同骨手那边的"召唤"）
    public static async Task<bool> GrowPlant(Player owner, int amount)
    {
        var plant = FindLivingPlant(owner);
        if (plant == null)
        {
            return false;
        }
        await CreatureCmd.GainMaxHp(plant, amount);
        return true;
    }

    // 各植物自己的回合行为
    protected abstract Task OnPlantTurnStart(PlayerChoiceContext choiceContext, Player player);
}
