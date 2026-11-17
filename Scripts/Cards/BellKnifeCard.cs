using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.ValueProps;
using OtherworldTreasures.Scripts.Alien;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Cards;

// 贝尔的日常小刀（来自《惊悚乐园》）：【贝尔的日常小刀】遗物每场战斗塞进手牌的临时卡。
// 1 费攻击，保留。造成伤害并给予灼伤；若此牌的直接攻击击杀敌人，永久获得 1 张【鸡肉味野味】。
// 图标暂用原版占位图。
[RegisterCard(typeof(TokenCardPool))]
public class BellKnifeCard : ModCardTemplate
{
    private const string PlaceholderPortrait = "res://images/powers/missing_power.png";

    private const int energyCost = 1;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Token;
    private const bool shouldShowInCardLibrary = DevelopmentContent.Extras;

    private const decimal DamageAmount = 8m;
    private const decimal ScorchStacks = 4m;

    public BellKnifeCard() : base(energyCost, type, rarity, TargetType.AnyEnemy, shouldShowInCardLibrary)
    {
    }

    public override bool CanBeGeneratedInCombat => false;
    public override bool CanBeGeneratedByModifiers => false;
    public override CardAssetProfile AssetProfile => new(PortraitPath: PlaceholderPortrait);

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain];

    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(DamageAmount, ValueProp.Move)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var target = cardPlay.Target;
        if (target == null)
        {
            return;
        }

        // Snapshot native fatal eligibility before the attack resolves death hooks.
        bool shouldRewardKill = target.Powers.All(power => power.ShouldOwnerDeathTriggerFatal());

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(choiceContext);

        if (target.IsAlive)
        {
            await AlienScorch.Apply(choiceContext, target, (int)ScorchStacks, this);
            return;
        }

        // 直接攻击把目标打死了，永久塞一张野味进牌组
        var player = Owner;
        if (player == null || !shouldRewardKill)
        {
            return;
        }

        var wildGame = player.RunState.CreateCard(ModelDb.Card<ChickenWildGameCard>(), player);
        await CardPileCmd.Add(wildGame, PileType.Deck, CardPilePosition.Bottom, this);
        Entry.Logger.Info("[BellKnifeCard] 击杀获得鸡肉味野味");
    }
}
