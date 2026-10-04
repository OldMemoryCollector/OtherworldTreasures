using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Audio;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using OtherworldTreasures.Scripts.Powers;
using STS2RitsuLib;
using STS2RitsuLib.Combat.Ui.ExtraCornerAmountLabels;
using STS2RitsuLib.Interactions.RightClick;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Relics;

// 库拉的骰子：每次战斗开始随机骰1-6点，右键遗物发动对应能力（每点仅限一次）
[RegisterRelic(typeof(SharedRelicPool))]
public class KurasDice : ModRelicTemplate, IModRightClickableRelic,
    IRelicExtraIconAmountLabelSpecsProvider, IRelicExtraIconAmountLabelsChangeSource,
    RelicRightClickSync.ICrossEndRightClickEffect
{
    // === 每位玩家各自的骰子状态 ===
    // 遗物实例每次战斗都会重建，所以状态按"玩家 NetId"保存。
    // 不能用 static 全局字段：两名玩家同时持有骰子时会是同一份状态，
    // 一方用掉点数（比如六点实现愿望）另一方也用不了。
    private sealed class DiceState
    {
        public int LastRolled;
        public bool Broken;
        // 每个点数是否已用（跨战斗持久化）
        public readonly HashSet<int> UsedFaces = new();
        // 二点重放：本场战斗改过 BaseReplayCount 的卡 → 原始值映射
        public readonly Dictionary<CardModel, int> ReplayModifiedCards = new();
    }

    private static readonly Dictionary<ulong, DiceState> s_states = new();

    // 当前所在玩家的状态
    private DiceState State => GetState(Owner);

    private static DiceState GetState(Player? owner)
    {
        var netId = owner?.NetId ?? 0UL;
        if (!s_states.TryGetValue(netId, out var state))
        {
            state = new DiceState();
            s_states[netId] = state;
        }
        return state;
    }

    // 供外部（描述文案补丁）按玩家查询
    public static bool IsBrokenFor(Player? owner) => GetState(owner).Broken;
    public static int RolledFaceFor(Player? owner) => GetState(owner).LastRolled;
    public static bool IsFaceUsedFor(Player? owner, int face) => GetState(owner).UsedFaces.Contains(face);

    public int CurrentRoll { get; private set; }
    public bool IsBroken => State.Broken;
    // 右键判定必须用"跨实例可靠"的 State.LastRolled，不能用实例字段 CurrentRoll
    // ——战斗中遗物会被克隆，右键命中的实例可能 CurrentRoll==0，导致 switch 无匹配静默失败
    public bool CanUseCurrentFace =>
        !State.Broken && State.LastRolled >= 1 && State.LastRolled <= 6 && !State.UsedFaces.Contains(State.LastRolled);

    public override RelicRarity Rarity => RelicRarity.Ancient;

    // RitsuLib 的 AssetProfile 只 patch Godot 端，原版 C# 端 PackedIconPath 按图集找
    // 所以直接 override 原版的虚拟属性绕过它
    protected override string IconBaseName => "kuras_dice";  // 不用于图集查找，纯粹标记
    public override string PackedIconPath => "res://OtherworldTreasures/images/relics/Kuras_Dice.png";
    protected override string PackedIconOutlinePath => "res://OtherworldTreasures/images/relics/Kuras_Dice.png";
    protected override string BigIconPath => "res://OtherworldTreasures/images/relics/Kuras_Dice.png";

    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://OtherworldTreasures/images/relics/Kuras_Dice.png",
        IconOutlinePath: "res://OtherworldTreasures/images/relics/Kuras_Dice.png",
        BigIconPath: "res://OtherworldTreasures/images/relics/Kuras_Dice.png"
    );

    private readonly DynamicVar _currentRollVar = new("CurrentRoll", 0m);
    protected override IEnumerable<DynamicVar> CanonicalVars => new[] { _currentRollVar };

    // 悬浮提示：六个点数的详细能力说明；已用点数 / 六点失效后标注"(已失效)"
    // 注意：局外（图鉴/收藏）不显示运行中的状态，避免把上一局的"已失效"带进图鉴
    protected override IEnumerable<IHoverTip> AdditionalHoverTips
    {
        get
        {
            bool inRun = RunManager.Instance?.IsInProgress ?? false;
            for (int face = 1; face <= 6; face++)
            {
                bool unavailable = inRun && (State.Broken || State.UsedFaces.Contains(face));
                var desc = new LocString("relics", $"OTHERWORLD_TREASURES_RELIC_KURAS_DICE.face_{face}.desc");
                desc.Add("Status", unavailable
                    ? new LocString("gameplay_ui", "OTHERWORLD_TREASURES_UI_FACE_USED").GetFormattedText()
                    : "");
                yield return new HoverTip(
                    new LocString("relics", $"OTHERWORLD_TREASURES_RELIC_KURAS_DICE.face_{face}.title"),
                    desc);
            }
        }
    }

    // === 角标：显示当前骰子面数 ===
    // 角标刷新事件（RitsuLib 监听顶栏遗物格）
    public event Action? RelicExtraIconAmountLabelsInvalidated;

    // 角标变化时通知刷新。必须在顶栏持有的那个遗物实例上触发事件
    // （战斗中遗物可能有克隆实例，事件订阅挂在顶栏实例上）
    private void RefreshBadge()
    {
        var inventoryInstance = Owner?.Relics?.OfType<KurasDice>().FirstOrDefault();
        if (inventoryInstance != null && !ReferenceEquals(inventoryInstance, this))
        {
            inventoryInstance.RelicExtraIconAmountLabelsInvalidated?.Invoke();
        }
        else
        {
            RelicExtraIconAmountLabelsInvalidated?.Invoke();
        }
    }

    // 角标内容：显示当前骰子面数（右下角）
    // RitsuLib 的角标字号继承自原版数量标签并按其框自适应（MinFontSize 约束），无法直接指定；
    // 因此改用 RichText 模式 + BBCode [font_size] 精确控制字号。
    private const int BadgeFontSize = 16;

    public IReadOnlyList<ExtraIconAmountLabelSpec> GetRelicExtraIconAmountLabelSpecs()
    {
        // 局外（图鉴/收藏）不显示角标，避免把上一局的点数带进图鉴
        if (!(RunManager.Instance?.IsInProgress ?? false))
        {
            return new List<ExtraIconAmountLabelSpec>();
        }
        if (State.Broken || State.LastRolled < 1 || State.LastRolled > 6)
        {
            return new List<ExtraIconAmountLabelSpec>();
        }
        return new List<ExtraIconAmountLabelSpec>
        {
            ExtraIconAmountLabelSpec.RichText(
                ExtraIconAmountLabelCorner.BottomRight,
                $"[font_size={BadgeFontSize}]{State.LastRolled}[/font_size]")
        };
    }

    // === 遗物获得：每局新 run 重新获得遗物时，重置所有跨战斗 static 状态 ===
    // 否则上一局用掉的点数 / 六点失效状态会残留到新局，导致右键"无法生效"，必须重启游戏进程
    public override async Task AfterObtained()
    {
        // 新的一局：清掉所有玩家的骰子状态（遗物重新获得）
        s_states.Clear();
        CurrentRoll = 0;
        _currentRollVar.BaseValue = 0m;
        Status = RelicStatus.Normal;
        Entry.Logger.Info("[KurasDice] AfterObtained: All static state reset for new run");
        RefreshBadge();
        await base.AfterObtained();
    }

    // === 进入房间：战斗房间就骰骰子 ===
    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is CombatRoom combatRoom && !State.Broken)
        {
            // 改走联机一致的 RNG（RunState.Rng.Niche，与 PlantSummonBase 同源），避免两端各滚各的分叉
            CurrentRoll = Owner.RunState.Rng.Niche.NextInt(1, 7);
            State.LastRolled = CurrentRoll;
            _currentRollVar.BaseValue = CurrentRoll;
            
            Entry.Logger.Info($"[KurasDice] Enter combat! Rolled {CurrentRoll}, FaceUsed={State.UsedFaces.Contains(CurrentRoll)}");
            RefreshBadge();
        }
        await base.AfterRoomEntered(room);
    }

    // 回合开始：移除 AmplifyPower（三点强化只持续本回合）
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        // 兜底补骰：读档/重进后本场战斗不会再触发 AfterRoomEntered，点数会一直是 0
        EnsureRolledInCombat();

        var amplify = player.Creature.Powers.FirstOrDefault(p => p is AmplifyPower);
        if (amplify != null)
        {
            Entry.Logger.Info("[KurasDice] AfterPlayerTurnStart: Removing AmplifyPower");
            await PowerCmd.Remove(amplify);
        }
        await base.AfterPlayerTurnStart(choiceContext, player);
    }

    // 兜底补骰：点数只在 AfterRoomEntered(战斗开始) 时投掷，而"战斗中途存档 → 退出重进"不会再触发该钩子，
    // static 的 State.LastRolled 会一直停在 0（进程重启后静态字段回到初始值），
    // 导致右键 switch(0) 无匹配而静默失效（表现为"骰子点数用不了"）。
    // 因此在任何需要点数的时机（回合开始 / 右键预检 / 右键执行）都补一次。
    private void EnsureRolledInCombat()
    {
        if (State.Broken)
        {
            return;
        }
        if (State.LastRolled >= 1 && State.LastRolled <= 6)
        {
            return;
        }
        if (CombatManager.Instance.DebugOnlyGetState() == null)
        {
            return; // 非战斗状态不投掷（非战斗显示 0 点是正常的）
        }
        // 改走联机一致的 RNG（RunState.Rng.Niche），避免两端各滚各的分叉
        CurrentRoll = Owner.RunState.Rng.Niche.NextInt(1, 7);
        State.LastRolled = CurrentRoll;
        _currentRollVar.BaseValue = CurrentRoll;
        Entry.Logger.Info(
            $"[KurasDice] 兜底补骰（读档/重进后）：{CurrentRoll}, FaceUsed={State.UsedFaces.Contains(CurrentRoll)}");
        RefreshBadge();
    }

    // 战斗结束：恢复 BaseReplayCount 原始值 + 重置骰点数
    public override async Task AfterCombatEnd(CombatRoom room)
    {
        // 恢复我们改过的卡的原始 BaseReplayCount
        foreach (var kv in State.ReplayModifiedCards.ToList())
        {
            if (kv.Key.BaseReplayCount != kv.Value)
            {
                kv.Key.BaseReplayCount = kv.Value;
            }
        }
        State.ReplayModifiedCards.Clear();
        
        CurrentRoll = 0;
        State.LastRolled = 0;
        _currentRollVar.BaseValue = 0m;
        RefreshBadge();
        await base.AfterCombatEnd(room);
    }

    // === 右键发动能力 ===
    public bool CanHandleRightClickLocal(ModRightClickContext context)
    {
        // 主动注册遗物身份令牌。战斗中遗物可能被克隆，新实例未注册到 ModModelIdentityRegistry，
        // 导致 TryGetToken 失败 → TryRequestSyncedModelAction 返回 false → 右键静默失效。
        // 在 CanHandle 阶段 EnsureRegistered，确保后续 TryCreatePayload 能拿到 token。
        var identity = RitsuLibFramework.EnsureModelIdentity(this);
        Entry.Logger.Info($"[KurasDice-RC] CanHandle: EnsureModelIdentity => {identity.Value} (IsMutable={IsMutable})");
        return true;
    }
    public bool CanExecuteRightClick(ModRightClickExecutionContext context)
    {
        // 注意：这里绝对不能补骰。右键预检只在"发起者一端"跑，
        // 在这里消费共享随机流（RunState.Rng.Niche）会让两端的 RNG 状态分叉（实测过）。
        // 补骰只在"所有端都会走"的时机做：战斗开始 / 回合开始 / 联机动作执行。
        bool can = CanUseCurrentFace;
        Entry.Logger.Info($"[KurasDice-RC] CanExecute => {can} (LastRolled={State.LastRolled}, Broken={State.Broken}, Used=[{string.Join(",", State.UsedFaces)}])");
        return can;
    }

    // 只有发起者本机负责入队；真正的发动由联机动作在所有端统一执行
    public async Task OnRightClick(ModRightClickExecutionContext context)
    {
        if (Owner == null || !Entry.IsLocalPlayer(Owner))
        {
            return;
        }
        RelicRightClickSync.Request(Owner, this);
    }

    async Task RelicRightClickSync.ICrossEndRightClickEffect.ApplyRightClickAsync(PlayerChoiceContext choiceContext)
    {
        // 双保险：真正执行前再保证有点数
        EnsureRolledInCombat();
        // 用 static 的 State.LastRolled（跨实例可靠），不用实例字段 CurrentRoll
        int face = State.LastRolled;
        Entry.Logger.Info($"[KurasDice-RC] ApplyRightClick face={face}");
        if (State.Broken || face < 1 || face > 6 || State.UsedFaces.Contains(face))
        {
            Entry.Logger.Info($"[KurasDice-RC] ABORT: Broken={State.Broken}, face={face}, used={State.UsedFaces.Contains(face)}");
            return;
        }
        State.UsedFaces.Add(face);

        Entry.Logger.Info($"[KurasDice] Using face {face}!");

        // 遗物闪光（顶栏图标闪光 + relic_activate_general 音效）
        Flash();

        switch (face)
        {
            case 1: await AbilityRestore(choiceContext); break;
            case 2: await AbilityCopy(choiceContext); break;
            case 3: await AbilityAmplify(choiceContext); break;
            case 4: await AbilityBarrier(choiceContext); break;
            case 5: await AbilityExplode(choiceContext); break;
            case 6: await AbilityWish(choiceContext); break;
        }
    }

    // === 能力实现 ===

    // 一点恢复：触发篝火休息 + 10 再生
    private async Task AbilityRestore(PlayerChoiceContext choiceContext)
    {
        Entry.Logger.Info("[KurasDice] AbilityRestore: HealRestSite + 10 Regen");
        var healAmount = HealRestSiteOption.GetHealAmount(Owner);
        await HealRestSiteOption.ExecuteRestSiteHeal(Owner, isMimicked: true);
        // 全屏治疗特效（绿光 + 粒子，与事件治疗一致；ExecuteRestSiteHeal 内部已播治疗音效和十字VFX）
        if (MegaCrit.Sts2.Core.Context.LocalContext.IsMe(Owner))
        {
            PlayerFullscreenHealVfx.Play(Owner, healAmount, NCombatRoom.Instance?.CombatVfxContainer);
        }
        await PowerCmd.Apply<RegenPower>(choiceContext, Owner.Creature, 10m, Owner.Creature, null!, false);
    }

    // 二点复制：给所有卡牌 BaseReplayCount +1（原版重放机制）
    private async Task AbilityCopy(PlayerChoiceContext choiceContext)
    {
        int count = 0;
        foreach (var pileType in new[] { PileType.Draw, PileType.Hand, PileType.Discard, PileType.Exhaust })
        {
            var pile = pileType.GetPile(Owner);
            foreach (var card in pile.Cards.Cast<CardModel>())
            {
                // 只在第一次记录原始值
                if (!State.ReplayModifiedCards.ContainsKey(card))
                {
                    State.ReplayModifiedCards[card] = card.BaseReplayCount;
                }
                card.BaseReplayCount += 1;
                count++;
            }
        }
        // 附魔闪光音效 + 玩家身上强化特效
        SfxCmd.Play(FmodSfx.enchant);
        NPowerUpVfx.CreateNormal(Owner.Creature);
        Entry.Logger.Info($"[KurasDice] AbilityCopy: Set BaseReplayCount+1 on {count} cards");
        await Task.CompletedTask;
    }

    // 三点强化：本回合伤害/格挡 ×3 + 3 能量
    private async Task AbilityAmplify(PlayerChoiceContext choiceContext)
    {
        Entry.Logger.Info("[KurasDice] AbilityAmplify: Applying AmplifyPower + 3 Energy");
        NPowerUpVfx.CreateNormal(Owner.Creature);
        SfxCmd.Play(FmodSfx.buff);
        var applied = await PowerCmd.Apply<AmplifyPower>(choiceContext, Owner.Creature, 1m, Owner.Creature, null!, false);
        Entry.Logger.Info($"[KurasDice] AmplifyPower applied: {applied != null}");
        PlayerCmd.GainEnergy(3, Owner);
        Entry.Logger.Info("[KurasDice] +3 Energy gained");
    }

    // 四点结界：8 层缓冲 + 8 层无实体 + 8 层人工制品
    private async Task AbilityBarrier(PlayerChoiceContext choiceContext)
    {
        Entry.Logger.Info("[KurasDice] AbilityBarrier: 8 Buffer + 8 Intangible + 8 Artifact");
        NPowerUpVfx.CreateGhostly(Owner.Creature);
        SfxCmd.Play(FmodSfx.buff);
        await PowerCmd.Apply<BufferPower>(choiceContext, Owner.Creature, 8m, Owner.Creature, null!, false);
        await PowerCmd.Apply<IntangiblePower>(choiceContext, Owner.Creature, 8m, Owner.Creature, null!, false);
        await PowerCmd.Apply<ArtifactPower>(choiceContext, Owner.Creature, 8m, Owner.Creature, null!, false);
    }

    // 五点爆炸：对每个敌人造成联机人数次 300 点伤害（逐次独立结算）
    private async Task AbilityExplode(PlayerChoiceContext choiceContext)
    {
        var choiceCtx = choiceContext;
        var combatState = CombatManager.Instance.DebugOnlyGetState();
        var playerCount = combatState.Players.Count;
        var enemies = combatState.HittableEnemies.ToList();
        if (enemies.Count == 0) return;

        Entry.Logger.Info($"[KurasDice] AbilityExplode: {playerCount} players × 300 to each of {enemies.Count} enemies, {playerCount} independent hits each");

        // 火焰音效 + 每个敌人脚下火爆特效（与原版 InfernoPower 一致）
        SfxCmd.Play(FmodSfx.fire);
        foreach (var enemy in enemies)
        {
            var burst = NFireBurstVfx.Create(enemy, 1f);
            if (burst != null)
            {
                NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(burst);
            }
        }

        // 对每个敌人重复 playerCount 次，每次 300 点，逐次独立结算
        foreach (var enemy in enemies)
        {
            for (int i = 0; i < playerCount; i++)
            {
                await CreatureCmd.Damage(choiceCtx, enemy, 300m, ValueProp.Unblockable, null);
            }
        }
    }

    // 六点愿望：999金 + 满血 + 升级所有卡 + 战斗结束6件遗物 + 永久失效
    private async Task AbilityWish(PlayerChoiceContext choiceContext)
    {
        Entry.Logger.Info("[KurasDice] AbilityWish: WISH GRANTED!");

        // 金币：走原版 GainGold（≥100 自动播 gold_3 音效）
        await PlayerCmd.GainGold(999, Owner);

        // 满血：走原版 Heal（治疗音效 + 十字VFX），再叠加全屏治疗特效
        var missingHp = Owner.Creature.MaxHp - Owner.Creature.CurrentHp;
        await CreatureCmd.Heal(Owner.Creature, missingHp);
        if (MegaCrit.Sts2.Core.Context.LocalContext.IsMe(Owner))
        {
            PlayerFullscreenHealVfx.Play(Owner, missingHp, NCombatRoom.Instance?.CombatVfxContainer);
        }

        // 升级全局 Deck（已经包含在 Owner.Piles 里了），附魔音效 + 玩家强化特效
        var allCards = Owner.Piles.SelectMany(p => p.Cards).Cast<CardModel>().ToList();
        Entry.Logger.Info($"[KurasDice] Wish upgrade: piles={Owner.Piles.Count()}, totalCards={allCards.Count}, upgradable={allCards.Count(c => c.IsUpgradable)}");
        foreach (var p in Owner.Piles)
        {
            Entry.Logger.Info($"[KurasDice]   pile={p.Type} cards={p.Cards.Count}");
        }
        CardCmd.Upgrade(allCards, CardPreviewStyle.None);
        SfxCmd.Play(FmodSfx.enchant);
        NPowerUpVfx.CreateNormal(Owner.Creature);
        Entry.Logger.Info($"[KurasDice] Upgraded {allCards.Count(c => c.IsUpgraded)} cards (now upgraded)");

        // 战斗内直接获得 6 件遗物（走原版遗物池 + 原版获得流程，含获得动画与音效）
        for (int i = 0; i < 6; i++)
        {
            var relic = RelicFactory
                .PullNextRelicFromFront(Owner, RelicFactory.RollRarity(Owner), IsWishRelicAllowed)
                .ToMutable();
            await RelicCmd.Obtain(relic, Owner);
            Entry.Logger.Info($"[KurasDice] Wish relic #{i + 1}: {relic.GetType().Name} ({relic.Rarity})");
        }

        // 永久失效：设置 RelicStatus.Disabled → UI 自动变灰
        State.Broken = true;
        Status = RelicStatus.Disabled;
        RefreshBadge();
        Entry.Logger.Info("[KurasDice] DICE BROKEN (Status=Disabled, UI=Gray)");
    }

    // 六点愿望的遗物筛选：排除纯升卡类遗物（愿望本身已升级全部卡牌，这些遗物毫无意义）
    // 磨刀石 / 战纹涂料 / 碎石钻 / 风箱
    private static bool IsWishRelicAllowed(RelicModel relic) =>
        relic is not Whetstone and not WarPaint and not StoneCracker and not Bellows;
}
