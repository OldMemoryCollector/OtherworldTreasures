using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using OtherworldTreasures.Scripts.FourSouls;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;

namespace OtherworldTreasures.Scripts.Powers;

/// <summary>
/// 四魂（敌人专用显示能力）：只挂在"带四魂的敌人"身上，用来一眼认出哪个敌人带魂。
/// 图标固定用遗物【四魂之玉碎片】的原图，**不标数字**；
/// 描述直接写出这个敌人持有哪几种魂（荒魂 / 和魂 / 幸魂 / 奇魂）。
/// 玩家侧不需要它——遗物栏本来就显示了四魂。
/// 本身不提供任何数值：实际强化仍由力量/覆甲/再生/生命上限各自结算。
///
/// 魂的种类**贴在生物本身**上（见 Attach），不依赖 SlotName——
/// 很多遭遇的 SlotName 是 null，拿它当键会导致写/读两边对不上、永远显示空。
/// 战斗内读档后附着状态会丢，这时按"房间四魂 + 敌人在列表中的位置"补算
/// （只在全场都活着时才补算，因为死掉的敌人会从列表移除、下标会漂）。
/// </summary>
[RegisterPower]
public class FourSoulsPower : ModPowerTemplate
{
    // 贴在生物上的四魂计数（下标 = SoulKind）
    private static readonly AttachedState<Creature, int[]> AttachedCounts = new(() => new int[Souls.Count]);

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override PowerAssetProfile AssetProfile => new(
        IconPath: "res://OtherworldTreasures/images/relics/Four_Souls_Fragment.png",
        BigIconPath: "res://OtherworldTreasures/images/relics/Four_Souls_Fragment.png");

    public override LocString Title => new LocString("powers", "OTHERWORLD_TREASURES_POWER_FOUR_SOULS.title");

    // 描述就一句话：此敌人持有哪些魂（不写数量）。
    // 没分到魂的敌人根本不会被挂上本能力（见 FourSoulsSystem.ApplyCombatStartBuffs）。
    public override LocString Description
    {
        get
        {
            var description = new LocString("powers", "OTHERWORLD_TREASURES_POWER_FOUR_SOULS.description");
            description.Add("Souls", CurrentSoulNames());
            return description;
        }
    }

    /// <summary>把该敌人分到的四魂贴到它身上（战斗开始时调用）。</summary>
    internal static void Attach(Creature target, int[] counts)
    {
        AttachedCounts[target] = counts;
        Entry.Logger.Info(
            $"[FourSouls] 敌人四魂：{target.Name} = {string.Join(",", counts)}");
    }

    /// <summary>给目标挂上本能力。</summary>
    internal static async Task ApplyAndSet(PlayerChoiceContext ctx, Creature target)
        => await PowerCmd.Apply<FourSoulsPower>(ctx, target, 1m, target, null, silent: true);

    /// <summary>当前持有哪几种魂，写成"荒魂, 和魂"这样；一种都没有时返回空串。</summary>
    private string CurrentSoulNames()
    {
        var counts = CurrentCounts();
        var names = new StringBuilder();
        for (int i = 0; i < counts.Length; i++)
        {
            if (counts[i] <= 0)
            {
                continue;
            }
            if (names.Length > 0)
            {
                names.Append(", ");
            }
            names.Append(Souls.Name((SoulKind)i));
        }
        return names.ToString();
    }

    private int[] CurrentCounts()
    {
        var owner = Owner;
        if (!IsMutable || owner == null)
        {
            return new int[Souls.Count];
        }
        if (AttachedCounts.TryGetValue(owner, out var attached))
        {
            return attached;
        }
        // 附着状态没了（战斗中读档）：按房间四魂补算
        return DeriveCounts(owner);
    }

    /// <summary>读档后的补算：房间四魂集合按敌人列表位置分发。</summary>
    private static int[] DeriveCounts(Creature owner)
    {
        var empty = new int[Souls.Count];
        var combatState = owner.CombatState;
        var runState = combatState?.RunState;
        var relic = runState == null ? null : FourSoulsSystem.FindRelic(runState);
        if (combatState == null || relic == null || !relic.Enabled)
        {
            return empty;
        }
        var enemies = combatState.Enemies;
        // 有敌人已死时，它在列表里的位置会让别人的下标漂移，宁可不显示也不显示错的
        foreach (var enemy in enemies)
        {
            if (!enemy.IsAlive)
            {
                return empty;
            }
        }
        int index = -1;
        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i] == owner)
            {
                index = i;
                break;
            }
        }
        if (index < 0)
        {
            return empty;
        }
        var souls = FourSoulsSystem.GetCurrentRoomSouls(runState);
        if (souls.Length == 0)
        {
            return empty;
        }
        var distribution = FourSoulsSystem.Distribute(souls, enemies.Count);
        return FourSoulsSystem.CountOf(distribution[index]);
    }
}
