using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Rewards;
using OtherworldTreasures.Scripts.Cards.Alien;
using OtherworldTreasures.Scripts.Rewards;
using STS2RitsuLib;
using STS2RitsuLib.CardPiles;
using STS2RitsuLib.Combat.Rewards;
using STS2RitsuLib.Content;
using STS2RitsuLib.Interop;
using HarmonyLib;
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

    public static void Init()
    {
        var assembly = Assembly.GetExecutingAssembly();
        RitsuLibFramework.EnsureGodotScriptsRegistered(assembly, Logger);
        ModTypeDiscoveryHub.RegisterModAssembly(ModId, assembly);

        // 注册自定义奖励：生命水晶。无动态状态，读档时工厂直接重建新实例
        LifeCrystalRewardType = ModRewardRegistry.For(ModId)
            .RegisterOwned(
                "life_crystal",
                (save, player, json) => new LifeCrystalReward(player))
            .RewardType;

        // Harmony patches
        var harmony = new Harmony("sts2.otherworldtreasures.patches");
        harmony.PatchAll();

        // 跑局生命周期：读档/新开跑时复位各遗物的"本场战斗"级静态状态，
        // 否则战斗中存档再读档会让「小破表」这类一次性遗物永远不可用（允许玩家 SL）
        RunLifecycle.Initialize();

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
        // 图标直接用战士的角色图标（没专门做卡池图标，复用原版资源）。
        ModContentRegistry.For(ModId)
            .RegisterCardLibraryCompendiumSharedPoolFilter<HeatblastCardPool>(
                "heatblast_pool",
                "res://images/ui/top_panel/character_icon_ironclad.png");

        // 附魔金苹果作为先古之民的选项之一，在 MemoryKeeper.GenerateInitialOptions 里与遗物一起抽取

        // 结束回合按钮自愈：玩家回合开始时延迟一帧兜底检查按钮是否被落在屏幕外
        // （【如果电话亭】重开战斗后按钮会间歇性消失，延迟一帧可避开正常的入场动画）
        CombatManager.Instance.TurnStarted += _ =>
            Callable.From(EndTurnButtonFixer.EnsureVisible).CallDeferred();

        Logger.Info("OtherworldTreasures mod initialized! ExclusionPile registered, Harmony patches applied.");
    }
}
