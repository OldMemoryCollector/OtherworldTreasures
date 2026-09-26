using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.RelicPools;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using OtherworldTreasures.Scripts.Cards;

namespace OtherworldTreasures.Scripts.Relics;

// 惊奇套牌：战斗开始时，将5张"惊奇卡牌"洗入你的牌库。
// 注册到共享遗物池（任何角色都可能遇到先古之民获得此遗物）
[RegisterRelic(typeof(SharedRelicPool))]
public class DeckOfWonders : ModRelicTemplate, ICombatCompactRelic
{
    // 战斗中只隐藏"惊奇卡牌"的额外悬浮提示，遗物主描述本身已足够简洁，保持不变
    bool ICombatCompactRelic.UseCompactDescriptionInCombat => false;

    // 由先古之民给予，使用 Ancient 稀有度
    public override RelicRarity Rarity => RelicRarity.Ancient;

    // 遗物图片资源（IconPath=小图标85x85，IconOutlinePath=轮廓85x85，BigIconPath=大图标256x256）
    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://OtherworldTreasures/images/relics/Deck_of_Wonders.png",
        IconOutlinePath: "res://OtherworldTreasures/images/relics/Deck_of_Wonders.png",
        BigIconPath: "res://OtherworldTreasures/images/relics/Deck_of_Wonders.png"
    );

    // 悬浮提示：预览【惊奇卡牌】（与原版"寻龙尺→探寻"一致，走 ExtraHoverTips）
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => base.AdditionalHoverTips.Concat(HoverTipFactory.FromCardWithCardHoverTips<ScrollOfWonder>());

    // 战斗开始时，将5张惊奇卡牌洗入抽牌堆
    public override async Task BeforeCombatStart()
    {
        var player = Owner;
        // 遗物闪光（顶栏图标闪光 + relic_activate_general 音效）
        Flash();
        // 获取当前战斗的 CombatState（DebugOnlyGetState 是公开访问器，命名虽带 DebugOnly 但是 RitsuLib 推荐用法）
        var combatState = CombatManager.Instance.DebugOnlyGetState();

        // 创建 5 张惊奇卡牌，以随机位置加入抽牌堆（随机位置=洗入效果）
        for (int i = 0; i < 5; i++)
        {
            var card = combatState.CreateCard<ScrollOfWonder>(player);
            await CardPileCmd.AddGeneratedCardToCombat(
                card,
                PileType.Draw,
                player,
                CardPilePosition.Random);
        }
    }
}
