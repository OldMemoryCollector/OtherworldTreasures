using System.Collections.Generic;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;
using OtherworldTreasures.Scripts.Enchantments;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Cards;

// 熊孩子的下鞭腿（来自《惊悚乐园》）：1 费技能。
// 目标是「有腿的敌人」：绊倒它，取消它的下一次行动，随后它获得力量。
// 目前暂用人形标签代替「有腿」的判定。
// 全局使用 3 次后从牌组中永久移除。
// 图标暂用原版占位图。
[RegisterCard(typeof(TokenCardPool))]
public class BratSweepKickCard : ModCardTemplate
{
    private const string PlaceholderPortrait = "res://images/powers/missing_power.png";

    private const int energyCost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Token;
    private const bool shouldShowInCardLibrary = DevelopmentContent.Extras;

    // 绊倒之后给敌人的力量
    private const decimal StrengthGiven = 2m;

    // 全局可用次数
    public const int MaxUses = 3;

    private int _uses;

    [SavedProperty]
    public int Uses
    {
        get => _uses;
        set
        {
            AssertMutable();
            _uses = value;
        }
    }

    public BratSweepKickCard() : base(energyCost, type, rarity, TargetType.AnyEnemy, shouldShowInCardLibrary)
    {
    }

    public override bool CanBeGeneratedInCombat => false;
    public override bool CanBeGeneratedByModifiers => false;
    public override CardAssetProfile AssetProfile => new(PortraitPath: PlaceholderPortrait);

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var target = cardPlay.Target;
        if (target == null || target.Side != MegaCrit.Sts2.Core.Combat.CombatSide.Enemy)
        {
            return;
        }

        if (!MonsterTypeTags.For(target).Contains(MonsterTypeTag.Humanoid))
        {
            return;
        }

        await CreatureCmd.Stun(target);
        await PowerCmd.Apply<StrengthPower>(choiceContext, target, StrengthGiven, Owner?.Creature, this);

        Uses++;
        Entry.Logger.Info($"[BratSweepKickCard] 使用 {Uses}/{MaxUses}");

        if (Uses >= MaxUses)
        {
            await CardPileCmd.RemoveFromDeck(this);
        }
    }
}
