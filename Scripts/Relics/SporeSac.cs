using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using OtherworldTreasures.Scripts.Powers;
using STS2RitsuLib.Combat.Ui.ExtraCornerAmountLabels;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Relics;

// 孢子囊（来自《泰拉瑞亚》Spore Sac）：
// 每次自己受到攻击（实际掉血）时，对攻击者施加中毒。
// 层数 = 基础层数 + 战斗内加成：基础层数初始 1，每赢得两场战斗永久 +1；
// 同一场战斗内，从第二个回合起每过一回合再 +1（战斗结束不保留）。
[RegisterRelic(typeof(SharedRelicPool))]
public class SporeSac : ModRelicTemplate,
    IRelicExtraIconAmountLabelSpecsProvider, IRelicExtraIconAmountLabelsChangeSource
{
    private const int InitialPoisonStacks = 1;
    private const int WinsPerBonus = 2;

    // 角标字号：RitsuLib 的角标字号继承自原版数量标签（很大），必须用 RichText 指定字号，
    // 与库拉的骰子保持一致（否则数字巨大）
    private const int BadgeFontSize = 16;

    // 中毒层数角标用中毒绿
    private const string PoisonGreen = "#84d64b";

    // 跨战斗实例共享（战斗中遗物会被克隆）；SavedProperty 随存档保存
    private static int s_poisonStacks = InitialPoisonStacks;
    private static int s_winsTowardNext;

    // 战斗内回合计数（每场战斗重置，不进存档）
    private static int s_combatTurn;

    private int _savedPoisonStacks = InitialPoisonStacks;
    private int _savedWinsTowardNext;

    [SavedProperty]
    public int SavedPoisonStacks
    {
        get => _savedPoisonStacks;
        set
        {
            AssertMutable();
            _savedPoisonStacks = value;
        }
    }

    [SavedProperty]
    public int SavedWinsTowardNext
    {
        get => _savedWinsTowardNext;
        set
        {
            AssertMutable();
            _savedWinsTowardNext = value;
        }
    }

    // 玩家身上真正的那件遗物（受击钩子可能命中克隆实例）
    private SporeSac? LiveRelic => Owner?.Relics?.OfType<SporeSac>().FirstOrDefault();

    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://OtherworldTreasures/images/relics/Spore_Sac.png",
        IconOutlinePath: "res://OtherworldTreasures/images/relics/Spore_Sac.png",
        BigIconPath: "res://OtherworldTreasures/images/relics/Spore_Sac.png"
    );

    // === 当前中毒层数角标 ===
    public event Action? RelicExtraIconAmountLabelsInvalidated;

    private void RefreshBadge()
    {
        var live = LiveRelic;
        if (live != null && !ReferenceEquals(live, this))
        {
            live.RelicExtraIconAmountLabelsInvalidated?.Invoke();
        }
        else
        {
            RelicExtraIconAmountLabelsInvalidated?.Invoke();
        }
    }

    public IReadOnlyList<ExtraIconAmountLabelSpec> GetRelicExtraIconAmountLabelSpecs()
    {
        // 局外（图鉴/收藏）不显示角标，避免把上一局的层数带进图鉴
        if (!(RunManager.Instance?.IsInProgress ?? false))
        {
            return new List<ExtraIconAmountLabelSpec>();
        }
        return new List<ExtraIconAmountLabelSpec>
        {
            // 左上：距离下一次层数成长还需的胜利场数（如 1/2）
            ExtraIconAmountLabelSpec.RichText(
                ExtraIconAmountLabelCorner.TopLeft,
                $"[font_size={BadgeFontSize}]{s_winsTowardNext}/{WinsPerBonus}[/font_size]"),
            // 右下：当前基础中毒层数（中毒绿）
            ExtraIconAmountLabelSpec.RichText(
                ExtraIconAmountLabelCorner.BottomRight,
                $"[font_size={BadgeFontSize}][color={PoisonGreen}]{s_poisonStacks}[/color][/font_size]")
        };
    }

    public override async Task AfterObtained()
    {
        s_poisonStacks = InitialPoisonStacks;
        s_winsTowardNext = 0;
        s_combatTurn = 0;
        SavedPoisonStacks = InitialPoisonStacks;
        SavedWinsTowardNext = 0;
        RefreshBadge();
        await base.AfterObtained();
    }

    // 读档/新开跑时复位（见 RunLifecycle）：只清战斗内回合计数；
    // 基础层数与胜利进度是跨战斗持久数据（有 SavedProperty），由 SyncFromSave 从存档恢复，不能在这里清
    internal static void ResetCombatScopedState()
    {
        s_combatTurn = 0;
    }

    // 读档后 static 会重置为初始值，用存档值校正（存档权威）
    private void SyncFromSave()
    {
        var live = LiveRelic ?? this;
        if (live._savedPoisonStacks != s_poisonStacks || live._savedWinsTowardNext != s_winsTowardNext)
        {
            s_poisonStacks = live._savedPoisonStacks;
            s_winsTowardNext = live._savedWinsTowardNext;
            Entry.Logger.Info($"[SporeSac] 从存档同步：基础层数 {s_poisonStacks}，胜利进度 {s_winsTowardNext}/{WinsPerBonus}");
            RefreshBadge();
        }
    }

    public override async Task BeforeCombatStart()
    {
        SyncFromSave();
        s_combatTurn = 0;
        // 挂上孢子囊面板能力（纯展示）：让玩家随时能看到当前能给攻击者反多少层中毒
        if (Owner != null)
        {
            await PowerCmd.Apply<SporeSacPower>(
                new ThrowingPlayerChoiceContext(), Owner.Creature, s_poisonStacks, Owner.Creature, null);
        }
        await base.BeforeCombatStart();
    }

    // 每个玩家回合开始累计回合计数（战斗内中毒加成用），并同步面板能力层数
    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner)
        {
            s_combatTurn++;
            RefreshDisplayPower();
        }
        return base.AfterPlayerTurnStart(choiceContext, player);
    }

    // 刷新孢子囊面板能力的层数（纯展示，与实际施加的中毒层数同一公式）
    private void RefreshDisplayPower()
    {
        var power = Owner?.Creature?.Powers?.OfType<SporeSacPower>().FirstOrDefault();
        power?.SetAmount(s_poisonStacks + Math.Max(0, s_combatTurn - 1), silent: true);
    }

    // 自己受到攻击时（即使伤害被格挡完全抵消也触发，按攻击次数），给攻击者施加中毒
    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext, Creature target, DamageResult result,
        ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        SyncFromSave();

        if (target != Owner?.Creature
            || result.TotalDamage <= 0
            || dealer is not { Side: CombatSide.Enemy } || dealer.IsDead)
        {
            return;
        }

        // 基础层数 + 战斗内加成：从第二个回合起每过一回合 +1
        int stacks = s_poisonStacks + Math.Max(0, s_combatTurn - 1);
        await PowerCmd.Apply<PoisonPower>(
            choiceContext, dealer, stacks, Owner!.Creature, null!, false);
        Entry.Logger.Info(
            $"[SporeSac] 对攻击者 {dealer.GetType().Name} 施加 {stacks} 层中毒" +
            $"（基础 {s_poisonStacks} + 战斗内 {Math.Max(0, s_combatTurn - 1)}，第 {s_combatTurn} 回合）");

        // 自己身上爆出绿色孢子（纯本地视觉，不影响游戏状态）——孢子囊被击中时喷出孢子
        SpawnSporeBurstVfx();
    }

    // 孢子爆发用的贴图与粒子材质：只在首次播放时构建一次，之后每次爆发复用。
    // （每次受击都要播放，绝不能每次去加载场景 / 逐像素处理贴图，否则战斗会掉帧）
    private static Texture2D? s_sporeTexture;
    private static ParticleProcessMaterial? s_sporeMaterial;
    private static bool s_sporeTextureMissingLogged;

    // 「石化刺胞 / sludge spinner」喷油用的那张小液滴贴图（49x48），拿来做孢子
    private const string SporeParticleTexturePath =
        "res://images/vfx/monsters/sludge_spinner/sludge_spinner_particle_1.png";

    // 孢子绿（与遗物角标同色 #84d64b）
    private static readonly Color SporeGreen = new(0.518f, 0.839f, 0.294f);

    // 借用原版「sludge spinner 喷油」那套粒子（同贴图、同液滴尺度），改成孢子绿、
    // 从自己身上朝四面八方炸开，表示孢子囊被击中时喷出孢子。
    // 只复用贴图和参数，不实例化原版任何场景，播放成本极低。
    private void SpawnSporeBurstVfx()
    {
        try
        {
            var selfNode = NCombatRoom.Instance?.GetCreatureNode(Owner?.Creature);
            var container = NCombatRoom.Instance?.CombatVfxContainer;
            if (selfNode == null || container == null)
            {
                return;
            }

            s_sporeTexture ??= GD.Load<Texture2D>(SporeParticleTexturePath);
            if (s_sporeTexture == null)
            {
                if (!s_sporeTextureMissingLogged)
                {
                    s_sporeTextureMissingLogged = true;
                    Entry.Logger.Warn($"[SporeSac] 找不到孢子贴图 {SporeParticleTexturePath}，孢子爆发特效已跳过");
                }
                return;
            }
            s_sporeMaterial ??= new ParticleProcessMaterial
            {
                ParticleFlagDisableZ = true,
                // 默认 Point 发射形状：从自己身上一个点向四周爆开
                Direction = new Vector3(0f, -1f, 0f),
                Spread = 180f, // 全方向
                InitialVelocityMin = 280f,
                InitialVelocityMax = 620f,
                AngularVelocityMin = -180f,
                AngularVelocityMax = 180f,
                Gravity = new Vector3(0f, -120f, 0f),
                DampingMin = 60f,
                DampingMax = 140f,
                ScaleMin = 0.15f,
                ScaleMax = 0.35f,
                Color = Colors.White,
            };

            var burst = new GpuParticles2D
            {
                Texture = s_sporeTexture,
                ProcessMaterial = s_sporeMaterial,
                Amount = 140,
                Lifetime = 0.7,
                OneShot = true,
                Explosiveness = 1f,
                LocalCoords = false, // 喷出后不跟随角色移动
                SelfModulate = SporeGreen,
            };
            // 与游戏自己的上负面特效同一容器（原版 NPowerAppliedDebuffVfx 也加在这里），坐标即世界坐标
            burst.GlobalPosition = selfNode.VfxSpawnPosition;

            // 与原版一样延迟挂载，避免在节点树遍历中改结构
            Callable.From(() =>
            {
                if (!GodotObject.IsInstanceValid(container) || !GodotObject.IsInstanceValid(burst))
                {
                    return;
                }
                container.AddChild(burst);
                burst.Restart();
                container.GetTree().CreateTimer(2.0).Timeout += () =>
                {
                    if (GodotObject.IsInstanceValid(burst))
                    {
                        burst.QueueFree();
                    }
                };
            }).CallDeferred();
        }
        catch (Exception ex)
        {
            // 特效失败绝不能打断战斗钩子流程，记日志即可
            Entry.Logger.Warn($"[SporeSac] 孢子爆发特效播放失败：{ex.Message}");
        }
    }

    // 每赢得两场战斗，基础中毒层数 +1
    public override async Task AfterCombatVictory(CombatRoom room)
    {
        s_winsTowardNext++;
        bool stackIncreased = false;
        if (s_winsTowardNext >= WinsPerBonus)
        {
            s_winsTowardNext = 0;
            s_poisonStacks++;
            stackIncreased = true;
        }

        var live = LiveRelic;
        if (live != null && !ReferenceEquals(live, this))
        {
            live.SavedPoisonStacks = s_poisonStacks;
            live.SavedWinsTowardNext = s_winsTowardNext;
        }
        else if (IsMutable)
        {
            SavedPoisonStacks = s_poisonStacks;
            SavedWinsTowardNext = s_winsTowardNext;
        }

        // 每场胜利都闪光：层数虽是每两场 +1，但胜利本身必须有反馈，否则第一场胜利像遗物没生效
        Flash();
        RefreshBadge();
        if (stackIncreased)
        {
            Entry.Logger.Info($"[SporeSac] 两场胜利达成，基础中毒层数提升至 {s_poisonStacks}");
        }
        else
        {
            Entry.Logger.Info($"[SporeSac] 战斗胜利，距离下一次 +1 还差 {WinsPerBonus - s_winsTowardNext} 场");
        }
        await base.AfterCombatVictory(room);
    }
}
