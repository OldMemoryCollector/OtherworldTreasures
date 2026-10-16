using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
using OtherworldTreasures.Scripts.Cards;
using OtherworldTreasures.Scripts.Cards.Alien;
using OtherworldTreasures.Scripts.Relics;
using OtherworldTreasures.Scripts.Rewards;
// TimeCloth 既是遗物类名又是同级 TimeClothState 所在命名空间名，直接用简单名会解析成命名空间
using TimeClothRelic = OtherworldTreasures.Scripts.Relics.TimeCloth;
using STS2RitsuLib;
using STS2RitsuLib.CardPiles;
using STS2RitsuLib.Combat.Rewards;
using STS2RitsuLib.Content;
using STS2RitsuLib.Interop;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;
using OtherworldTreasures.Scripts.UI;
using STS2RitsuLib.Scaffolding.Godot.NodeAttachments;
using STS2RitsuLib.Interactions.RightClick;
using Logger = MegaCrit.Sts2.Core.Logging.Logger;

namespace OtherworldTreasures.Scripts;

[ModInitializer(nameof(Init))]
public class Entry
{
    public const string ModId = "OtherworldTreasures";
    public static readonly Logger Logger = RitsuLibFramework.CreateLogger(ModId);

    // 自定义牌堆：除外池（类似消耗堆，但卡牌无法通过常规手段取回）
    public static PileType ExclusionPile;

    // 自定义牌堆：小破表变身的暂存堆（Headless 永不显示，只用来放变身前暂存的原始牌堆）
    public static PileType OmnitrixStashPile;

    // 自定义奖励：生命水晶（战后奖励小概率出现，领取后生命上限 +10）
    public static RewardType LifeCrystalRewardType;
    public static RewardType EnchantedAppleRewardType;

