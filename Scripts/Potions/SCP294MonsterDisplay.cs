using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace OtherworldTreasures.Scripts.Potions;

public sealed partial class SCP294Potion
{
    internal static SCP294Potion MonsterDisplay(MonsterBrewPlan plan)
    {
        var rng = Scp294MonsterPotions.RecipeRng(plan.Monster, plan.Move);
        var ingredients = plan.Effects.Select(e => new BrewIngredient(e.Effect, e.Amount,
            e.TargetsPlayer ? BrewRecipient.SelectedEnemy : BrewRecipient.Self)).ToArray();
        string name = ingredients.Length == 0 ? LocText("defaultName") : MakeName(rng, ingredients);
        string bottle = ChooseNativeEntry(rng, NativeBottleEntries);
        var recipe = new BrewRecipe(name, ingredients, bottle,
            LiquidEntries: ChooseLiquidEntries(rng, bottle, ingredients.Length),
            VisualStyle: RollVisualStyle(rng), VisualSeed: rng.NextInt(int.MaxValue));
        var potion = (SCP294Potion)ModelDb.Potion<SCP294Potion>().ToMutable();
        AttachRecipe(potion, recipe);
        return potion;
    }

    internal static string MonsterDescription(MonsterBrewPlan plan)
    {
        string language = LocManager.Instance.Language;
        return string.Join("\n", plan.Effects.Select(effect =>
        {
            var ingredient = new BrewIngredient(effect.Effect, effect.Amount,
                effect.TargetsPlayer ? BrewRecipient.SelectedEnemy : BrewRecipient.Self);
            string text = Describe(ingredient);
            // Only change the actor label; retain the exact player-potion effect text and formatting.
            return language switch
            {
                "eng" => effect.TargetsPlayer ? text.Replace("Target: ", "Player: ") : text.Replace("You: ", "Self: "),
                "jpn" => effect.TargetsPlayer ? text.Replace("敵：", "プレイヤー：") : text,
                _ => effect.TargetsPlayer ? text.Replace("敌方目标：", "玩家：") : text
            };
        }));
    }
}
