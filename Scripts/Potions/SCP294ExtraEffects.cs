using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace OtherworldTreasures.Scripts.Potions;
public sealed partial class SCP294Potion
{
    private static readonly string[] OrbEntries = ["lightning", "frost", "dark", "plasma"];
    private bool _reviving;
    private static readonly MethodInfo NativeFoulUse = AccessTools.Method(typeof(FoulPotion), "OnUse");
    private static bool HasEffect(PotionModel potion, BrewEffect effect) => RecipeFor(potion).Ingredients.Any(i => i.Effect == effect);
    private static bool IsFairyOnly(PotionModel potion) => RecipeFor(potion).Ingredients.All(i => i.Effect == BrewEffect.FairyRevive);
    private static string VariableNumber(int value) => Scp294Settings.RainbowValues
        ? $"[rainbow sat=0.55 val=1.0]{value}[/rainbow]" : $"[blue]{value}[/blue]";

    private static bool CanUseOutsideCombat(PotionModel potion)
    {
        if (!potion.IsMutable || potion.Owner == null) return false;
        var ingredients = RecipeFor(potion).Ingredients;
        if (ingredients.Any(i => i.Effect == BrewEffect.FoulSplash))
            return potion.Owner.RunState?.CurrentRoom is { } room && FoulPotion.GetFoulPotionMerchantTarget(room).button != null;
        if (!ingredients.All(i => i.Effect is BrewEffect.Heal or BrewEffect.HealPercent or BrewEffect.MaxHp or
            BrewEffect.FairyRevive or BrewEffect.FillPotionSlots or BrewEffect.HealExtraTurn or BrewEffect.AmbergrisHeal)) return false;
        return true;
    }

    public override bool ShouldDie(Creature creature) => creature != Owner.Creature ||
        !RecipeFor(this).Ingredients.Any(i => i.Effect == BrewEffect.FairyRevive && i.Amount > 0);

    public override async Task AfterPreventingDeath(Creature creature)
    {
        if (creature != Owner.Creature || HasBeenRemovedFromState ||
            !RecipeFor(this).Ingredients.Any(i => i.Effect == BrewEffect.FairyRevive && i.Amount > 0)) return;
        _reviving = true;
        try { await OnUseWrapper(new ThrowingPlayerChoiceContext(), creature); }
        finally { _reviving = false; }
    }

    private async Task UseOutsideCombat(PlayerChoiceContext context, BrewRecipe recipe, Creature? target)
    {
        foreach (var ingredient in recipe.Ingredients)
        {
            switch (ingredient.Effect)
            {
                case BrewEffect.Damage:
                    await CreatureCmd.Damage(context,Owner.Creature,ingredient.Amount,ValueProp.Unblockable|ValueProp.Unpowered,null,null);break;
                case BrewEffect.Heal:
                    await CreatureCmd.Heal(Owner.Creature, ingredient.Amount); break;
                case BrewEffect.HealPercent:
                case BrewEffect.AmbergrisHeal:
                case BrewEffect.HealExtraTurn:
                    await CreatureCmd.Heal(Owner.Creature, Owner.Creature.MaxHp * ingredient.Amount / 100m); break;
                case BrewEffect.MaxHp:
                    await CreatureCmd.GainMaxHp(Owner.Creature, ingredient.Amount); break;
                case BrewEffect.FillPotionSlots:
                    await FillPotionSlots(Owner); break;
                case BrewEffect.FoulSplash:
                    var native = ModelDb.Potion<FoulPotion>().ToMutable();
                    native.Owner = Owner;
                    native.DynamicVars.Damage.BaseValue = ingredient.Amount;
                    native.DynamicVars.Gold.BaseValue = ingredient.SecondaryAmount ?? 100;
                    await (Task)NativeFoulUse.Invoke(native, [context, target])!;
                    break;
            }
        }
    }

    private static async Task FillPotionSlots(Player player)
    {
        int remaining = player.MaxPotionCount - player.Potions.Count();
        for (int i = 0; i < remaining && player.HasOpenPotionSlots; i++)
        {
            var potion = PotionFactory.CreateRandomPotionOutOfCombat(player, player.RunState.Rng.CombatPotionGeneration).ToMutable();
            if (!(await PotionCmd.TryToProcure(potion, player)).success) break;
        }
    }

