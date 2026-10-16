using System;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib;
using STS2RitsuLib.Interactions.RightClick;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Networking.ManagedActions;
using STS2RitsuLib.Scaffolding.Content;
using OtherworldTreasures.Scripts.Cards;
using OtherworldTreasures.Scripts.Monsters;

namespace OtherworldTreasures.Scripts.Relics;

// 戴夫的种子：《植物大战僵尸》乱入遗物。
// 右键有两段用法，每场战斗各一次：
//   第一段：弹出卡牌三选一（三种植物召唤卡均为 2 费），选中后扣掉该卡费用并召唤：
//     向日葵（15 血）：每回合提供 1 点能量（20% 概率触发两次），召唤当回合立即触发一次
//     坚果墙（25 血）：每回合获得 6 点格挡（20% 概率翻倍）
//     豌豆射手（20 血）：每回合对第一位敌人造成 7 点伤害（20% 概率两次），召唤当回合立即攻击一次
//   第二段（召唤之后）：再弹出三选一，给一张【施肥】卡（1 费，给植物 +5 生命）；
//     三张卡效果一致，另外两张分别带【除外】和【湮灭】，只能拿一张
// 植物会替玩家承受"被强化的攻击"伤害；不可替换、植物死亡后也不能再召唤。
[RegisterRelic(typeof(SharedRelicPool))]
public class DavesSeeds : ModRelicTemplate, IModRightClickableRelic
{
    // 按玩家隔离的状态（遗物实例每次战斗会被克隆，static 又会被两名玩家共享）。
    // 刻意不做 [SavedProperty] 持久化：允许玩家战斗中存档→重进（SL）后重新召唤。
    private sealed class PlayerState
    {
        // 本场战斗是否已经召唤过
        public bool SummonedThisCombat;
        // 本场战斗是否已经领过【施肥】卡
        public bool GotGrowthCardThisCombat;
    }

    private PlayerState S => PerPlayerState<PlayerState>.Of(Owner);

    private bool _summonedThisCombat;

    private bool _gotGrowthCardThisCombat;

    public bool SummonedThisCombat
    {
        get => _summonedThisCombat;
        set
        {
            AssertMutable();
            _summonedThisCombat = value;
        }
    }

    public bool GotGrowthCardThisCombat
    {
        get => _gotGrowthCardThisCombat;
        set
        {
            AssertMutable();
            _gotGrowthCardThisCombat = value;
        }
    }

    // 玩家身上真正的那件遗物（右键派发的可能是克隆实例，直接读写 this 不可靠）
    private DavesSeeds? LiveRelic => Owner?.Relics.OfType<DavesSeeds>().FirstOrDefault();

    // 本场是否已经召唤过
    private bool AlreadySummoned => S.SummonedThisCombat || (LiveRelic?.SummonedThisCombat ?? false);

    // 本场是否已经领过【施肥】卡
    private bool AlreadyGotGrowthCard => S.GotGrowthCardThisCombat || (LiveRelic?.GotGrowthCardThisCombat ?? false);

    // 读档/新开跑时复位（见 RunLifecycle）：允许玩家 SL 后重新召唤/领卡
    internal static void ResetCombatScopedState()
    {
        // 按玩家隔离：整体清空（下次取用时重建为默认值）
        PerPlayerState<PlayerState>.ClearAll();
    }

    // 由先古之民给予，使用 Ancient 稀有度
    public override RelicRarity Rarity => RelicRarity.Ancient;

