using MegaCrit.Sts2.Core.Entities.Potions;

namespace OtherworldTreasures.Scripts.Potions;

// 自定义药水稀有度：「极稀有」
// 与遗物那边同理：原版 PotionRarity 枚举不能在运行时扩展，这里使用枚举的未定义值。
// 注意两处原版逻辑：
// 1) 随机抽药只滚 Common/Uncommon/Rare，所以自定义稀有度天然不会进随机池
// 2) PotionRarityExtensions.ToLocString 的 default 分支会抛异常 —— 已在 GamePatches 里拦掉
public static class ModPotionRarity
{
    public static readonly PotionRarity VeryRare = (PotionRarity)99;
}
