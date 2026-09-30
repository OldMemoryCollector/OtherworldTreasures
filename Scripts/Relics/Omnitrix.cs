using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using OtherworldTreasures.Scripts.Alien;
using OtherworldTreasures.Scripts.Powers;
using OtherworldTreasures.Scripts.UI;
using STS2RitsuLib;
using STS2RitsuLib.Interactions.RightClick;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Relics;

/// <summary>
/// 小破表（Omnitrix）：右键点击变身成为外星英雄。
/// - 战斗内右键：播放变身动画 → 显示英雄选择界面 → 选中后替换角色形象
/// - 可选英雄：火焰人、四手霸王、超能兽
/// - 极低概率变身被鬼影替换
/// - 变身持续 3 个回合（由 OmnitrixTransformPower 计时），可以重复变身，
///   但变回原形后要等 CooldownTurns 个回合才能再变（内部规则，不对玩家显示）
/// 与先古遗物池成员。
/// </summary>
[RegisterRelic(typeof(SharedRelicPool))]
public class Omnitrix : ModRelicTemplate, IModRightClickableRelic
{
    // 鬼影替换概率（极低）
    private const double GhostFreakChance = 0.02;

    // 火焰人的三套专精：变身时随机抽一套（玩家不可选）
    private static readonly AlienSpecialization[] HeatblastSpecs =
    {
        AlienSpecialization.Blast,
        AlienSpecialization.Shaping,
        AlienSpecialization.Propulsion,
    };

    // 音效（放在 OtherworldTreasures/audio/ 下，随模组 pck 打包）
    private const string StartSfxPath = "res://OtherworldTreasures/audio/omnitrix_start.wav";
    private const string TransformSfxPath = "res://OtherworldTreasures/audio/omnitrix_transform.wav";
    internal const string SwitchSfxPath = "res://OtherworldTreasures/audio/omnitrix_switch.wav";
    private const string EndSfxPath = "res://OtherworldTreasures/audio/omnitrix_end.wav";

    // 变回原形后的冷却回合数（内部规则，不向玩家展示，遗物描述里也不提次数/冷却）
    private const int CooldownTurns = 6;

    // 冷却结束的回合号：玩家回合号 >= 这个值才能再次变身（static：战斗中遗物会被克隆，需跨实例共享）
    private static int s_cooldownUntilTurn;

    // 变身中被隐藏的原身体节点、叠加的英雄立绘节点（解除变身后恢复时用）
    private static Node2D? s_hiddenBody;
    private static Node2D? s_overlaySprite;

    // 变身前状态快照（原牌组 + 自身能力），变身结束时还原
    private static TransformSnapshot? s_transformSnapshot;

    // 变身结束后还要补扫几个回合的英雄牌残留。
    // 带【保留】的英雄牌（炎狱天降）在还原那一刻既不在任何牌堆里（它要等下一回合开始才回到手牌），
    // 所以还原那一次必然扫不干净；而且补扫时机可能早于"保留牌回手牌"，一次扫空不能就收手，
    // 因此这里用"剩余补扫回合数"，固定往后扫几回合。
    private static int s_alienCleanupTurnsLeft;

