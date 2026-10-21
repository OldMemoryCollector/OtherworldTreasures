using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace OtherworldTreasures.Scripts.Potions;

public sealed partial class SCP294Potion
{
    internal static SCP294Potion MonsterDisplay(MonsterBrewPlan plan)
    {
        var rng = Scp294MonsterPotions.RecipeRng(plan.Monster, plan.Move, combatStart: plan.IsCombatStart);
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
        if (plan.Effects.Length == 0) return language switch
        { "eng" => "No effects.", "jpn" => "効果なし。", _ => "无药效。" };
        return GroupedDescription(plan.Effects.Select(effect => new BrewIngredient(effect.Effect, effect.Amount,
            effect.TargetsPlayer ? BrewRecipient.SelectedEnemy : BrewRecipient.Self)), monster: true);
    }
}
