using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Random;
using OtherworldTreasures.Scripts.Events;
namespace OtherworldTreasures.Scripts.Potions;
public sealed partial class SCP294Potion
{
    private static Dictionary<string,string>? _catalogDrinkAliases;
    internal static IEnumerable<PotionModel> CatalogDrinks()=>ModelDb.AllPotions.Where(p=>p is not SCP294Potion&&p is not DeprecatedPotion&&p.Rarity!=PotionRarity.None&&p.GetType().Namespace?.Contains(".Mocks")!=true).OrderBy(p=>p.Id.Entry,StringComparer.Ordinal);
    internal static IEnumerable<string> CatalogDrinkNames()=>CatalogDrinks().Select(p=>p.Title.GetFormattedText());
    internal static bool IsCatalogBrew(PotionModel potion)=>TryRecipe(potion,out var recipe)&&recipe.NativePotionEntry!=null;
    internal static string BrewNameOf(PotionModel potion)=>RecipeFor(potion).Name;
    internal static string SummaryOf(PotionModel potion)=>potion is SCP294Potion brew?brew.PreviewSummary:
        Scp294InputText.Text("保留原药效，附带随机效果；悬停查看。","Original effect plus random effects. Hover for details.","元の効果にランダム効果を追加。ホバーで詳細。");
    private static string CatalogNameKey(string name)=>NormalizeDrinkName(Regex.Replace(name,@"\[[^\]]*\]","")).ToLowerInvariant().Trim(' ','.','。','!','！','?','？');
    internal static bool TryFindCatalogDrink(string input,out PotionModel potion)
    {
        if(_catalogDrinkAliases==null)
        {
            var aliases=new Dictionary<string,string>(StringComparer.Ordinal);var models=CatalogDrinks().ToArray();
            // Read every shipped locale so peers using different UI languages resolve the same input.
            foreach(string language in LocManager.Languages.OrderBy(s=>s,StringComparer.Ordinal))
            {
                var paths=new[]{ $"res://localization/{language}/potions.json" }.Concat(MegaCrit.Sts2.Core.Modding.ModManager.GetModdedLocTables(language,"potions.json"));
                foreach(string path in paths)
                {
                    if(!Godot.FileAccess.FileExists(path))continue;
                    var table=JsonSerializer.Deserialize<Dictionary<string,string>>(Godot.FileAccess.GetFileAsString(path))!;
                    foreach(var model in models)if(table.TryGetValue(model.Id.Entry+".title",out var title))aliases.TryAdd(CatalogNameKey(title),model.Id.Entry);
                }
            }
            foreach(var model in models)aliases.TryAdd(CatalogNameKey(model.Id.Entry),model.Id.Entry);
            _catalogDrinkAliases=aliases;
        }
        string key=CatalogNameKey(input);if(!_catalogDrinkAliases.TryGetValue(key,out var entry))
        {
            key=Regex.Replace(key,@"^(?:给我一杯|我要一杯|来一杯|来一瓶|一杯|一瓶|a cup of |a bottle of )", "");
            _catalogDrinkAliases.TryGetValue(key,out entry);
        }
        potion=entry==null?null!:CatalogDrinks().First(p=>p.Id.Entry==entry);return potion!=null;
    }
    private static NamedDrinkResult BrewCatalogDrink(Rng rng,string input,PotionModel original,bool allowFreeRedo)
    {
        EnsureCatalogUseHook(original);var request=ParseDrinkName(input);var potion=original.ToMutable();int density=rng.NextInt(6);var extras=new List<BrewIngredient>();
        int count=rng.NextInt(1,3);for(int i=0;i<count;i++)
        {
            BrewEffect effect;
            if(original.Usage==PotionUsage.AnyTime)
            {
                BrewEffect[] options=[BrewEffect.Heal,BrewEffect.HealPercent,BrewEffect.MaxHp,BrewEffect.Damage];effect=rng.NextItem(options);
                extras.Add(new BrewIngredient(effect,Math.Max(1,RollAmount(rng,effect,density)),BrewRecipient.Self));continue;
            }
            bool positive=rng.NextInt(2)==0;var options2=Enum.GetValues<BrewEffect>().Where(e=>e is not (BrewEffect.HealExtraTurn or BrewEffect.FairyRevive)&&(PolarityOf(e)==BrewPolarity.Positive)==positive).ToArray();effect=rng.NextItem(options2);
            var item=MakeIngredient(rng,effect,density,Feeling.Elegant);bool enemy=!IsPlayerOnlyEffect(effect)&&rng.NextInt(2)==0;var recipient=enemy?BrewRecipient.AllEnemies:BrewRecipient.Self;
            extras.Add(item with{Recipient=recipient,Amount=RollAmount(rng,effect,density,recipient),Timing=BrewTiming.Immediate});
        }
        if(original is FairyInABottle)AddResurrectionRisks(rng,extras,density);
        var recipe=new BrewRecipe(request.Name,extras,original.Id.Entry,VisualSeed:rng.NextInt(int.MaxValue),NativePotionEntry:original.Id.Entry);AttachRecipe(potion,recipe);
        int roll=rng.NextInt(100);var accident=roll<4?BrewAccident.HpLoss:roll<7?BrewAccident.Curse:roll<10&&allowFreeRedo?BrewAccident.FreeRedo:BrewAccident.None;
        return new NamedDrinkResult(potion,request,accident,accident==BrewAccident.HpLoss?rng.NextInt(3,9):0);
    }
    private static void AddResurrectionRisks(Rng rng,List<BrewIngredient> ingredients,int density)
    {
        var effects=new List<BrewEffect>{BrewEffect.Damage,BrewEffect.Weak,BrewEffect.Vulnerable,BrewEffect.Frail,BrewEffect.StrengthDown,BrewEffect.DexterityDown};
        for(int i=0;i<2;i++){var effect=rng.NextItem(effects);effects.Remove(effect);ingredients.Add(new BrewIngredient(effect,Math.Max(1,RollAmount(rng,effect,density)),BrewRecipient.Self));}
    }
    private static readonly HashSet<MethodInfo> ExtraCatalogHooks=new();
    internal static void EnsureCatalogUseHook(PotionModel potion)
    {
        if(potion.GetType().Assembly==typeof(PotionModel).Assembly)return;
        var method=AccessTools.Method(potion.GetType(),"OnUse");if(method==null||!ExtraCatalogHooks.Add(method))return;
        new Harmony(Entry.ModId+".scp294.catalog").Patch(method,postfix:new HarmonyMethod(typeof(SCP294CatalogUsePatch),"Postfix"));
    }
    internal static async Task ApplyCatalogBonus(PotionModel potion,Task original,PlayerChoiceContext context,Creature? target)
    {
        await original;if(!potion.Owner.Creature.IsAlive||!TryRecipe(potion,out var recipe)||recipe.NativePotionEntry==null)return;
        var bonus=(SCP294Potion)ModelDb.Potion<SCP294Potion>().ToMutable();AttachRecipe(bonus,recipe with{NativePotionEntry=null});bonus.Owner=potion.Owner;
        await bonus.OnUse(context,target);
    }
}

// Native potion classes keep their original targeting, passive hooks and usability.
// Only explicitly augmented instances run the extra effects, inside their normal use task.
[HarmonyPatch]
internal static class SCP294CatalogUsePatch
{
    private static IEnumerable<MethodBase> TargetMethods()=>typeof(PotionModel).Assembly.GetTypes()
        .Where(t=>typeof(PotionModel).IsAssignableFrom(t)&&!t.IsAbstract)
        .Select(t=>AccessTools.DeclaredMethod(t,"OnUse")).Where(m=>m!=null).Cast<MethodBase>();
    private static void Postfix(PotionModel __instance,PlayerChoiceContext __0,Creature? __1,ref Task __result)
    {
        if(SCP294Potion.IsCatalogBrew(__instance))__result=SCP294Potion.ApplyCatalogBonus(__instance,__result,__0,__1);
    }
}