    // 先古遗物（由旧忆收藏家给出）
    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://OtherworldTreasures/images/relics/Omnitrix.png",
        // 不给轮廓图的话，原版会去 relic_outline_atlas 里找同名 sprite 并报缺失警告
        IconOutlinePath: "res://OtherworldTreasures/images/relics/Omnitrix.png",
        BigIconPath: "res://OtherworldTreasures/images/relics/Omnitrix.png");

    // 右键预检：注册模型身份令牌
    public bool CanHandleRightClickLocal(ModRightClickContext context)
    {
        RitsuLibFramework.EnsureModelIdentity(this);
        return true;
    }

    // 战斗内、不在变身中、且冷却结束才能右键（不满足时右键无反应，不做任何提示）
    public bool CanExecuteRightClick(ModRightClickExecutionContext context)
    {
        var player = Owner;
        var combatState = player?.PlayerCombatState;
        if (player == null || combatState == null || combatState.TurnNumber < s_cooldownUntilTurn)
        {
            return false;
        }
        return !IsTransformed(player);
    }

    // "正在变身"以玩家身上的变身能力为准：这样读档后（能力随存档恢复）也不会误判成没变身
    private static bool IsTransformed(Player player)
        => player.Creature?.Powers?.Any(p => p is OmnitrixTransformPower) ?? false;

    // 每场战斗开始：冷却清零（打一场新仗时总是可以变身）
    public override Task BeforeCombatStart()
    {
        s_cooldownUntilTurn = 0;
        return base.BeforeCombatStart();
    }

    // 变身结束后补扫残留的英雄牌（主要是带【保留】的炎狱天降，它在回合交替期间可能还没回到手牌）。
    // 连续一轮扫不到就解除标记，不会一直扫下去。
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (s_alienCleanupTurnsLeft > 0 && player == Owner)
        {
            // 回合管线里的钩子抛异常会打断整条管线（表现为卡死），必须兜住
            try
            {
                s_alienCleanupTurnsLeft--;
                int removed = await TransformSnapshot.RemoveAlienCards(player);
                Entry.Logger.Info(
                    $"[Omnitrix] 补扫残留英雄牌：本次移除 {removed} 张（还可补扫 {s_alienCleanupTurnsLeft} 回合）");
            }
            catch (Exception e)
            {
                Entry.Logger.Warn($"[Omnitrix] 补扫残留英雄牌失败（已忽略，避免打断回合管线）：{e}");
            }
        }
        await base.AfterPlayerTurnStart(choiceContext, player);
    }

    // 读档/新开跑时复位（见 RunLifecycle）：允许玩家 SL 后再次变身
    internal static void ResetCombatScopedState()
    {
        s_cooldownUntilTurn = 0;
        s_endSequenceStarted = false;
        s_transformSnapshot = null;
        s_hiddenBody = null;
        s_overlaySprite = null;
        s_alienCleanupTurnsLeft = 0;
    }

    // 右键：打开变身选择界面
    public async Task OnRightClick(ModRightClickExecutionContext context)
    {
        if (Owner == null) return;

        // 表盘启动音（界面弹出前）
        ModAudio.PlayOneShot(StartSfxPath);

        // 极低概率：变身被鬼影替换（跳过选择，直接展示鬼影）
        // 用玩家共享的 Transformations 随机序列，联机各客户端结果一致
        bool ghostfreak = Owner.PlayerRng.Transformations.NextDouble() < GhostFreakChance;

        var screen = ghostfreak
            ? new OmnitrixSelectScreen(AlienHero.Ghostfreak)
            : new OmnitrixSelectScreen();
        var selection = await screen.ShowAsync();
        if (selection.HasValue)
        {
            await Transform(selection.Value);
        }
    }

    // 执行变身：绿色闪光过渡 → 在最亮的一刻换形象、换牌组、挂被动、上 3 回合变身状态
    private async Task Transform(HeroSelection selection)
    {
        var hero = selection.Hero;

        // 流派不由玩家选：火焰人变身时随机抽一套（用玩家共享的 Transformations 随机序列，联机各端一致）
        if (hero == AlienHero.Heatblast && Owner != null)
        {
            var spec = HeatblastSpecs[Owner.PlayerRng.Transformations.NextInt(HeatblastSpecs.Length)];
            selection = new HeroSelection(hero, spec);
            Entry.Logger.Info($"[Omnitrix] 本次变身随机流派：{spec}");
        }

        s_endSequenceStarted = false;

        // 先把屏幕闪绿，再在最亮的时候换形象（变身音也在这一刻，声画同步）
        PlayGreenFlash(TransformFlashIn, TransformFlashHold, TransformFlashOut);
        await Cmd.CustomScaledWait(TransformFlashIn, TransformFlashIn);

        var player = Owner;
        var creature = player?.Creature;

        // 变身前快照（原牌组 + 自身能力）：变回时按它还原
        s_transformSnapshot = player != null ? await TransformSnapshot.Capture(player) : null;

        ReplaceAppearance(creature, hero);
        ModAudio.PlayOneShot(TransformSfxPath);

        if (player != null && creature != null)
        {
            var choiceContext = new ThrowingPlayerChoiceContext();
            // 换上英雄专属牌组（按流派选专精牌 + 起手保底 + 先古置入手牌）与专属被动
            await AlienDecks.BuildDeck(choiceContext, player, selection);
            await AlienDecks.ApplyPassive(choiceContext, player, selection);
            // 变身状态（面板上显示为剩余回合数），3 回合后由能力自己解除变身
            await PowerCmd.Apply<OmnitrixTransformPower>(
                choiceContext, creature, OmnitrixTransformPower.DurationTurns, creature, null, silent: true);
        }
    }

    // 换掉角色形象
    // 玩家角色用的是 Spine 动画（SpineSprite），没有 Sprite2D 可直接换贴图，
    // 因此做法是：隐藏原 Spine 身体，在同一位置叠加一个显示英雄形象的节点。
    // 有逐帧序列的英雄（目前只有火焰人）用 AnimatedSprite2D 循环播放待机动画，其余用静态立绘 Sprite2D。
    // 注意：Spine 的 Visuals 自带很小的工作缩放（如铁甲战士 0.28），绝不能拿来乘立绘尺寸，
    // 立绘要按角色实际显示高度（Visuals.Bounds.Height）独立计算缩放。
    private static void ReplaceAppearance(Creature? creature, AlienHero hero)
    {
        var creatureNode = NCombatRoom.Instance?.GetCreatureNode(creature);
        var body = creatureNode?.Visuals?.GetCurrentBody();
        if (body == null)
        {
            return;
        }

        var idleFrames = GetIdleFrames(hero);

        Node2D overlay;
        float textureHeight;
        if (idleFrames != null)
        {
            textureHeight = idleFrames.GetFrameTexture(IdleAnimationName, 0).GetHeight();
            overlay = new AnimatedSprite2D
            {
                SpriteFrames = idleFrames,
                Animation = IdleAnimationName,
            };
        }
        else
        {
            var heroTexture = GD.Load<Texture2D>(GetHeroTexturePath(hero));
            if (heroTexture == null)
            {
                return;
            }
            textureHeight = heroTexture.GetHeight();
            overlay = new Sprite2D { Texture = heroTexture };
        }

        // 目标显示高度取自角色自身的 Bounds（角色坐标原点在脚底，Bounds 底边即原点）
        float targetHeight = creatureNode!.Visuals.Bounds?.Size.Y ?? 0f;
        float scale = targetHeight > 1f
            ? targetHeight / textureHeight * HeroScaleMultiplier
            : DefaultHeroScale;

        // Offset 用贴图像素表示：上移半张图高，让立绘底边正好落在脚底
        var offset = new Vector2(0f, -textureHeight / 2f);
        if (overlay is Sprite2D sprite)
        {
            sprite.Offset = offset;
        }
        else if (overlay is AnimatedSprite2D animated)
        {
            animated.Offset = offset;
        }

        overlay.Position = Vector2.Zero; // 父节点原点 = 角色脚底
        overlay.Scale = Vector2.One * scale;
        overlay.ZIndex = 0; // 与植物一致：不要盖住血条
        body.GetParent().AddChild(overlay);

        if (overlay is AnimatedSprite2D animatedSprite)
        {
            animatedSprite.Play();
        }
        s_overlaySprite = overlay;

        // 隐藏原 Spine 身体
        body.Visible = false;
        s_hiddenBody = body;

        Entry.Logger.Info($"[Omnitrix] 变身为 {hero}，立绘高度目标 {targetHeight:0}px，缩放 {scale:0.###}");
    }

    // === 英雄逐帧待机动画 ===

    private const string IdleAnimationName = "idle";

    // 火焰人待机动画帧目录（frame_0001.png 起连续编号）
    private const string HeatblastIdleDir = "res://OtherworldTreasures/images/heroes/heatblast_idle";

    // 待机动画帧率：觉得快/慢调这里
    private const float IdleFps = 24f;

    // 找帧时的安全上限（序列不足时会在第一个缺号处停下）
    private const int MaxIdleFrames = 240;

    // 已构建的逐帧序列（含 null＝该英雄没有序列），构建一次后跨变身复用
    private static readonly Dictionary<AlienHero, SpriteFrames?> s_idleFrames = new();

    // 取英雄的逐帧待机动画；没有序列的英雄返回 null（走静态立绘）
    private static SpriteFrames? GetIdleFrames(AlienHero hero)
    {
        if (s_idleFrames.TryGetValue(hero, out var cached))
        {
            return cached;
        }

        var frames = BuildIdleFrames(hero);
        s_idleFrames[hero] = frames;
        return frames;
    }

    private static SpriteFrames? BuildIdleFrames(AlienHero hero)
    {
        var dir = hero switch
        {
            AlienHero.Heatblast => HeatblastIdleDir,
            _ => string.Empty,
        };
        if (dir.Length == 0)
        {
            return null;
        }

        var frames = new SpriteFrames();
        frames.RemoveAnimation("default");
        frames.AddAnimation(IdleAnimationName);
        frames.SetAnimationSpeed(IdleAnimationName, IdleFps);
        frames.SetAnimationLoop(IdleAnimationName, true);

        for (int i = 1; i <= MaxIdleFrames; i++)
        {
            var path = $"{dir}/frame_{i:0000}.png";
            if (!ResourceLoader.Exists(path))
            {
                break;
            }
            frames.AddFrame(IdleAnimationName, GD.Load<Texture2D>(path));
        }

        int frameCount = frames.GetFrameCount(IdleAnimationName);
        if (frameCount == 0)
        {
            return null;
        }

        Entry.Logger.Info($"[Omnitrix] {hero} 待机动画载入 {frameCount} 帧 @ {IdleFps:0}fps");
        return frames;
    }

    // 立绘缩放的微调倍率（1.0 = 立绘高度与角色 Bounds 高度一致），偏大/偏小改这里
    private const float HeroScaleMultiplier = 1.15f;

    // 读不到角色 Bounds 时的兜底缩放（512px 立绘约 278px 高）
    private const float DefaultHeroScale = 0.54f;

    // 战斗结束时恢复原形（变身已到期时是空操作）
    public override async Task AfterCombatEnd(CombatRoom room)
    {
        RestoreAppearanceSilently();
        await base.AfterCombatEnd(room);
    }

    // === 绿色闪光过渡 ===

    // 闪光色：原版 NAdditiveOverlayVfx 用的那支绿（00ff15），alpha 由 tween 控制
    private static readonly Color FlashColor = new(0f, 1f, 0.08f, 0f);

    // 最亮时的透明度，觉得太亮/太暗改这里
    private const float FlashPeakAlpha = 0.3f;

    // 变身时的闪光节奏（淡入 / 保持 / 淡出，秒）
    private const float TransformFlashIn = 0.12f;
    private const float TransformFlashHold = 0.10f;
    private const float TransformFlashOut = 0.30f;

    // 变回原形时的闪光节奏（淡入 / 保持 / 淡出，秒）
    private const float EndFlashIn = 0.18f;
    private const float EndFlashHold = 0.18f;
    private const float EndFlashOut = 0.45f;
    private const float EndFlashTotal = EndFlashIn + EndFlashHold + EndFlashOut;

    // 结束音长度（omnitrix_end.wav ≈ 3.02s）。换音效时同步改这里。
    private const float EndSfxLength = 3.02f;

    // 结束演出是否已经开始（提前放音后，后面只等收尾，不重复触发）
    private static bool s_endSequenceStarted;

    // 变身结束的声画对齐：
    // 结束音（3.02s）比闪光（0.81s）长得多，硬要"同起同落"是做不到的，
    // 所以换个方向——声音尽量早放（最后一回合的敌方回合结束就放），
    // 闪光 + 变回原形则往后压 (音频长度 - 闪光长度 = 2.21s)，
    // 这样闪光收完的瞬间，结束音也刚好放完。
    internal static async Task StartTransformationEnd(Player player)
    {
        if (s_endSequenceStarted)
        {
            return;
        }
        s_endSequenceStarted = true;

        // 牌组与自身状态在这一刻就还原：此刻玩家回合已经结束、手牌是空的，
        // 所以只动不渲染的牌堆，手牌不会留下卡牌节点"尸体"
        if (s_transformSnapshot != null)
        {
            var snapshot = s_transformSnapshot;
            s_transformSnapshot = null;
            await snapshot.Restore(player);
        }
        // 还原那一次必然漏掉带【保留】的英雄牌，往后两回合再补扫
        s_alienCleanupTurnsLeft = 2;

        ModAudio.PlayOneShot(EndSfxPath);

        float delay = Math.Max(0f, EndSfxLength - EndFlashTotal);
        var tree = NCombatRoom.Instance?.GetTree();
        if (tree == null)
        {
            PlayEndFlashAndRevert();
            return;
        }
        // 用计时器而不是 await：绝不能把回合管线卡住 2 秒
        tree.CreateTimer(delay).Timeout += () =>
        {
            // 演出期间若已开新战斗/新变身（标记被清掉），这次延时就不该再动形象
            if (GodotObject.IsInstanceValid(tree) && s_endSequenceStarted)
            {
                PlayEndFlashAndRevert();
            }
        };
    }

    // 闪光 + 闪到最亮时变回原形
    private static void PlayEndFlashAndRevert()
    {
        PlayGreenFlash(EndFlashIn, EndFlashHold, EndFlashOut);

        var tree = NCombatRoom.Instance?.GetTree();
        if (tree == null)
        {
            FinishEndSequence();
            return;
        }
        tree.CreateTimer(EndFlashIn).Timeout += FinishEndSequence;
    }

    // 结束演出的最后一拍：变回原形，并解除"演出中"标记
    private static void FinishEndSequence()
    {
        RestoreOriginalAppearance();
        s_endSequenceStarted = false;
    }

    // 变身 3 回合到期（玩家回合开始那一刻）：只收状态（移除能力由调用方负责）。
    // 牌组还原与结束演出在正常流程里已经由 StartTransformationEnd 提前做完了。
    internal static async Task OnTransformationExpired(Player player)
    {
        // 兜底：没走到提前收尾（比如读档正好卡在这个回合）就地补一次（牌组 / 能力 / 形象一起收）
        await StartTransformationEnd(player);

        int turn = player.PlayerCombatState?.TurnNumber ?? 0;
        s_cooldownUntilTurn = turn + CooldownTurns;
        Entry.Logger.Info($"[Omnitrix] 第 {turn} 回合开始：变身到期，冷却至第 {s_cooldownUntilTurn} 回合");
    }

    // 兜底：变身能力被其它方式移除（战斗结束清空、被驱散等）时恢复形象。
    // 但结束演出正在跑的时候不动 —— 那时候形象要留给演出收尾（声音尾 + 闪光一起收）。
    internal static void RestoreAppearanceSilently()
    {
        if (s_endSequenceStarted)
        {
            return;
        }
        RestoreOriginalAppearance();
    }

    // 全屏绿色闪光过渡。做法与原版 NAdditiveOverlayVfx 一致：全屏 ColorRect + 加法混合材质，
    // 挂在与原版同一容器（CombatVfxContainer）里，层次表现一致。
    private static void PlayGreenFlash(float fadeIn, float hold, float fadeOut)
    {
        try
        {
            var container = NCombatRoom.Instance?.CombatVfxContainer;
            if (container == null)
            {
                return;
            }

            var rect = new ColorRect
            {
                Color = Colors.White,
                Modulate = FlashColor,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Material = GD.Load<Material>("res://themes/canvas_item_material_additive_shared.tres"),
            };
            rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);

            // 延迟挂载：变身到期是在回合开始的钩子里触发的，直接改节点树结构会报"父节点正在设置子节点"
            Callable.From(() =>
            {
                if (!GodotObject.IsInstanceValid(container) || !GodotObject.IsInstanceValid(rect))
                {
                    return;
                }
                container.AddChild(rect);

                var tween = rect.CreateTween();
                tween.TweenProperty(rect, "modulate:a", FlashPeakAlpha, fadeIn).SetEase(Tween.EaseType.Out);
                if (hold > 0f)
                {
                    tween.TweenInterval(hold);
                }
                // 长尾巴用 Ease Out：先快速退掉大部分绿光（不挡操作），再留一层很淡的余光慢慢散尽
                tween.TweenProperty(rect, "modulate:a", 0f, fadeOut).SetEase(Tween.EaseType.Out);
                tween.Finished += () =>
                {
                    if (GodotObject.IsInstanceValid(rect))
                    {
                        rect.QueueFree();
                    }
                };
            }).CallDeferred();
        }
        catch (Exception e)
        {
            // 表现层失败绝不能影响变身/回合流程
            Entry.Logger.Warn($"[Omnitrix] 闪光过渡播放失败：{e.Message}");
        }
    }

    // 恢复玩家原始形象；本来就没在变身状态时是空操作
    private static void RestoreOriginalAppearance()
    {
        bool wasTransformed = s_hiddenBody != null || s_overlaySprite != null;

        if (s_hiddenBody != null && GodotObject.IsInstanceValid(s_hiddenBody))
        {
            s_hiddenBody.Visible = true;
        }
        if (s_overlaySprite != null && GodotObject.IsInstanceValid(s_overlaySprite))
        {
            s_overlaySprite.QueueFree();
        }
        s_hiddenBody = null;
        s_overlaySprite = null;

        if (wasTransformed)
        {
            Entry.Logger.Info("[Omnitrix] 变身解除，恢复原始形象");
        }
    }

    // 获取英雄的身体贴图路径
    internal static string GetHeroTexturePath(AlienHero hero) => hero switch
    {
        AlienHero.Heatblast => "res://OtherworldTreasures/images/heroes/Heatblast.png",
        AlienHero.FourArms => "res://OtherworldTreasures/images/heroes/FourArms.png",
        AlienHero.Wildmutt => "res://OtherworldTreasures/images/heroes/Wildmutt.png",
        AlienHero.Ghostfreak => "res://OtherworldTreasures/images/heroes/Ghostfreak.png",
        _ => "res://OtherworldTreasures/images/heroes/Heatblast.png"
    };
}

/// <summary>
/// 外星英雄枚举
/// </summary>
public enum AlienHero
{
    Heatblast,    // 火焰人
    FourArms,     // 四手霸王
    Wildmutt,     // 超能兽
    Ghostfreak    // 鬼影
}

/// <summary>
/// 火焰人专精流派。
/// 选火焰人时同时选定一个流派，决定变身时携带哪 7 张专精牌。
/// 未指定时默认【烈焰爆破】；鬼影模式固定【烈焰爆破】。
/// </summary>
public enum AlienSpecialization
{
    Blast,        // 烈焰爆破：灼伤引爆爆发
    Shaping,      // 熔岩塑形：燃尽状态牌换格挡/回血
    Propulsion,   // 喷射推进：额外能量 + 0 费牌输出
}

/// <summary>
/// 变身选择结果：英雄 + 流派。
/// </summary>
public readonly struct HeroSelection
{
    public AlienHero Hero { get; }
    public AlienSpecialization Specialization { get; }

    public HeroSelection(AlienHero hero, AlienSpecialization specialization)
    {
        Hero = hero;
        Specialization = specialization;
    }
}
