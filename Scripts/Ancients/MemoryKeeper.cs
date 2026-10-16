using System.Collections;
using Godot;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using OtherworldTreasures.Scripts.Relics;

namespace OtherworldTreasures.Scripts.Ancients;

// 旧忆收藏家：先古之民，在第二层出现。
// 原版章节与 Index 对照：Overgrowth = 0（第一层）、Underdocks = 0（第一层的另一变体）、Hive = 1（第二层）、Glory = 2（第三层）。
// 注意 Overgrowth 不是第二层，注册到它会让先古之民出现在第一层。
[RegisterActAncient(typeof(Hive))]
public class MemoryKeeper : ModAncientEventTemplate
{
    // 选项按钮颜色（紫色调，呼应"旧忆"主题）
    public override Color ButtonColor => new(0.45f, 0.30f, 0.65f, 0.5f);

    // 对话框颜色
    public override Color DialogueColor => new(0.45f, 0.30f, 0.65f);

    // 背景场景：旧忆收藏家专属背景（神秘之手场景图）
    public override string CustomBackgroundScenePath => "res://OtherworldTreasures/scenes/events/background_scenes/memory_keeper.tscn";

    // 地图图标（278x278）与通关回顾头像（88x88）：旧忆收藏家专属
    public override AncientEventPresentationAssetProfile AncientPresentationAssetProfile => new(
        MapIconPath: "res://OtherworldTreasures/images/ancients/memory_keeper_map.png",
        MapIconOutlinePath: "res://OtherworldTreasures/images/ancients/memory_keeper_map.png",
        RunHistoryIconPath: "res://OtherworldTreasures/images/ancients/memory_keeper_head.png",
        RunHistoryIconOutlinePath: "res://OtherworldTreasures/images/ancients/memory_keeper_head.png"
    );

    // 七件遗物；泰拉瑞亚三饰品由向导事件赠送，不进入先古之民选项池。
    // 哆啦A梦道具（任意门/缩小灯/竹蜻蜓/如果电话亭/时光布/空气炮/桃太郎丸子）是【四次元口袋】的附属遗物，不进这里。
    // 四魂之玉只在本选项池出现；点击后实际获得的是随机的【四魂之玉碎片】（碎片本身不进选项池）。
    private IReadOnlyList<EventOption> RelicPool => [
        CreateModRelicOption<DeckOfWonders>(),
        CreateModRelicOption<KurasDice>(),
        CreateModRelicOption<AshenFlask>(),
        CreateModRelicOption<FourDimensionalPocket>(),
        CreateModRelicOption<DavesSeeds>(),
        CreateModRelicOption<FourSoulsGem>(),
        CreateModRelicOption<Omnitrix>()
    ];

    public override IEnumerable<EventOption> AllPossibleOptions => RelicPool;

    // 生成初始选项：洗牌后随机取 4 个
    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        var owned = Owner?.Relics.Select(r => r.GetType()).ToHashSet() ?? new HashSet<Type>();
        var available = RelicPool
            .Where(o => o.Relic == null || !owned.Contains(o.Relic.GetType()))
            .ToList();

        // 兜底：全部都持有过时退回完整池，避免给出空选项
        if (available.Count == 0)
        {
            available = RelicPool.ToList();
        }

        return available.UnstableShuffle(Rng).Take(4).ToList();
    }

}
