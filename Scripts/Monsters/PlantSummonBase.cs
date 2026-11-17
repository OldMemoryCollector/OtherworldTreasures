using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Audio;
using MegaCrit.Sts2.Core.Combat;
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
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
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
    protected virtual float BodyScale => 0.30f;
    private float _layoutScale = 1f;
    private float DisplayScale => BodyScale * _layoutScale;

    // 形象位置微调（正数 = 向下/向右），用来对站位
    protected virtual Vector2 BodyOffset => Vector2.Zero;

    // 贴图里"可见植物本体"的中心在这张图上的相对位置（0~1，从左上角算起）。
    // 原版特效落点（%CenterPos）默认是给常规体型生物定死的固定点，静态图不改就会偏，
    // 所以这里按各植物贴图的 alpha 范围量出真实中心，再把落点挪过去。
    protected virtual Vector2 VisualCenterRatio => new(0.5f, 0.5f);

    // Preferred position beside the owner. Actual placement also reserves every
    // other creature's body and health bar in the multiplayer formation.
    public virtual Vector2 LayoutOffset => new Vector2(185f, 0f);

    // 把场上所有植物按各自 LayoutOffset 重摆一次。
    // 原版 NCombatRoom.AddCreature 会把"非骨手随从"统一挪到一个固定位置，会把先前召唤的植物、
    // 以及驯服随从一并带跑；所以每次有新的随从加入时，两类随从都要整体重摆（见 GamePatches 的两个排版补丁）。
    internal static void ApplyPlantLayouts(NCombatRoom room)
    {
        // A fixed offset can put one player's pet directly on another player.
        // Reserve both bodies and their health/status bars before placing pets.
        var occupied = room.CreatureNodes
            .Where(n => n.Entity.IsAlive && n.Entity.Monster is not PlantSummonBase)
            .Select(n => n.Hitbox.GetGlobalRect().GrowIndividual(12f, 18f, 12f, 84f))
            .ToList();
        var plants = room.CreatureNodes
            .Where(n => n.Entity.IsAlive && n.Entity.Monster is PlantSummonBase)
            .OrderBy(n => room.GetCreatureNode(n.Entity.PetOwner?.Creature)?.GlobalPosition.X ?? 0f)
            .ThenBy(n => n.Entity.CombatId)
            .ToArray();
        if (plants.Length == 0) return;

        // The multiplayer roster extends downwards as players join. It is a
        // real obstruction even though it is outside the combat creature tree.
        if (NRun.Instance?.GlobalUi.MultiplayerPlayerContainer is { } roster && roster.IsVisibleInTree())
        {
            Rect2? panel = null;
            void Visit(Node node)
            {
                if (node is Control control && control.IsVisibleInTree())
                {
                    var rect = control.GetGlobalRect();
                    if (rect.HasArea()) panel = panel?.Merge(rect) ?? rect;
                }
                foreach (var child in node.GetChildren()) Visit(child);
            }
            foreach (var child in roster.GetChildren()) Visit(child);
            if (panel is { } hud)
                occupied.Add(new Rect2(new Vector2(0f, hud.Position.Y - 12f), new Vector2(hud.End.X + 12f, hud.Size.Y + 24f)));
        }

        // Plan the entire formation before moving any plant. The previous
        // per-plant fallback put later pets back on their owner when rows filled.
        float right = room.CreatureNodes.Where(n => n.Entity.IsAlive && n.Entity.Side == CombatSide.Enemy)
            .Select(n => n.Hitbox.GetGlobalRect().Position.X - 12f).DefaultIfEmpty(1908f).Min();
        var stage = new Rect2(12f, 180f, Math.Max(0f, right - 12f), 770f);
        foreach (bool keepOwnerRow in new[] { true, false })
        foreach (float factor in new[] { 1f, .9f, .8f, .7f, .6f, .5f, .45f, .4f })
        {
            var free = new List<Rect2> { stage };
            foreach (var rect in occupied) free = SubtractPlantSpace(free, rect);
            var planned = new Vector2[plants.Length];
            int attempts = 0;
            if (!PlanPlantPositions(room, plants, 0, factor, keepOwnerRow, free, planned, ref attempts)) continue;
            foreach (var node in plants)
            {
                var plant = (PlantSummonBase)node.Entity.Monster!;
                if (Math.Abs(plant._layoutScale - factor) > .001f)
                {
                    plant._layoutScale = factor;
                    plant.RefreshBodyVisual(updateLayout: false);
                }
            }
            for (int i = 0; i < plants.Length; i++) plants[i].GlobalPosition = planned[i];
            return;
        }
        Entry.Logger.Info("[Plant] No unobstructed allied formation fits the current encounter.");
    }

    private static List<Rect2> SubtractPlantSpace(List<Rect2> spaces, Rect2 obstacle)
    {
        var result = new List<Rect2>();
        foreach (var space in spaces)
        {
            if (!space.Intersects(obstacle)) { result.Add(space); continue; }
            var cut = space.Intersection(obstacle);
            if (cut.Position.X > space.Position.X) result.Add(new Rect2(space.Position, new Vector2(cut.Position.X - space.Position.X, space.Size.Y)));
            if (cut.End.X < space.End.X) result.Add(new Rect2(new Vector2(cut.End.X, space.Position.Y), new Vector2(space.End.X - cut.End.X, space.Size.Y)));
            if (cut.Position.Y > space.Position.Y) result.Add(new Rect2(space.Position, new Vector2(space.Size.X, cut.Position.Y - space.Position.Y)));
            if (cut.End.Y < space.End.Y) result.Add(new Rect2(new Vector2(space.Position.X, cut.End.Y), new Vector2(space.Size.X, space.End.Y - cut.End.Y)));
        }
        // Overlapping maximal rectangles are intentional. Remove contained
        // duplicates so a crowded scene does not grow the search exponentially.
        for (int i = result.Count - 1; i >= 0; i--)
            if (result.Where((_, j) => j != i).Any(r => r.Encloses(result[i]))) result.RemoveAt(i);
        return result;
    }

    private static bool PlanPlantPositions(NCombatRoom room, NCreature[] nodes, int index, float factor,
        bool keepOwnerRow, List<Rect2> free, Vector2[] planned, ref int attempts)
    {
        if (index == nodes.Length) return true;
        if (++attempts > 3000) return false;
        var node = nodes[index];
        var plant = (PlantSummonBase)node.Entity.Monster!;
        var ownerNode = node.Entity.PetOwner is { } owner ? room.GetCreatureNode(owner.Creature) : null;
        if (ownerNode == null) return false;
        var body = node.Hitbox.GetGlobalRect();
        float ratio = factor / plant._layoutScale;
        var size = body.Size * ratio;
        var offset = (body.Position - node.GlobalPosition - plant.BodyOffset) * ratio + plant.BodyOffset;
        // Include the leading block icon, health text and one status row.
        var padding = new Vector2(36f, 12f);
        var areaSize = size + new Vector2(48f, 96f);
        var desired = ownerNode.GlobalPosition + plant.LayoutOffset;
        var desiredArea = desired + offset - padding;
        var candidates = free.Where(r => r.Size.X >= areaSize.X && r.Size.Y >= areaSize.Y)
            .Select(r => new Vector2(Math.Clamp(desiredArea.X, r.Position.X, r.End.X - areaSize.X),
                Math.Clamp(desiredArea.Y, r.Position.Y, r.End.Y - areaSize.Y)))
            .Where(p => !keepOwnerRow || Math.Abs(p.Y - desiredArea.Y) <= 1f)
            .Distinct().OrderBy(p => Math.Abs(p.Y - desiredArea.Y))
            .ThenBy(p => p.DistanceSquaredTo(desiredArea)).ToArray();
        foreach (var position in candidates)
        {
            planned[index] = position + padding - offset;
            if (PlanPlantPositions(room, nodes, index + 1, factor, keepOwnerRow,
                SubtractPlantSpace(free, new Rect2(position, areaSize)), planned, ref attempts)) return true;
        }
        return false;
    }

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
    #if !STS2_113
    public override bool IsHealthBarVisible => true;
    #endif

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
    protected void RefreshBodyVisual(bool updateLayout = true)
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
        sprite.Scale = Vector2.One * DisplayScale;
        sprite.Offset = new Vector2(0f, -texture.GetHeight() / 2f);
        sprite.GlobalPosition = creatureNode.GlobalPosition + BodyOffset;
        // 身体不要盖住血条（血条是 NCreature 场景里 Visuals 的兄弟节点，按绘制顺序排）
        sprite.ZIndex = 0;

        // 3) Bounds 贴合植物实际占用的矩形（左上角在原点上方）
        //    血条/名字/能力栏的位置与宽度都以它为基准
        var size = new Vector2(texture.GetWidth(), texture.GetHeight()) * DisplayScale;
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
                (VisualCenterRatio.X - 0.5f) * texture.GetWidth() * DisplayScale,
                (VisualCenterRatio.Y - 1f) * texture.GetHeight() * DisplayScale);
            vfxMarker.GlobalPosition = creatureNode.GlobalPosition + visualCenter + BodyOffset;
            Entry.Logger.Info($"[Plant] {GetType().Name} 特效落点: {vfxMarker.GlobalPosition}（中心偏移={visualCenter}）");
        }

        // 5) 让游戏用新的 Bounds 重算 Hitbox 与血条位置
        creatureNode.SetScaleAndHue(1f, 0f);

        // The AddCreature postfix runs before the custom sprite bounds exist.
        // Place again once the actual body and health bar have their final size.
        if (updateLayout && NCombatRoom.Instance is { } room)
            ApplyPlantLayouts(room);

        Entry.Logger.Info($"[Plant] {GetType().Name} 形象: scale={DisplayScale} offset={BodyOffset} size={size} bounds={bounds.Position} creature={creatureNode.GlobalPosition} spriteZ={sprite.ZIndex}");
    }

    // 形象在画面上的高度（像素），供特效定位用
    protected float BodyHeightPx => (PlantFx.LoadTexture(GetBodyTexturePath())?.GetHeight() ?? 0) * DisplayScale;

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
