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
public class MyKeywords
{
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
}
