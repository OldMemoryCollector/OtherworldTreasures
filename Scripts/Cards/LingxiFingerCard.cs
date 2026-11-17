using System.Collections.Generic;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Cards;

// 灵犀一指（来自古龙《陆小凤传奇》，陆小凤的独门武功）：2 费技能，打出后二选一。
//   进攻：造成 20 点伤害，无视格挡
//   防守：获得 1 层缓冲
// 图标暂用原版占位图。
[RegisterCard(typeof(TokenCardPool))]
public class LingxiFingerCard : ModCardTemplate
{
    private const string PlaceholderPortrait = "res://images/powers/missing_power.png";

    private const int energyCost = 2;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Token;
    private const bool shouldShowInCardLibrary = DevelopmentContent.Extras;

    // 进攻分支的伤害
    public const decimal StrikeDamage = 20m;

    // 防守分支的缓冲层数
    public const decimal GuardBuffer = 1m;

    public LingxiFingerCard() : base(energyCost, type, rarity, TargetType.AnyEnemy, shouldShowInCardLibrary)
    {
    }

    public override bool CanBeGeneratedInCombat => false;
    public override bool CanBeGeneratedByModifiers => false;
    public override CardAssetProfile AssetProfile => new(PortraitPath: PlaceholderPortrait);


    public override IEnumerable<CardKeyword> CanonicalKeywords => [MyKeywords.Choice];

    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(StrikeDamage, ValueProp.Unblockable | ValueProp.Move)
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var owner = Owner;
        var combatState = owner?.Creature?.CombatState;
        if (owner == null || combatState == null)
        {
            return;
        }

        var options = new List<CardModel>
        {
            combatState.CreateCard(ModelDb.Card<LingxiFingerStrike>(), owner),
            combatState.CreateCard(ModelDb.Card<LingxiFingerGuard>(), owner)
        };

        var chosen = await CardSelectCmd.FromChooseACardScreen(choiceContext, options, owner);
        if (chosen == null)
        {
            return;
        }

        if (chosen is LingxiFingerStrike)
        {
            if (cardPlay.Target == null)
            {
                return;
            }

            await DamageCmd.Attack(StrikeDamage)
                .FromCard(this, cardPlay)
                .Targeting(cardPlay.Target)
                .WithHitFx("vfx/vfx_attack_slash")
                .Execute(choiceContext);
            return;
        }

        if (chosen is LingxiFingerGuard)
        {
            await PowerCmd.Apply<BufferPower>(choiceContext, owner.Creature, GuardBuffer, owner.Creature, this);
        }
    }
}
