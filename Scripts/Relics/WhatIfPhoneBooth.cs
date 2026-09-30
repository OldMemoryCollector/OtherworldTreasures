using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib;
using STS2RitsuLib.Interactions.RightClick;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Relics;

// 如果电话亭：右键重新开始本次战斗，以一个新的时间线（换掉决定抽牌的随机数）。
// 原版下"退出重进"卡牌顺序是一样的（随机数是确定性的），这个遗物会换一个新的种子，
// 所以每次重开拿到的手牌都不同。
// 实现：用原版"跳进指定房间"通道 EnterRoomDebug（控制台 fight 命令同款），
// 传入当前这场遭遇战的全新副本（怪物重新生成），而不是走存档载入通道。
[RegisterRelic(typeof(SharedRelicPool))]
public class WhatIfPhoneBooth : ModRelicTemplate, IDoraemonItem, IModRightClickableRelic
{
    // 每场战斗的使用次数上限。当前给 999（等于不限制），后续要加限制改这里即可。
    private const int maxUsesPerCombat = 999;

    // 本场战斗已用次数（重开战斗后仍保留，战斗结束才复位）
    private static int s_usesThisCombat;

    // 战斗开始时的玩家血量，重开时还原（等同"退出重进"回到战斗开始的血量）
    private static int s_combatStartHp = -1;

    // 哆啦A梦道具：自定义稀有度
    public override RelicRarity Rarity => ModRelicRarity.Doraemon;

    // 原版 MerchantCost 的 switch 遇到未知稀有度会抛异常，这里直接给值
    public override int MerchantCost => 999999999;

    protected override string IconBaseName => "what_if_phone_booth";

    protected override string BigIconPath => "res://OtherworldTreasures/images/relics/What_If_Phone_Booth.jpg";

    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://OtherworldTreasures/images/relics/What_If_Phone_Booth.jpg",
        IconOutlinePath: "res://OtherworldTreasures/images/relics/What_If_Phone_Booth.jpg",
        BigIconPath: "res://OtherworldTreasures/images/relics/What_If_Phone_Booth.jpg"
    );

    // 悬浮提示：补充"新的时间线"的含义
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => base.AdditionalHoverTips.Append(
        new HoverTip(
            new LocString("relics", "OTHERWORLD_TREASURES_RELIC_WHAT_IF_PHONE_BOOTH.tip.title"),
            new LocString("relics", "OTHERWORLD_TREASURES_RELIC_WHAT_IF_PHONE_BOOTH.tip.description")));

    public override async Task AfterObtained()
    {
        s_usesThisCombat = 0;
        s_combatStartHp = -1;
        var identity = RitsuLibFramework.EnsureModelIdentity(this);
        Entry.Logger.Info($"[WhatIfPhoneBooth] AfterObtained, EnsureModelIdentity => {identity.Value}");
        await base.AfterObtained();
    }

    // 战斗开始：记录起始血量（重开战斗时会再次触发，用已用次数防止覆盖）
    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is CombatRoom && s_usesThisCombat == 0)
        {
            s_combatStartHp = Owner?.Creature?.CurrentHp ?? -1;
            Entry.Logger.Info($"[WhatIfPhoneBooth] Combat start HP snapshot = {s_combatStartHp}");
        }
        await base.AfterRoomEntered(room);
    }

    // 战斗结束：复位本场已用次数
    public override async Task AfterCombatEnd(CombatRoom room)
    {
        s_usesThisCombat = 0;
        await base.AfterCombatEnd(room);
    }

    // 读档/新开跑时复位（见 RunLifecycle）：允许玩家 SL 后重新使用
    internal static void ResetCombatScopedState()
    {
        s_usesThisCombat = 0;
        s_combatStartHp = -1;
    }

    public bool CanHandleRightClickLocal(ModRightClickContext context)
    {
        RitsuLibFramework.EnsureModelIdentity(this);
        return true;
    }

    // 仅战斗内、且本场未超上限时可点
    public bool CanExecuteRightClick(ModRightClickExecutionContext context)
    {
        return Owner != null
            && s_usesThisCombat < maxUsesPerCombat
            && CombatManager.Instance.DebugOnlyGetState() != null;
    }

    public async Task OnRightClick(ModRightClickExecutionContext context)
    {
        await RestartCombatWithNewTimeline();
    }

    private async Task RestartCombatWithNewTimeline()
    {
        if (Owner?.RunState is not RunState runState)
        {
            return;
        }
        var runManager = RunManager.Instance;
        if (runManager == null)
        {
            return;
        }
        // 只有身处战斗房间时才谈得上"重开本场战斗"
        if (runState.CurrentRoom is not CombatRoom currentRoom)
        {
            Entry.Logger.Info("[WhatIfPhoneBooth] Not in a combat room, aborting");
            return;
        }
        if (runManager.ActionExecutor == null)
        {
            return;
        }

        s_usesThisCombat++;

        // 1) 换一个新的时间线：重播种影响洗牌/选牌的随机数
        ulong seed = (ulong)System.Random.Shared.NextInt64();
        runState.Rng.Shuffle.LoadFromSerializable(new Rng(seed).ToSerializable());
        runState.Rng.CombatCardSelection.LoadFromSerializable(new Rng(seed + 1).ToSerializable());

        // 2) 血量还原到战斗开始时（等同"退出重进"）
        // 注意：若战斗中途存档重进，本场开始的快照在进程重启后已丢失（static 重置），此时跳过回血，
        // 但"重开本场战斗"的主体效果不受影响。
        if (Owner.Creature != null && s_combatStartHp > 0)
        {
            int delta = s_combatStartHp - Owner.Creature.CurrentHp;
            if (delta > 0)
            {
                await CreatureCmd.Heal(Owner.Creature, delta);
            }
        }
        else
        {
            Entry.Logger.Info("[WhatIfPhoneBooth] 无本场开始血量快照（多为读档/重进），跳过回血");
        }

        // 3) 取当前这场遭遇战的全新副本 → 同一场战斗，怪物重新生成
        var encounter = ModelDb.GetById<EncounterModel>(currentRoom.Encounter.Id).ToMutable();
        encounter.DebugRandomizeRng();

        Flash();
        Entry.Logger.Info(
            $"[WhatIfPhoneBooth] Restarting combat: encounter={encounter.Id.Entry}, roomType={currentRoom.RoomType}, " +
            $"seed={seed}, usesThisCombat={s_usesThisCombat}");

        // 4) 走原版"跳进指定房间"通道重新进入这场战斗
        await runManager.EnterRoomDebug(currentRoom.RoomType, MapPointType.Unassigned, encounter);
        runManager.ActionExecutor.Unpause();

        Entry.Logger.Info(
            $"[WhatIfPhoneBooth] Restart finished: inCombat={CombatManager.Instance.IsInProgress}, " +
            $"currentRoom={runState.CurrentRoom?.GetType().Name}");
    }
}