    private static OrbModel OrbOf(string? entry) => entry switch
    {
        "lightning" => ModelDb.Orb<LightningOrb>(), "frost" => ModelDb.Orb<FrostOrb>(),
        "plasma" => ModelDb.Orb<PlasmaOrb>(), _ => ModelDb.Orb<DarkOrb>()
    };

    private async Task<bool> ApplyExtraEffect(PlayerChoiceContext context, BrewIngredient ingredient, Creature creature)
    {
        int amount = ingredient.Amount;
        switch (ingredient.Effect)
        {
            case BrewEffect.FairyRevive:
                // A held fairy is consumed by the death-prevention hook, not by an ordinary sip.
                return true;
            case BrewEffect.FillPotionSlots:
                if (creature.Player is { } fillPlayer) await FillPotionSlots(fillPlayer);
                return true;
            case BrewEffect.SneckoDraw:
                if (creature.Player is not { } sneckoPlayer) return true;
                if (amount > 0) await CardPileCmd.Draw(context, amount, sneckoPlayer);
                await Apply(context, new BrewIngredient(BrewEffect.RandomizeHandCosts, 1, BrewRecipient.Self), creature);
                return true;
            case BrewEffect.FoulSplash:
                if (creature.CombatState is { } foulCombat)
                    await CreatureCmd.Damage(context, foulCombat.Creatures.Where(c => !c.IsPet && c.IsAlive).ToArray(),
                        amount, ValueProp.Unpowered, Owner.Creature, null, null);
                return true;
            case BrewEffect.HealExtraTurn:
                await CreatureCmd.Heal(creature, creature.MaxHp * amount / 100m);
                if (CombatManager.Instance.IsInProgress)
                    await PowerCmd.Apply<AmbergrisPower>(context, creature, 1, Owner.Creature, null);
                return true;
            case BrewEffect.ExhaustHandDraw:
                if (creature.Player is not { } glowPlayer) return true;
                foreach (var card in PileType.Hand.GetPile(glowPlayer).Cards.ToArray()) await CardCmd.Exhaust(context, card);
                if (amount > 0) await CardPileCmd.Draw(context, amount, glowPlayer);
                return true;
            case BrewEffect.ChannelSameOrbs:
            case BrewEffect.ChannelRandomOrbs:
                if (creature.Player is not { } orbPlayer) return true;
                for (int i = 0; i < amount; i++)
                {
                    var orb = ingredient.Effect == BrewEffect.ChannelSameOrbs ? OrbOf(ingredient.OrbEntry)
                        : OrbModel.GetRandomOrb(orbPlayer.RunState.Rng.CombatOrbGeneration);
                    await OrbCmd.Channel(context, orb.ToMutable(), orbPlayer);
                }
                return true;
            default: return false;
        }
    }

    private static string OrbName(string? entry, string language) => (entry, language) switch
    {
        ("lightning", "eng") => "Lightning", ("frost", "eng") => "Frost", ("plasma", "eng") => "Plasma", (_, "eng") => "Dark",
        ("lightning", "jpn") => "ライトニング", ("frost", "jpn") => "フロスト", ("plasma", "jpn") => "プラズマ", (_, "jpn") => "ダーク",
        ("lightning", _) => "闪电", ("frost", _) => "冰霜", ("plasma", _) => "等离子", _ => "黑暗"
    };

