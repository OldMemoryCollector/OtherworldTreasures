using MegaCrit.Sts2.Core.Entities.Relics;

namespace OtherworldTreasures.Scripts.Relics;

// 自定义遗物稀有度：「哆啦A梦」
// 原版 RelicRarity 枚举无法在运行时扩展，这里使用枚举的未定义值（C# 允许枚举持有未定义值，
// 用 == 比较完全正常）。
// 稀有度显示文本来自 gameplay_ui.RELIC_RARITY.<稀有度名>，
// 而 (RelicRarity)99 的 ToString() 就是 "99"，所以本地化里加一条 "RELIC_RARITY.99" 即可显示成中文。
public static class ModRelicRarity
{
    public static readonly RelicRarity Doraemon = (RelicRarity)99;
}