    // 遗物图片资源（图标 256x256）
    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://OtherworldTreasures/images/relics/Daves_Seeds.png",
        IconOutlinePath: "res://OtherworldTreasures/images/relics/Daves_Seeds.png",
        BigIconPath: "res://OtherworldTreasures/images/relics/Daves_Seeds.png"
    );

    // 悬浮提示：三种植物各自一个文本栏，分别介绍数值与效果。
    // 文案里的数字用 {VarName} 占位，数值直接读代码常量 → 以后改数值不用再同步文本。
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => base.AdditionalHoverTips
        .Append(PlantTip(
            "sunflower",
            new DynamicVar("Cost", SunflowerCard.CardEnergyCost),
            new DynamicVar("Hp", SunflowerPlant.MaxHpValue),
            new EnergyVar("Energy", SunflowerPlant.EnergyPerTurn)))
        .Append(PlantTip(
            "pea_shooter",
            new DynamicVar("Cost", PeaShooterCard.CardEnergyCost),
            new DynamicVar("Hp", PeaShooterPlant.MaxHpValue),
            new DynamicVar("Damage", PeaShooterPlant.DamagePerTurn)))
        .Append(PlantTip(
            "wall_nut",
            new DynamicVar("Cost", WallNutCard.CardEnergyCost),
            new DynamicVar("Hp", WallNutPlant.MaxHpValue),
            new DynamicVar("Block", WallNutPlant.BlockPerTurn)))
        .Append(PlantTip(
            "fertilizer",
            new DynamicVar("Cost", PlantGrowthCard.CardEnergyCost),
            new DynamicVar("Hp", PlantSummonBase.GrowthAmount)));

    private static HoverTip PlantTip(string plantKey, params DynamicVar[] vars)
    {
        var title = new LocString("relics", $"OTHERWORLD_TREASURES_RELIC_DAVES_SEEDS.{plantKey}.title");
        var description = new LocString("relics", $"OTHERWORLD_TREASURES_RELIC_DAVES_SEEDS.{plantKey}.description");
        foreach (var dynVar in vars)
        {
            description.Add(dynVar);
        }
        return new HoverTip(title, description);
    }

    // 获得遗物：重置本场的使用记录
    public override async Task AfterObtained()
    {
        ResetUsage();
        var identity = RitsuLibFramework.EnsureModelIdentity(this);
        Entry.Logger.Info($"[DavesSeeds] AfterObtained, EnsureModelIdentity => {identity.Value}");
        await base.AfterObtained();
    }

    // 进入战斗房间：重置本场的使用记录（图标恢复可点）
    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is CombatRoom)
        {
            ResetUsage();
        }
        await base.AfterRoomEntered(room);
    }

    // 战斗结束：重置，下一场两段用法都能再用一次
    public override async Task AfterCombatEnd(CombatRoom room)
    {
        ResetUsage();
        await base.AfterCombatEnd(room);
    }

    // 玩家回合开始：按当前进度修正图标（例如战斗中读档重进）
    // 召唤过但还没领卡时仍要可点（第二段用法）；两段都用完了才变灰
    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner)
        {
            Status = AlreadyGotGrowthCard ? RelicStatus.Disabled : RelicStatus.Normal;
        }
        return Task.CompletedTask;
    }

    // 清空本场的使用记录与图标状态
    private void ResetUsage()
    {
        S.SummonedThisCombat = false;
        SummonedThisCombat = false;
        S.GotGrowthCardThisCombat = false;
        GotGrowthCardThisCombat = false;
        Status = RelicStatus.Normal;
    }

    // 标记"本场已召唤"（按玩家状态与存档字段都要写）
    // 注意：召唤完图标保持可点，因为还有第二段用法（领【施肥】卡）
    private void MarkSummoned()
    {
        S.SummonedThisCombat = true;
        var relic = LiveRelic;
        if (relic != null)
        {
            relic.SummonedThisCombat = true;
        }
        Status = RelicStatus.Normal;
    }

    // 标记"本场已领过【施肥】卡"（两段都用完，图标变灰）
    private void MarkGotGrowthCard()
    {
        S.GotGrowthCardThisCombat = true;
        var relic = LiveRelic;
        if (relic != null)
        {
            relic.GotGrowthCardThisCombat = true;
        }
        Status = RelicStatus.Disabled;
    }

    // 右键预检：注册模型身份令牌（右键同步派发依赖它）
    public bool CanHandleRightClickLocal(ModRightClickContext context)
    {
        RitsuLibFramework.EnsureModelIdentity(this);
        return true;
    }

    // 战斗内可点：第一段需要付得起最便宜的植物（向日葵 2 费）；第二段已经召唤过，领卡本身不花钱
    public bool CanExecuteRightClick(ModRightClickExecutionContext context)
    {
        var player = Owner;
        if (player == null || player.PlayerCombatState == null)
        {
            return false;
        }
        if (CombatManager.Instance.DebugOnlyGetState() == null)
        {
            return false;
        }
        if (!AlreadySummoned)
        {
            return player.PlayerCombatState.Energy >= SunflowerCard.CardEnergyCost;
        }
        return !AlreadyGotGrowthCard;
    }

    // 右键：先按"是否召唤过"分流到两段用法。
    // 关键：只有发起者本机负责入队；**选择和结算都放进联机动作里**，由动作队列在所有端统一执行。
    // 这样"玩家选择"的 id 记账在所有端都是对称的（原版卡牌也是这个模型：OnPlay 在所有端跑、各端都调选择接口）。
    // 不这么做的话：要么远端不执行（只有一端生效），要么只有一端记账（choice id 分叉）。
    public Task OnRightClick(ModRightClickExecutionContext context)
    {
        if (Owner == null || !Entry.IsLocalPlayer(Owner) || s_seedsDescriptor == null)
        {
            return Task.CompletedTask;
        }

        var phase = (byte)(AlreadySummoned ? 1 : 0);
        var queued = RitsuLibManagedNetActions.Request(RunManager.Instance, s_seedsDescriptor, phase);
        Entry.Logger.Info($"[DavesSeeds] 动作已入队={queued} 阶段={(phase == 0 ? "召唤植物" : "施肥卡")}");
        return Task.CompletedTask;
    }

    // === 联机：种子动作（所有端按动作队列执行同一份逻辑）===

    private static RitsuLibManagedNetActionDescriptor<byte>? s_seedsDescriptor;

    internal static void RegisterSeedsAction()
    {
        if (s_seedsDescriptor != null)
        {
            return;
        }
        try
        {
            s_seedsDescriptor = new RitsuLibManagedNetActionDescriptor<byte>(
                Entry.ModId,
                "daves_seeds",
                static _ => new byte[1],
                static _ => (byte)0,
                ExecuteSeedsAsync,
                GameActionType.CombatPlayPhaseOnly);
            RitsuLibManagedNetActions.Register(s_seedsDescriptor);
            Entry.Logger.Info("[DavesSeeds] 动作已注册到联机动作队列");
        }
        catch (Exception e)
        {
            Entry.Logger.Info($"[DavesSeeds] 动作注册失败（已忽略）：{e}");
        }
    }

    private static async Task ExecuteSeedsAsync(RitsuLibManagedNetActionContext<byte> context)
    {
        try
        {
            var player = context.Player;
            var relic = player?.Relics?.OfType<DavesSeeds>().FirstOrDefault();
            if (relic == null)
            {
                return;
            }

            var phase = context.Message;
            var index = phase == 0
                ? await relic.ChooseSummonIndex(context.PlayerChoiceContext)
                : await relic.ChooseGrowthIndex(context.PlayerChoiceContext);
            if (index is not int choice)
            {
                return;
            }

            if (phase == 0)
            {
                await relic.ApplySummon(context.PlayerChoiceContext, choice);
            }
            else
            {
                await relic.ApplyGrowth(choice);
            }
        }
        catch (Exception e)
        {
            Entry.Logger.Info($"[DavesSeeds] 动作执行异常（已忽略）：{e}");
        }
    }

    // === 选择（所有端都会调用；发起者本机弹界面，其它端等远端结果）===

    // 三选一：向日葵 / 豌豆射手 / 坚果墙（只列出付得起的；返回固定序号，两端一致）
    private async Task<int?> ChooseSummonIndex(PlayerChoiceContext choiceContext)
    {
        var player = Owner;
        var combatState = CombatManager.Instance.DebugOnlyGetState();
        if (player == null || player.PlayerCombatState == null || combatState == null)
        {
            return null;
        }

        var candidates = new List<(int Index, CardModel Card)>();
        for (var i = 0; i < 3; i++)
        {
            var card = combatState.CreateCard(CanonicalPlantCard(i), player);
            if (player.PlayerCombatState.HasEnoughResourcesFor(card, out _))
            {
                candidates.Add((i, card));
            }
        }
        if (candidates.Count == 0)
        {
            Entry.Logger.Info("[DavesSeeds] 没有付得起的植物");
            return null;
        }

        // 复用原版"选择一张牌"界面（与时光布的【抉择】同一套）
        var chosen = await CardSelectCmd.FromChooseACardScreen(
            choiceContext, candidates.Select(c => c.Card).ToList(), player);
        if (chosen == null)
        {
            Entry.Logger.Info("[DavesSeeds] 玩家取消了选择");
            return null;
        }
        foreach (var candidate in candidates)
        {
            if (ReferenceEquals(candidate.Card, chosen))
            {
                return candidate.Index;
            }
        }
        return null;
    }

    // 三选一：施肥卡（基础 / 除外 / 湮灭）
    private async Task<int?> ChooseGrowthIndex(PlayerChoiceContext choiceContext)
    {
        var player = Owner;
        var combatState = CombatManager.Instance.DebugOnlyGetState();
        if (player == null || player.PlayerCombatState == null || combatState == null)
        {
            return null;
        }

        var options = new List<CardModel>
        {
            combatState.CreateCard(ModelDb.Card<PlantGrowthCard>(), player),
            combatState.CreateCard(ModelDb.Card<PlantGrowthExcludeCard>(), player),
            combatState.CreateCard(ModelDb.Card<PlantGrowthAnnihilateCard>(), player),
        };
        var chosen = await CardSelectCmd.FromChooseACardScreen(choiceContext, options, player);
        if (chosen == null)
        {
            Entry.Logger.Info("[DavesSeeds] 玩家取消了选择（不消耗这次机会）");
            return null;
        }
        var index = options.FindIndex(c => ReferenceEquals(c, chosen));
        return index < 0 ? null : index;
    }

    private static CardModel CanonicalPlantCard(int index) => index switch
    {
        0 => ModelDb.Card<SunflowerCard>(),
        1 => ModelDb.Card<PeaShooterCard>(),
        _ => ModelDb.Card<WallNutCard>(),
    };

    // 召唤（所有端各执行一次）：扣费 → 加植物 → 当回合立刻行动一次
    private async Task ApplySummon(PlayerChoiceContext choiceContext, int plantIndex)
    {
        var player = Owner;
        var combatState = CombatManager.Instance.DebugOnlyGetState();
        if (player == null || combatState == null || player.PlayerCombatState == null || AlreadySummoned)
        {
            return;
        }

        var card = combatState.CreateCard(CanonicalPlantCard(plantIndex), player);
        await PlayerCmd.LoseEnergy(card.EnergyCost.GetWithModifiers(CostModifiers.All), player);

        Creature pet = plantIndex switch
        {
            0 => await PlayerCmd.AddPet<SunflowerPlant>(player),
            1 => await PlayerCmd.AddPet<PeaShooterPlant>(player),
            _ => await PlayerCmd.AddPet<WallNutPlant>(player),
        };

        // 召唤当回合立刻行动一次（回合开始钩子已经过去了）
        if (pet.Monster is PlantSummonBase plant)
        {
            await plant.ActOnSummonTurn(choiceContext, player);
        }

        // 本场战斗只允许召唤一次（植物死亡后也不能再召唤）
        MarkSummoned();

        Flash();
        Entry.Logger.Info($"[DavesSeeds] 召唤植物：{pet.Monster.Id.Entry}（{card.Id.Entry}）");
    }

    // 领施肥卡（所有端各执行一次）：领卡本身不花钱（卡自己的 1 费在打出时结算）
    private async Task ApplyGrowth(int index)
    {
        var player = Owner;
        if (player?.Creature == null || AlreadyGotGrowthCard)
        {
            return;
        }

        switch (index)
        {
            case 1:
                await CardPileCmd.AddToCombatAndPreview<PlantGrowthExcludeCard>(
                    player.Creature, PileType.Hand, 1, player);
                break;
            case 2:
                await CardPileCmd.AddToCombatAndPreview<PlantGrowthAnnihilateCard>(
                    player.Creature, PileType.Hand, 1, player);
                break;
            default:
                await CardPileCmd.AddToCombatAndPreview<PlantGrowthCard>(
                    player.Creature, PileType.Hand, 1, player);
                break;
        }

        MarkGotGrowthCard();
        Flash();
        Entry.Logger.Info($"[DavesSeeds] 获得施肥卡：序号={index}");
    }
}
