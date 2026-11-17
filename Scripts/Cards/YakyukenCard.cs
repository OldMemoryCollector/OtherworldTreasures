using System.Collections.Generic;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.ValueProps;
using OtherworldTreasures.Scripts.Events;
using STS2RitsuLib.Cards.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Cards;

// 野球拳（来自《金庸群侠传》）：1 费攻击，越用越强的拳法。
// LV1～LV9 是同一张牌的不断升级：每级伤害 +1，升到下一级要打的拳数从 6 起，每级 +2（6、8、10……）。
// 打满拳数就把这张牌升一级（走原版的升级机制，锻造和临时升级也能提级），LV9 封顶。
// LV9 之后再打满一轮拳数，这张牌离场，换成独立的【野球拳lv10】。
// 出拳计数走 YakyukenProgress 的局内数据，跨战斗保留。
// 图标暂用原版占位图。
[RegisterCard(typeof(TokenCardPool))]
public class YakyukenCard : ModCardTemplate
{
    private const string PlaceholderPortrait = "res://images/powers/missing_power.png";

    private const int energyCost = 1;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Token;
    private const bool shouldShowInCardLibrary = DevelopmentContent.Extras;

    // LV1 的伤害，每升一级 +1（在 OnUpgrade 里加）
    public const decimal BaseDamage = 6m;

    // LV1 加 8 次升级到 LV9；第 10 级是另一张卡
    public const int MaxLevel = 9;

    public YakyukenCard() : base(energyCost, type, rarity, TargetType.AnyEnemy, shouldShowInCardLibrary)
    {
    }

    public override bool CanBeGeneratedInCombat => false;
    public override bool CanBeGeneratedByModifiers => false;
    public override CardAssetProfile AssetProfile => new(PortraitPath: PlaceholderPortrait);

    // 规范卡（图鉴、预览）没有 Owner，读进度会抛，直接按 LV1 算
    private Player? OwnerOrNull => IsMutable ? Owner : null;

    // 等级就是升级等级 + 1：LV1 是没升级的那张
    public int Level => CurrentUpgradeLevel + 1;

    // 这一级还差几拳升级
    public int PunchesLeft => YakyukenProgress.Needed(Level) - YakyukenProgress.Punches(OwnerOrNull, this);

    // 最多升到 LV9
    public override int MaxUpgradeLevel => MaxLevel - 1;

    // 卡名跟着等级走：野球拳lv1、野球拳lv2……不叠原版的「+」后缀
    public override string Title => $"{TitleLocString.GetFormattedText()}lv{Level}";

    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(BaseDamage, ValueProp.Move),
        ModCardVars.Computed("PunchesLeft", YakyukenProgress.BasePunchRequirement,
            (card, _) => card is YakyukenCard yakyuken ? yakyuken.PunchesLeft : YakyukenProgress.BasePunchRequirement)
    ];

    // 每升一级伤害 +1
    protected override void OnUpgrade()
    {
        DynamicVars["Damage"].UpgradeValueBy(1m);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var target = cardPlay.Target;
        if (target == null)
        {
            return;
        }

        await DamageCmd.Attack(DynamicVars["Damage"].BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(target)
            .WithHitFx("vfx/vfx_attack_blunt")
            .Execute(choiceContext);

        var owner = OwnerOrNull;
        if (owner == null)
        {
            return;
        }

        var levelUp = YakyukenProgress.AddPunch(owner, this, YakyukenProgress.Needed(Level));
        if (!levelUp)
        {
            RefreshVisuals();
            return;
        }

        if (Level < MaxLevel)
        {
            CardCmd.Upgrade(this);
            if (DeckVersion is YakyukenCard deckCard && deckCard != this)
            {
                CardCmd.Upgrade(deckCard);
            }

            Entry.Logger.Info($"[YakyukenCard] 升到 LV{Level}");
            RefreshVisuals();
            return;
        }

        // 已经 LV9，又打满一轮：换成独立的【野球拳lv10】，牌组里那张本体也一起换
        var combatState = CombatState;
        if (combatState == null)
        {
            return;
        }

        YakyukenProgress.Reset(owner, this);

        if (DeckVersion is YakyukenCard deckOriginal && deckOriginal != this)
        {
            var deckReplacement = owner.RunState.CreateCard<YakyukenLv10Card>(owner);
            await CardCmd.Transform(deckOriginal, deckReplacement);
        }

        var lv10Card = combatState.CreateCard<YakyukenLv10Card>(owner);
        await CardPileCmd.RemoveFromCombat(this);
        await CardPileCmd.Add(lv10Card, PileType.Discard);
        Entry.Logger.Info("[YakyukenCard] 修炼到顶，换成【野球拳lv10】");
    }

    // 拳数变了，卡面上的「还需打出X次」跟着刷
    private void RefreshVisuals()
    {
        try
        {
            NCard.FindOnTable(this)?.UpdateVisuals(PileType.Hand, CardPreviewMode.None);
        }
        catch (System.Exception ex)
        {
            Entry.Logger.Warn($"[YakyukenCard] 刷新卡面失败：{ex.Message}");
        }
    }
}
