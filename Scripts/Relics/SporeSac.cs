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

        // 在攻击者身上播放绿色版"施加负面"意图特效（纯本地视觉，不影响游戏状态）
        SpawnGreenDebuffVfx(dealer);
    }

    // 绿色版 debuff 意图贴图缓存（红绿通道对调一次，之后复用）
    private static Texture2D? s_greenDebuffTexture;

    // 在攻击者身上播放"施加负面"意图的粒子特效（克隆原版 intent.tscn 里的粒子，
    // 继承原版全部参数），贴图换成 debuff 图标并转成中毒绿。
    // 只在本地播放、不影响任何游戏状态，联机各客户端表现一致（触发时机确定）。
    private void SpawnGreenDebuffVfx(Creature dealer)
    {
        try
        {
            var node = NCombatRoom.Instance?.GetCreatureNode(dealer);
            if (node == null)
            {
                return;
            }

            // 实例化原版意图场景但绝不把场景本体加进树（它的 _EnterTree 会挂接原版战斗信号，
            // _intent 为空时触发状态变化会空引用），只克隆出其中的粒子节点来用
            var intentScene = ResourceLoader.Load<PackedScene>("res://scenes/combat/intent.tscn");
            if (intentScene?.Instantiate() is not { } intentRoot)
            {
                return;
            }
            var particles = intentRoot.GetNodeOrNull<CpuParticles2D>("%IntentParticle")?.Duplicate() as CpuParticles2D;
            intentRoot.Free();
            if (particles == null)
            {
                return;
            }

            // 贴图：debuff 意图图标，像素级红绿通道对调成中毒绿（红色无法用染色变绿，乘法只会变黑）
            if (s_greenDebuffTexture == null
                && ResourceLoader.Exists("res://images/atlases/intent_atlas.sprites/intent_debuff.tres"))
            {
                var img = GD.Load<Texture2D>("res://images/atlases/intent_atlas.sprites/intent_debuff.tres")?.GetImage();
                if (img != null)
                {
                    img.Convert(Image.Format.Rgba8);
                    for (int y = 0; y < img.GetHeight(); y++)
                    {
                        for (int x = 0; x < img.GetWidth(); x++)
                        {
                            var p = img.GetPixel(x, y);
                            img.SetPixel(x, y, new Color(p.G, p.R, p.B, p.A));
                        }
                    }
                    s_greenDebuffTexture = ImageTexture.CreateFromImage(img);
                }
            }
            if (s_greenDebuffTexture != null)
            {
                particles.Texture = s_greenDebuffTexture;
            }

            // 挂到攻击者身上：先入树定位，再单发爆发
            node.AddChild(particles);
            particles.GlobalPosition = node.VfxSpawnPosition;
            particles.ZIndex = 20;
            particles.OneShot = true;
            particles.Emitting = true;
            node.GetTree().CreateTimer(3.0).Timeout += () =>
            {
                if (GodotObject.IsInstanceValid(particles))
                {
                    particles.QueueFree();
                }
            };
        }
        catch (Exception ex)
        {
            // 特效失败绝不能打断战斗钩子流程，记日志即可
            Entry.Logger.Warn($"[SporeSac] 绿色负面特效播放失败：{ex.Message}");
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
