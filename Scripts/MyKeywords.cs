using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.Content;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Keywords;

namespace OtherworldTreasures.Scripts;

// 自定义卡牌关键词
// - 除外（类似消耗，但卡牌无法通过常规手段取回）
// - 抉择（从多个效果中选择一个）
// - 湮灭（回合结束时若仍在手牌中，则将其移除）
// - 回溯 / 加速（【时光布】的两个抉择方向，各自带说明）
// - 燃尽 / 引爆 / 清除（火焰人的三条术语）。这三条**不设 CardDescriptionPlacement**：
//   描述里已用 [gold]燃尽[/gold] 染色，卡牌 CanonicalKeywords 里挂上它们即可自动带出悬浮提示，
//   不需要在卡面再加一段说明文字块。
[RegisterOwnedCardKeyword(nameof(Exclude),
    CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.BeforeCardDescription)]
[RegisterOwnedCardKeyword(nameof(Choice),
    CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.BeforeCardDescription)]
[RegisterOwnedCardKeyword(nameof(Annihilate),
    CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.BeforeCardDescription)]
[RegisterOwnedCardKeyword(nameof(Rewind),
    CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.BeforeCardDescription)]
[RegisterOwnedCardKeyword(nameof(Accelerate),
    CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.BeforeCardDescription)]
[RegisterOwnedCardKeyword(nameof(Incinerate))]
[RegisterOwnedCardKeyword(nameof(Detonate))]
[RegisterOwnedCardKeyword(nameof(Cleanse))]
[RegisterOwnedCardKeyword(nameof(Remove), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.BeforeCardDescription)]
[RegisterOwnedCardKeyword(nameof(Knockback))]
[RegisterOwnedCardKeyword(nameof(Linkage))]
[RegisterOwnedCardKeyword(nameof(Combo))]
[RegisterOwnedCardKeyword(nameof(Poise))]
public class MyKeywords
{
    public static readonly CardKeyword Linkage = ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Linkage)).GetModCardKeyword();

    // 连招：打出后返回手牌并变为指定阶段，离开手牌时恢复初始形态
    public static readonly CardKeyword Combo = ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Combo)).GetModCardKeyword();
    public static readonly CardKeyword Remove =
        ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Remove)).GetModCardKeyword();
    public static readonly CardKeyword Exclude =
        ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Exclude)).GetModCardKeyword();

    public static readonly CardKeyword Choice =
        ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Choice)).GetModCardKeyword();

    public static readonly CardKeyword Annihilate =
        ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Annihilate)).GetModCardKeyword();

    public static readonly CardKeyword Rewind =
        ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Rewind)).GetModCardKeyword();

    public static readonly CardKeyword Accelerate =
        ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Accelerate)).GetModCardKeyword();

    // 燃尽：把手牌中的状态牌消耗掉
    public static readonly CardKeyword Incinerate =
        ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Incinerate)).GetModCardKeyword();

    // 引爆：清除目标身上的全部灼伤，并按层数造成额外伤害
    public static readonly CardKeyword Detonate =
        ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Detonate)).GetModCardKeyword();

    // 清除：移除灼伤，但不产生引爆伤害
    public static readonly CardKeyword Cleanse =
        ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Cleanse)).GetModCardKeyword();

    // 击退：被撞开的敌人撞向身后的敌人
    public static readonly CardKeyword Knockback =
        ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Knockback)).GetModCardKeyword();

    // 应势：通用触发机制。关键词只解释「满足条件时自动触发卡牌效果」，
    // 具体条件与效果由卡面正文自己写，避免关键词被写死成某一张卡的专属描述。
    public static readonly CardKeyword Poise =
        ModContentRegistry.GetQualifiedKeywordId(Entry.ModId, nameof(Poise)).GetModCardKeyword();
}
