using MegaCrit.Sts2.Core.Localization;
using OtherworldTreasures.Scripts.Events;
namespace OtherworldTreasures.Scripts.Potions;
public sealed partial class SCP294Potion
{
    internal static string ChoiceDetails(string page, int value, Taste? taste, Feeling? feeling)
    {
        string language = LocManager.Instance.Language;
        string Text(string zh, string en, string jp) => language switch { "eng" => en, "jpn" => jp, _ => zh };
        string result;
        if (page == "TASTE")
        {
            result = (Taste)value switch
            {
                Taste.Sweet => Text("侧重治疗、生命恢复、再生与保命。", "Favors healing, Regen and survival.", "回復・再生・生存効果を優先。"),
                Taste.Bitter => Text("侧重虚弱与易伤。", "Favors Weak and Vulnerable.", "脱力と弱体を優先。"),
                Taste.Sour => Text("侧重中毒、伤害与虚弱。", "Favors Poison, damage and Weak.", "毒・ダメージ・脱力を優先。"),
                Taste.Spicy => Text("侧重伤害、力量、易伤与荆棘。", "Favors damage, Strength, Vulnerable and Thorns.", "ダメージ・筋力・弱体・トゲを優先。"),
                Taste.Salty => Text("侧重格挡、力量、敏捷、缓冲与覆甲。", "Favors Block, Strength, Dexterity, Buffer and Plating.", "ブロック・筋力・敏捷・バッファー・プレートを優先。"),
                Taste.Cool => Text("侧重抽牌、能量、手牌操作与充能球。", "Favors draw, Energy, hand effects and Orbs.", "ドロー・エナジー・手札操作・オーブを優先。"),
                Taste.Numbing => Text("侧重虚弱、脆弱与中毒。", "Favors Weak, Frail and Poison.", "脱力・脆弱・毒を優先。"),
                Taste.Astringent => Text("侧重虚弱、易伤与伤害。", "Favors Weak, Vulnerable and damage.", "脱力・弱体・ダメージを優先。"),
                Taste.Savory => Text("侧重治疗、力量、格挡与人工制品。", "Favors healing, Strength, Block and Artifact.", "回復・筋力・ブロック・アーティファクトを優先。"),
                _ => Text("各种药效皆有可能，更易混入多种效果。", "Any effect is possible; tends to mix more effects.", "全効果が候補。より多くの効果が混ざりやすい。")
            };
        }
        else if (page == "FEELING")
        {
            var selected = (Feeling)value;
            string direction = PolarityOf(selected) switch
            {
                BrewPolarity.Positive => Text("偏向正面效果", "Favors positive effects", "良い効果を優先"),
                BrewPolarity.Negative => Text("偏向负面效果", "Favors negative effects", "悪い効果を優先"),
                _ => Text("正负效果随机", "Positive and negative effects", "良し悪しはランダム")
            };
            string recipient = RecipientLabel(RecipientPreferenceOf(selected), language);
            result = Text($"{direction}，目标倾向{recipient}。", $"{direction}; favors {recipient}.", $"{direction}。対象は{recipient}を優先。");
        }
        else
        {
            int density = Math.Clamp(value, 0, 5);
            if (Scp294Settings.CrazyMode)
                result = Text("强度随机为原版的0～10倍；浓度影响强弱倾向。", "Potency: 0–10× vanilla; concentration biases the roll.", "強度は原作の0～10倍。濃度で強弱の傾向が変わる。");
            else
            {
                string Range(BrewEffect effect)
                {
                    var range = ConcentrationRange(effect, density);
                    return $"{VariableNumber(range.Min)}～{VariableNumber(range.Max)}";
                }
                result = Text($"示例：治疗{Range(BrewEffect.Heal)}；中毒{Range(BrewEffect.Poison)}；伤害{Range(BrewEffect.Damage)}。",
                    $"Examples: Heal {Range(BrewEffect.Heal)}, Poison {Range(BrewEffect.Poison)}, damage {Range(BrewEffect.Damage)}.",
                    $"例：回復{Range(BrewEffect.Heal)}・毒{Range(BrewEffect.Poison)}・ダメージ{Range(BrewEffect.Damage)}。");
            }
        }
        return FormatPotionText(result, language);
    }

    private static string RecipientLabel(BrewRecipient recipient, string language) => (recipient, language) switch
    {
        (BrewRecipient.SelectedEnemy, "eng") => "one enemy", (BrewRecipient.AllEnemies, "eng") => "all enemies",
        (BrewRecipient.AllAllies, "eng") => "all allies", (BrewRecipient.BothSides, "eng") => "both sides", (BrewRecipient.SelfAndEnemy, "eng") => "you and one enemy", (_, "eng") => "yourself",
        (BrewRecipient.SelectedEnemy, "jpn") => "敵1体", (BrewRecipient.AllEnemies, "jpn") => "敵全体",
        (BrewRecipient.AllAllies, "jpn") => "味方全体", (BrewRecipient.BothSides, "jpn") => "双方", (BrewRecipient.SelfAndEnemy, "jpn") => "自身と敵1体", (_, "jpn") => "自身",
        (BrewRecipient.SelectedEnemy, _) => "敌方目标", (BrewRecipient.AllEnemies, _) => "敌方全体",
        (BrewRecipient.AllAllies, _) => "己方全体", (BrewRecipient.BothSides, _) => "敌我双方", (BrewRecipient.SelfAndEnemy, _) => "自身与敌方目标", _ => "自身"
    };
}