    private static string? DescribeExtra(BrewIngredient ingredient, string language)
    {
        string n = VariableNumber(ingredient.Amount), gold = VariableNumber(ingredient.SecondaryAmount ?? 100);
        if (ingredient.Effect == BrewEffect.Doom && ingredient.Recipient is BrewRecipient.BothSides or BrewRecipient.SelfAndEnemy)
        {
            string own = VariableNumber(ingredient.SecondaryAmount ?? Math.Min(6, ingredient.Amount));
            bool all = ingredient.Recipient == BrewRecipient.BothSides;
            return language switch
            {
                "eng" => $"Apply {n} Doom to {(all ? "all enemies" : "the target enemy")}; apply {own} Doom to {(all ? "all allies" : "yourself")}.",
                "jpn" => $"{(all ? "敵全体" : "敵1体")}に破滅{n}、{(all ? "味方全体" : "自身")}に破滅{own}を付与する。",
                _ => $"对{(all ? "敌方全体" : "敌方目标")}各施加{n}层灾厄，对{(all ? "己方全体" : "自身")}各施加{own}层灾厄。"
            };
        }
        if (ingredient.Effect == BrewEffect.FairyRevive && ingredient.Amount <= 0)
            return language switch
            {
                "eng" => $"Lethal protection heals {n}% of Max HP (inactive).",
                "jpn" => $"致死ダメージ時の回復率{n}%（無効）。",
                _ => $"致命保护回复{n}%最大生命值（无效）。"
            };
        string orb = OrbName(ingredient.OrbEntry, language);
        return (ingredient.Effect, language) switch
        {
            (BrewEffect.FairyRevive, "eng") => $"On lethal damage, consume this potion and heal {n}% of Max HP.",
            (BrewEffect.FillPotionSlots, "eng") => "Fill all empty potion slots with random potions.",
            (BrewEffect.SneckoDraw, "eng") => $"Draw {n} cards. Randomize your hand's Energy costs this turn.",
            (BrewEffect.FoulSplash, "eng") => $"Deal {n} damage to all players and enemies. Outside combat, throw at a merchant to gain {gold} Gold.",
            (BrewEffect.HealExtraTurn, "eng") => $"Heal {n}% of Max HP. In combat, also take an extra turn.",
            (BrewEffect.ExhaustHandDraw, "eng") => $"Exhaust your hand, then draw {n} cards.",
            (BrewEffect.ChannelSameOrbs, "eng") => $"Channel {n} {orb} Orbs.",
            (BrewEffect.ChannelRandomOrbs, "eng") => $"Channel {n} random Orbs; each type is rolled separately.",
            (BrewEffect.FairyRevive, "jpn") => $"致死ダメージ時、この薬を消費し最大HPの{n}%を回復する。",
            (BrewEffect.FillPotionSlots, "jpn") => "空いているすべてのポーションスロットにランダムな薬を得る。",
            (BrewEffect.SneckoDraw, "jpn") => $"カードを{n}枚引く。このターン、手札のエナジーコストをランダム化する。",
            (BrewEffect.FoulSplash, "jpn") => $"全プレイヤーと敵に{n}ダメージ。戦闘外では商人に投げて{gold}ゴールドを得る。",
            (BrewEffect.HealExtraTurn, "jpn") => $"最大HPの{n}%を回復する。戦闘中なら追加のターンを1回得る。",
            (BrewEffect.ExhaustHandDraw, "jpn") => $"手札をすべて廃棄し、カードを{n}枚引く。",
            (BrewEffect.ChannelSameOrbs, "jpn") => $"{orb}オーブを{n}個生成する。",
            (BrewEffect.ChannelRandomOrbs, "jpn") => $"ランダムなオーブを{n}個生成する。種類は1個ずつ抽選する。",
            (BrewEffect.FairyRevive, _) => $"受到致命伤害时，消耗此药水并回复最大生命值的{n}%。",
            (BrewEffect.FillPotionSlots, _) => "在所有空药水栏位中获得随机药水。",
            (BrewEffect.SneckoDraw, _) => $"抽{n}张牌，本回合随机化手牌耗能。",
            (BrewEffect.FoulSplash, _) => $"对所有玩家和敌人造成{n}点伤害；战斗外可投掷给商人换取{gold}金币。",
            (BrewEffect.HealExtraTurn, _) => $"回复最大生命值的{n}%；在战斗中使用时额外进行1个回合。",
            (BrewEffect.ExhaustHandDraw, _) => $"消耗你的手牌，然后抽{n}张牌。",
            (BrewEffect.ChannelSameOrbs, _) => $"生成{n}个{orb}充能球。",
            (BrewEffect.ChannelRandomOrbs, _) => $"生成{n}个随机充能球，每个球的类型单独随机。",
            _ => null
        };
    }