    public static void Init()
    {
        var assembly = Assembly.GetExecutingAssembly();
        RitsuLibFramework.EnsureGodotScriptsRegistered(assembly, Logger);
        ModTypeDiscoveryHub.RegisterModAssembly(ModId, assembly);
        Events.TrainingProgress.Initialize();
        AppleChestDrops.Initialize();
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Events.GoldenRelics).TypeHandle);
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Events.SilverRelics).TypeHandle);
        ModRightClickRegistry.Register(ScouterDisplayRightClick.Instance);
        ModNodeAttachmentRegistry.For(ModId).RegisterReadyChild<NCreature, ScouterReadout>(
            "scouter_readout", static _ => new ScouterReadout());
        ModNodeAttachmentRegistry.For(ModId).RegisterReadyChild<NHealthBar, TemporaryHealthReadout>(
            "temporary_health_readout", static _ => new TemporaryHealthReadout());
        ModNodeAttachmentRegistry.For(ModId).RegisterReadyChild<MegaCrit.Sts2.Core.Nodes.Relics.NRelic, GoldenRelicBorder>(
            "golden_relic_border", static _ => new GoldenRelicBorder());
        ModNodeAttachmentRegistry.For(ModId).RegisterReadyChild<MegaCrit.Sts2.Core.Nodes.Relics.NRelic, TrueExcaliburGlow>(
            "true_excalibur_glow", static _ => new TrueExcaliburGlow());
        ModNodeAttachmentRegistry.For(ModId).RegisterReadyChild<NIntent, GhostIntentSeal>(
            "ghostfreak_intent_seal", static _ => new GhostIntentSeal(), new NodeAttachmentOptions
            {
                Name = "OtherworldGhostIntentSeal",
                DuplicatePolicy = NodeAttachmentDuplicatePolicy.ReuseExistingByName,
            });

        // 注册自定义奖励：生命水晶。无动态状态，读档时工厂直接重建新实例
        LifeCrystalRewardType = ModRewardRegistry.For(ModId)
            .RegisterOwned(
                "life_crystal",
                (save, player, json) => new LifeCrystalReward(player))
            .RewardType;

        EnchantedAppleRewardType = ModRewardRegistry.For(ModId)
            .RegisterOwned("enchanted_apple_chest", (save, player, json) =>
                new EnchantedAppleChestReward(player, System.Text.Json.JsonSerializer.Deserialize<string>(json!)!))
            .RewardType;

        // Harmony patches
        var harmony = new Harmony("sts2.otherworldtreasures.patches");
        harmony.PatchAll();

        // 跑局生命周期：读档/新开跑时复位各遗物的"本场战斗"级静态状态，
        // 否则战斗中存档再读档会让「小破表」这类一次性遗物永远不可用（允许玩家 SL）
        RunLifecycle.Initialize();

        // 桃太郎丸子的"捕获"要跨端一致：注册成联机动作，右键时由发起者把目标入队，
        // 所有端按动作队列执行同一份捕获逻辑（见 MomotaroDumplings）
        MomotaroDumplings.RegisterCaptureAction();

        // 右键遗物的跨端效果：小破表变身 / 时光布发放 / 时光布抉择牌 / 戴夫的种子
        // —— 都是"发起者本机入队，动作队列在所有端统一执行"
        Omnitrix.RegisterTransformAction();
        TimeClothRelic.RegisterGrantAction();
        TimeClothCard.RegisterTargetAction();
        DavesSeeds.RegisterSeedsAction();

        // 其余右键遗物（原素瓶 / 缩小灯 / 库拉的骰子 / 任意门 / 四魂碎片）统一走这条通道
        RelicRightClickSync.Register();

        // 第三方模组异常防护：给「皮皮倒带: Rewind」的 RewindButton.OnTurnStarted 挂 Finalizer 吞异常。
        // 它在战斗 UI 被重建后会持有已释放节点并抛 ObjectDisposedException，而该异常抛在回合循环内部，
        // 会把回合循环打死（原版注释：the combat is stuck until the room is restarted），表现为卡死。
        // 找不到该模组就跳过；挂载失败也不影响模组自身加载。
        try
        {
            var rewindButtonType = AccessTools.TypeByName("Rewind.Scripts.RewindButton");
            var rewindOnTurnStarted = rewindButtonType == null
                ? null
                : AccessTools.Method(rewindButtonType, "OnTurnStarted");
            if (rewindOnTurnStarted != null)
            {
                harmony.Patch(rewindOnTurnStarted, finalizer: new HarmonyMethod(
                    typeof(ThirdPartyGuards), nameof(ThirdPartyGuards.SwallowTurnLoopKiller)));
                Logger.Info("[Guard] 已为 Rewind.RewindButton.OnTurnStarted 挂上异常防护");
            }
            else
            {
                Logger.Info("[Guard] 未找到 Rewind 模组，跳过其异常防护");
            }
        }
        catch (Exception e)
        {
            Logger.Info($"[Guard] 挂载 Rewind 异常防护失败（已忽略）：{e}");
        }

        // 注册除外自定义牌堆
        var registry = ModCardPileRegistry.For(ModId);
        ExclusionPile = registry.RegisterOwned("exclusion_pile", new ModCardPileSpec
        {
            Scope = ModCardPileScope.CombatOnly,
            Style = ModCardPileUiStyle.BottomRight,
            // 放在消耗堆按钮正上方（Custom 模式，父控件局部坐标中心对齐）
            // 消耗堆按钮中心约 (1820, 975)，正上方约减 95px → (1820, 880)
            Anchor = ModCardPileAnchor.AtCenter(new Vector2(1820, 880)),
            IconPath = "res://OtherworldTreasures/images/piles/exclusion_pile.svg",
            OnOpen = ctx => ctx.ShowDefaultPileScreen(),
            // 只有有牌进入除外堆时才显示按钮（与消耗堆一致，空堆隐藏）
            VisibleWhen = ctx => ctx.Pile != null && ctx.Pile.Cards.Count > 0,
        }).PileType;

        // 小破表变身用的"暂存堆"：变身前把玩家原始牌堆整体挪进来，变身结束再挪回抽牌堆。
        // Headless = 永不显示（没有按钮、不占屏幕），VisibleWhen 再兜一层。
        OmnitrixStashPile = registry.RegisterOwned("omnitrix_stash", new ModCardPileSpec
        {
            Scope = ModCardPileScope.CombatOnly,
            Style = ModCardPileUiStyle.Headless,
            Anchor = ModCardPileAnchor.Default,
            IconPath = "res://OtherworldTreasures/images/piles/exclusion_pile.svg",
            OnOpen = ctx => ctx.ShowDefaultPileScreen(),
            VisibleWhen = ctx => false,
        }).PileType;

        // 火焰人卡池的图鉴筛选器：让火焰人的专属牌在卡牌图鉴里单列一类。
        // 使用四位外星英雄各自的图鉴头像。
        ModContentRegistry.For(ModId)
            .RegisterCardLibraryCompendiumSharedPoolFilter<HeatblastCardPool>(
                "heatblast_pool",
                "res://OtherworldTreasures/images/heroes/compendium/Heatblast.png");
        ModContentRegistry.For(ModId)
            .RegisterCardLibraryCompendiumSharedPoolFilter<FourArmsCardPool>(
                "four_arms_pool",
                "res://OtherworldTreasures/images/heroes/compendium/FourArms.png");
        ModContentRegistry.For(ModId).RegisterCardLibraryCompendiumSharedPoolFilter<WildmuttCardPool>("wildmutt_pool", "res://OtherworldTreasures/images/heroes/compendium/Wildmutt.png");
        ModContentRegistry.For(ModId).RegisterCardLibraryCompendiumSharedPoolFilter<GhostfreakCardPool>("ghostfreak_pool", "res://OtherworldTreasures/images/heroes/compendium/Ghostfreak.png");

        // 附魔金苹果作为先古之民的选项之一，在 MemoryKeeper.GenerateInitialOptions 里与遗物一起抽取

        // 结束回合按钮自愈：玩家回合开始时延迟一帧兜底检查按钮是否被落在屏幕外
        // （【如果电话亭】重开战斗后按钮会间歇性消失，延迟一帧可避开正常的入场动画）
        CombatManager.Instance.TurnStarted += _ =>
            Callable.From(EndTurnButtonFixer.EnsureVisible).CallDeferred();

        Logger.Info("OtherworldTreasures mod initialized! ExclusionPile registered, Harmony patches applied.");
    }

    // 判断某个玩家是不是"本机玩家"。单机 / 回放一律视为本机。
    // 用于那些"会被同步到所有端执行、但只在发起者这边需要本地交互"的钩子（右键 / 出牌）。
    public static bool IsLocalPlayer(Player player)
    {
        try
        {
            var net = RunManager.Instance?.NetService;
            if (net == null || net.Type is NetGameType.None or NetGameType.Singleplayer or NetGameType.Replay)
            {
                return true;
            }
            return net.NetId == player.NetId;
        }
        catch
        {
            return true;
        }
    }
}
