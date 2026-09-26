using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Relics;

// 四次元口袋：接下来 5 次从精英战斗获得的遗物，改为来自「哆啦A梦道具」池。
// 道具池 = 所有实现了 IDoraemonItem 的遗物（可扩展：新增道具自动入池）。
[RegisterRelic(typeof(SharedRelicPool))]
public class FourDimensionalPocket : ModRelicTemplate
{
    private const int initialCharges = 5;

    // 跨战斗实例持久化：遗物在战斗中会被克隆，普通实例字段会丢失
    private static int s_remaining = initialCharges;

    public override RelicRarity Rarity => RelicRarity.Ancient;

    protected override string IconBaseName => "four_dimensional_pocket";

    protected override string BigIconPath => "res://OtherworldTreasures/images/relics/Four_Dimensional_Pocket.png";

    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://OtherworldTreasures/images/relics/Four_Dimensional_Pocket.png",
        IconOutlinePath: "res://OtherworldTreasures/images/relics/Four_Dimensional_Pocket.png",
        BigIconPath: "res://OtherworldTreasures/images/relics/Four_Dimensional_Pocket.png"
    );

    // 剩余次数角标
    public override bool ShowCounter => s_remaining > 0;

    public override int DisplayAmount => s_remaining;

    // 悬浮提示：预览道具池中的 5 件哆啦A梦道具（只取各自的遗物说明，不嵌套子提示）
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => base.AdditionalHoverTips.Concat(new IHoverTip[]
    {
        ModelDb.Relic<AnywhereDoor>().HoverTip,
        ModelDb.Relic<ShrinkRay>().HoverTip,
        ModelDb.Relic<BambooCopter>().HoverTip,
        ModelDb.Relic<WhatIfPhoneBooth>().HoverTip,
        ModelDb.Relic<TimeCloth>().HoverTip
    });

    // 每局新 run 重新获得遗物时重置 static 状态（避免继承旧档的剩余次数）
    public override async Task AfterObtained()
    {
        s_remaining = initialCharges;
        Status = RelicStatus.Normal;
        InvokeDisplayAmountChanged();
        Entry.Logger.Info($"[FourDimensionalPocket] AfterObtained: charges reset to {s_remaining}");
        await base.AfterObtained();
    }

    // 精英战斗的遗物奖励 → 替换为哆啦A梦道具
    public override bool TryModifyRewards(Player player, List<Reward> rewards, AbstractRoom? room)
    {
        if (player != Owner)
        {
            return false;
        }
        if (room == null || room.RoomType != RoomType.Elite)
        {
            return false;
        }
        if (s_remaining <= 0)
        {
            return false;
        }

        var item = PickRandomItem(player);
        if (item == null)
        {
            Entry.Logger.Info("[FourDimensionalPocket] No available Doraemon item, reward left unchanged");
            return false;
        }

        var replacement = new RelicReward(item, player);
        int index = rewards.FindIndex(r => r is RelicReward);
        if (index >= 0)
        {
            rewards[index] = replacement;
        }
        else
        {
            rewards.Add(replacement);
        }

        s_remaining--;
        InvokeDisplayAmountChanged();
        if (s_remaining <= 0)
        {
            Status = RelicStatus.Disabled;
        }
        Entry.Logger.Info($"[FourDimensionalPocket] Elite relic replaced by {item.GetType().Name}, remaining={s_remaining}");
        return true;
    }

    // 道具池：所有实现 IDoraemonItem 的遗物；优先给尚未持有的，避免重复
    private static RelicModel? PickRandomItem(Player player)
    {
        var all = ModelDb.AllRelics.Where(r => r is IDoraemonItem).ToList();
        if (all.Count == 0)
        {
            return null;
        }

        var ownedTypes = player.Relics.Select(r => r.GetType()).ToHashSet();
        var pool = all.Where(r => !ownedTypes.Contains(r.GetType())).ToList();
        if (pool.Count == 0)
        {
            pool = all;
        }

        return pool[System.Random.Shared.Next(pool.Count)].ToMutable();
    }
}