    private static string ExtraEffectName(BrewEffect effect)
    {
        string language = MegaCrit.Sts2.Core.Localization.LocManager.Instance.Language;
        return (effect, language) switch
        {
            (BrewEffect.FairyRevive, "eng") => "Fairy", (BrewEffect.FillPotionSlots, "eng") => "Entropic",
            (BrewEffect.SneckoDraw, "eng") => "Snecko", (BrewEffect.FoulSplash, "eng") => "Foul",
            (BrewEffect.HealExtraTurn, "eng") => "Ambergris", (BrewEffect.ExhaustHandDraw, "eng") => "Glowwater",
            (BrewEffect.ChannelSameOrbs, "eng") => "Orb", (BrewEffect.ChannelRandomOrbs, "eng") => "Chaos Orb",
            (BrewEffect.FairyRevive, "jpn") => "妖精", (BrewEffect.FillPotionSlots, "jpn") => "混沌",
            (BrewEffect.SneckoDraw, "jpn") => "スネッコ", (BrewEffect.FoulSplash, "jpn") => "汚濁",
            (BrewEffect.HealExtraTurn, "jpn") => "龍涎香", (BrewEffect.ExhaustHandDraw, "jpn") => "発光水",
            (BrewEffect.ChannelSameOrbs, "jpn") => "単色オーブ", (BrewEffect.ChannelRandomOrbs, "jpn") => "ランダムオーブ",
            _ => ExtraEffectGlyph(effect)
        };
    }
    private static string ExtraEffectGlyph(BrewEffect effect) => effect switch
    {
        BrewEffect.FairyRevive => "仙灵", BrewEffect.FillPotionSlots => "混沌", BrewEffect.SneckoDraw => "异蛇",
        BrewEffect.FoulSplash => "污浊", BrewEffect.HealExtraTurn => "龙涎", BrewEffect.ExhaustHandDraw => "涌动",
        BrewEffect.ChannelSameOrbs => "同球", BrewEffect.ChannelRandomOrbs => "乱球", _ => "能量"
    };

    private static readonly Dictionary<string, Regex> GoldPatterns = new()
    {
        ["zhs"] = new Regex(@"生命比例恢复|生命恢复|生命回复|最大生命值|最大生命|百分比回复|伤害吸收|人工制品|黑暗充能球|充能球栏位|充能球|抽牌堆|弃牌堆|小刀\+|无色牌|手牌|消耗(?!能量)|保留|覆甲|格挡|力量|敏捷|虚弱|易伤|脆弱|中毒|灾厄|荆棘|再生|缓冲|无实体|集中|仪式|星星|金币|商人|闪电|冰霜|等离子|治疗|回血|保命|生命|伤害|抽牌|能量|(?<!消)耗能|打击|灵魂|升级|生成|弃置|复制|缩小|亡灭|衰亡|巨大化|削弱|迟钝|额外回合|清晰|光辉|清明|辉光|攻击牌|技能牌|能力牌", RegexOptions.CultureInvariant),
        ["eng"] = new Regex(@"\b(Max HP|healing|Heal|HP|damage|Energy|Upgrade|Channel|Soul|Strike|Shrinking|Absorption|Artifact|Dark Orbs?|Orbs?|draw pile|discard pile|draw|Shivs?|Colorless|hand|Exhaust|Retain|Plating|Block|Strength|Dexterity|Weak|Vulnerable|Frail|Poison|Doom|Thorns|Regen|Buffer|Intangible|Focus|Ritual|Stars?|Gold|merchant|Lightning|Frost|Plasma)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        ["jpn"] = new Regex("最大HP|回復|ダメージ吸収|ダメージ|エナジー|ドロー|アップグレード|生成|ソウル|アーティファクト|オーブ|山札|捨て札|ナイフ|無色|手札|廃棄|保持|プレート|ブロック|筋力|敏捷|脱力|弱体|脆弱|毒|破滅|トゲ|再生|バッファー|霊体|集中力|儀式|スター|ゴールド|商人|ライトニング|フロスト|プラズマ|ダーク", RegexOptions.CultureInvariant)
    };
    internal static string GoldKeywords(string text, string language) =>
        GoldPatterns.GetValueOrDefault(language, GoldPatterns["zhs"]).Replace(text, match => $"[gold]{match.Value}[/gold]");
    private static readonly Regex FixedNumbers = new(
        @"\[(rainbow|blue|gold|red|green|purple|orange|aqua|color)(?:[^\]]*)\].*?\[/\1\]|\[[^\]]+\]|(?<number>\d+(?:\.\d+)?%?)",
        RegexOptions.Singleline | RegexOptions.CultureInvariant);

    internal static string FormatPotionText(string text, string language)
    {
        // Skip existing formatting, especially variable values in rainbow tags.
        string numbers = FixedNumbers.Replace(text, match => match.Groups["number"].Success
            ? $"[blue]{match.Value}[/blue]" : match.Value);
        return GoldKeywords(numbers, language);
    }

}
