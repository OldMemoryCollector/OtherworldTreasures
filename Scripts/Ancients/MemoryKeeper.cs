using System.Collections;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Acts;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using OtherworldTreasures.Scripts.Potions;
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

    // 池子：八件"先古遗物"。
    // 哆啦A梦道具（任意门/缩小灯/竹蜻蜓/如果电话亭/时光布/空气炮/桃太郎丸子）是【四次元口袋】的附属遗物，不进这里。
    private IReadOnlyList<EventOption> RelicPool => [
        CreateModRelicOption<DeckOfWonders>(),
        CreateModRelicOption<KurasDice>(),
        CreateModRelicOption<AshenFlask>(),
        CreateModRelicOption<FourDimensionalPocket>(),
        CreateModRelicOption<DavesSeeds>(),
        CreateModRelicOption<MythicCharm>(),
        CreateModRelicOption<SporeSac>(),
        CreateModRelicOption<CrossNecklace>()
    ];

    // 所有可能的选项（含先古之民专属药水；图鉴等使用）
    // 附魔金苹果被暂时隐藏时不列出它，先古图鉴里也看不到
    public override IEnumerable<EventOption> AllPossibleOptions =>
        EnchantedGoldenAppleOption is { } potionOption ? RelicPool.Append(potionOption) : RelicPool;

    // 生成初始选项：与原版一致 —— 洗牌后随机取 3 个
    // 未隐藏时，先古之民专属药水（附魔金苹果）与遗物一起参与抽取
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

        if (EnchantedGoldenAppleOption is { } potionOption)
        {
            available.Add(potionOption);
        }

        return available.UnstableShuffle(Rng).Take(3).ToList();
    }

    // 先古之民专属药水（附魔金苹果）选项；它被暂时隐藏时返回 null
    private EventOption? EnchantedGoldenAppleOption =>
        HiddenPotions.GoldenApplesHidden ? null : BuildEnchantedGoldenAppleOption();

    // 构造「附魔金苹果」选项：选中后直接获得该药水
    private EventOption BuildEnchantedGoldenAppleOption()
    {
        var potion = ModelDb.Potion<EnchantedGoldenApple>().ToMutable();
        return new EventOption(
            this,
            async () =>
            {
                if (Owner != null)
                {
                    await PotionCmd.TryToProcure(potion, Owner);
                }
            },
            new LocString("potions", "OTHERWORLD_TREASURES_POTION_ENCHANTED_GOLDEN_APPLE.title"),
            new LocString("potions", "OTHERWORLD_TREASURES_POTION_ENCHANTED_GOLDEN_APPLE.description"),
            "OTHERWORLD_TREASURES_ENCHANTED_GOLDEN_APPLE",
            Array.Empty<IHoverTip>());
    }
}
