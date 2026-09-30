using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rooms;
using STS2RitsuLib;
using STS2RitsuLib.Interactions.RightClick;
using STS2RitsuLib.Interop.AutoRegistration;
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
    // 本场战斗是否已经召唤过。
    // static：战斗中遗物会被克隆，用 static 跨实例共享（与时光布/库拉的骰子一致）。
    // 刻意不做 [SavedProperty] 持久化：允许玩家战斗中存档→重进（SL）后重新召唤。
    private static bool s_summonedThisCombat;

    // 本场战斗是否已经领过【施肥】卡（同样不持久化，允许 SL）
    private static bool s_gotGrowthCardThisCombat;

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
    private bool AlreadySummoned => s_summonedThisCombat || (LiveRelic?.SummonedThisCombat ?? false);

    // 本场是否已经领过【施肥】卡
    private bool AlreadyGotGrowthCard => s_gotGrowthCardThisCombat || (LiveRelic?.GotGrowthCardThisCombat ?? false);

    // 读档/新开跑时复位（见 RunLifecycle）：允许玩家 SL 后重新召唤/领卡
    internal static void ResetCombatScopedState()
    {
        s_summonedThisCombat = false;
        s_gotGrowthCardThisCombat = false;
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
            new DynamicVar("Energy", SunflowerPlant.EnergyPerTurn)))
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
        s_summonedThisCombat = false;
        SummonedThisCombat = false;
        s_gotGrowthCardThisCombat = false;
        GotGrowthCardThisCombat = false;
        Status = RelicStatus.Normal;
    }

    // 标记"本场已召唤"（static 与存档字段都要写）
    // 注意：召唤完图标保持可点，因为还有第二段用法（领【施肥】卡）
    private void MarkSummoned()
    {
        s_summonedThisCombat = true;
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
        s_gotGrowthCardThisCombat = true;
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

    // 右键：先按"是否召唤过"分流到两段用法
    public async Task OnRightClick(ModRightClickExecutionContext context)
    {
        if (!AlreadySummoned)
        {
            await SummonByChoice(context.PlayerChoiceContext);
            return;
        }
        await OfferGrowthCard(context.PlayerChoiceContext);
    }

    // 右键：弹出卡牌三选一（向日葵/豌豆射手/坚果墙），选中后扣掉该卡费用并召唤
    private async Task SummonByChoice(PlayerChoiceContext choiceContext)
    {
        var player = Owner;
        if (player == null)
        {
            return;
        }
        if (AlreadySummoned)
        {
            Entry.Logger.Info("[DavesSeeds] SummonByChoice aborted: already summoned this combat");
            return;
        }
        var combatState = CombatManager.Instance.DebugOnlyGetState();
        if (combatState == null || player.PlayerCombatState == null)
        {
            Entry.Logger.Info("[DavesSeeds] SummonByChoice aborted: not in combat");
            return;
        }

        // 只列出当前付得起的植物（费用写在各自的召唤卡上）
        var options = new List<CardModel>();
        foreach (var canonical in new CardModel[]
                 {
                     ModelDb.Card<SunflowerCard>(),
                     ModelDb.Card<PeaShooterCard>(),
                     ModelDb.Card<WallNutCard>(),
                 })
        {
            var card = combatState.CreateCard(canonical, player);
            if (player.PlayerCombatState.HasEnoughResourcesFor(card, out _))
            {
                options.Add(card);
            }
        }
        if (options.Count == 0)
        {
            Entry.Logger.Info("[DavesSeeds] 没有付得起的植物");
            return;
        }

        // 复用原版"选择一张牌"界面（与时光布的【抉择】同一套）
        var chosen = await CardSelectCmd.FromChooseACardScreen(
            new BlockingPlayerChoiceContext(), options, player);
        if (chosen == null)
        {
            Entry.Logger.Info("[DavesSeeds] 玩家取消了选择");
            return;
        }

        // 扣掉该卡的费用
        await PlayerCmd.LoseEnergy(chosen.EnergyCost.GetWithModifiers(CostModifiers.All), player);

        // 召唤选中的植物
        Creature pet = chosen switch
        {
            SunflowerCard => await PlayerCmd.AddPet<SunflowerPlant>(player),
            PeaShooterCard => await PlayerCmd.AddPet<PeaShooterPlant>(player),
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
        Entry.Logger.Info($"[DavesSeeds] 召唤植物：{pet.Monster.Id.Entry}（{chosen.Id.Entry}）");
    }

    // 第二段用法：三选一给一张【施肥】卡（直接进手牌），每场战斗只能领一张，领完图标变灰
    private async Task OfferGrowthCard(PlayerChoiceContext choiceContext)
    {
        var player = Owner;
        if (player == null)
        {
            return;
        }
        if (AlreadyGotGrowthCard)
        {
            Entry.Logger.Info("[DavesSeeds] OfferGrowthCard aborted: already got the card this combat");
            return;
        }
        var combatState = CombatManager.Instance.DebugOnlyGetState();
        if (combatState == null || player.PlayerCombatState == null)
        {
            Entry.Logger.Info("[DavesSeeds] OfferGrowthCard aborted: not in combat");
            return;
        }

        // 三张卡效果一致，区别只在词条（基础 / 除外 / 湮灭）
        var options = new List<CardModel>
        {
            combatState.CreateCard(ModelDb.Card<PlantGrowthCard>(), player),
            combatState.CreateCard(ModelDb.Card<PlantGrowthExcludeCard>(), player),
            combatState.CreateCard(ModelDb.Card<PlantGrowthAnnihilateCard>(), player),
        };

        // 复用原版"选择一张牌"界面（与召唤步骤同一套）
        var chosen = await CardSelectCmd.FromChooseACardScreen(
            new BlockingPlayerChoiceContext(), options, player);
        if (chosen == null)
        {
            Entry.Logger.Info("[DavesSeeds] 玩家取消了选择（不消耗这次机会）");
            return;
        }

        // 领卡本身不花钱（卡自己的 1 费在打出时结算），直接进手牌
        switch (chosen)
        {
            case PlantGrowthExcludeCard:
                await CardPileCmd.AddToCombatAndPreview<PlantGrowthExcludeCard>(
                    player.Creature, PileType.Hand, 1, player);
                break;
            case PlantGrowthAnnihilateCard:
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
        Entry.Logger.Info($"[DavesSeeds] 获得施肥卡：{chosen.Id.Entry}");
    }
}
