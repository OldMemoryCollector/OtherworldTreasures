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
    private static readonly Dictionary<string,PotionModel> CatalogByEntry=new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string,PotionModel> AnyPotionByEntry=new(StringComparer.OrdinalIgnoreCase);
    private static PotionModel[]? _catalogDrinks;
    private static int _catalogStamp=-1;

    // ModelDb.AllPotions only walks the potion pools of vanilla characters plus the shared pool.
    // A mod is free to register into a pool of its own (RitsuLib: [RegisterPotion(typeof(MyPool))]),
    // and those potions never reach that list, so the full model registry is the dependable source.
    // Everything is read through one cache so vanilla and modded potions behave identically.
    private static PotionModel[] EnsureCatalog()
    {
        int stamp=ModelDb.All.Count();
        if(_catalogDrinks!=null&&_catalogStamp==stamp)return _catalogDrinks;
        var seen=new HashSet<ModelId>();var every=new List<PotionModel>();var drinks=new List<PotionModel>();
        foreach(var potion in ModelDb.All.OfType<PotionModel>().Concat(ModelDb.AllPotions))
        {
            if(potion==null||!seen.Add(potion.Id))continue;
            every.Add(potion);
            // A second SCP-294 machine is not a drink. The standalone random-potion mod ships its own
            // SCP294Potion, and pouring that would hand the player a copy of the machine.
            if(potion.GetType().Name=="SCP294Potion")continue;
            if(potion is DeprecatedPotion)continue;
            if(potion.Rarity==PotionRarity.None)continue;
            if(potion.GetType().Namespace?.Contains(".Mocks")==true)continue;
            drinks.Add(potion);
        }
        every.Sort((a,b)=>string.CompareOrdinal(a.Id.Entry,b.Id.Entry));
        drinks.Sort((a,b)=>string.CompareOrdinal(a.Id.Entry,b.Id.Entry));
        _catalogDrinks=drinks.ToArray();_catalogStamp=stamp;_catalogDrinkAliases=null;
        AnyPotionByEntry.Clear();CatalogByEntry.Clear();
        foreach(var potion in every)AnyPotionByEntry[potion.Id.Entry]=potion;
        foreach(var potion in _catalogDrinks)CatalogByEntry[potion.Id.Entry]=potion;
        return _catalogDrinks;
    }
    internal static IEnumerable<PotionModel> CatalogDrinks()=>EnsureCatalog();
    internal static string SafeTitle(PotionModel potion)
    {
        // A broken third-party title must not take the whole catalog down with it.
        try{return potion.Title.GetFormattedText()??"";}catch(Exception){return "";}
    }
    internal static PotionModel? PotionByEntry(string? entry)
    {
        if(string.IsNullOrWhiteSpace(entry))return null;
        EnsureCatalog();
        return AnyPotionByEntry.TryGetValue(entry,out var potion)?potion:null;
    }
    internal static IEnumerable<string> CatalogDrinkNames()=>EnsureCatalog().Select(SafeTitle).Where(name=>name.Length>0);
    internal static bool IsCatalogBrew(PotionModel potion)=>TryRecipe(potion,out var recipe)&&recipe.NativePotionEntry!=null;
    internal static string BrewNameOf(PotionModel potion)=>DisplayName(RecipeFor(potion));
    internal static string SummaryOf(PotionModel potion)=>TryRecipe(potion,out var recipe)?EffectBody(recipe):"";
    private static string CatalogNameKey(string name)=>NormalizeDrinkName(Regex.Replace(name,@"\[[^\]]*\]","")).ToLowerInvariant().Trim(' ','.','。','!','！','?','？');
    private static Dictionary<string,string> BuildCatalogAliases()
    {
        var aliases=new Dictionary<string,string>(StringComparer.Ordinal);var models=EnsureCatalog();
        // Read every shipped locale so peers using different UI languages resolve the same input.
        foreach(string language in LocManager.Languages.OrderBy(s=>s,StringComparer.Ordinal))
        {
            var paths=new[]{ $"res://localization/{language}/potions.json" }.Concat(MegaCrit.Sts2.Core.Modding.ModManager.GetModdedLocTables(language,"potions.json"));
            foreach(string path in paths)
            {
                if(!Godot.FileAccess.FileExists(path))continue;
                Dictionary<string,string>? table;
                try{table=JsonSerializer.Deserialize<Dictionary<string,string>>(Godot.FileAccess.GetFileAsString(path));}
                catch(Exception){continue;}
                if(table==null)continue;
                foreach(var model in models)if(table.TryGetValue(model.Id.Entry+".title",out var title)&&!string.IsNullOrWhiteSpace(title))aliases.TryAdd(CatalogNameKey(title),model.Id.Entry);
            }
        }
        foreach(var model in models)
        {
            aliases.TryAdd(CatalogNameKey(model.Id.Entry),model.Id.Entry);
            // A mod may resolve its titles through its own table or code path, which the raw JSON scan
            // above cannot see. The runtime title always can, so it is the load-bearing fallback.
            // A table that is missing entirely yields the raw key instead of a title; letting that
            // through would make "potions.x.title" a nameable drink.
            string title=SafeTitle(model);
            if(title.Length>0&&!title.Contains(".title",StringComparison.OrdinalIgnoreCase))aliases.TryAdd(CatalogNameKey(title),model.Id.Entry);
        }
        _catalogDrinkAliases=aliases;
        return aliases;
    }
    internal static bool TryFindCatalogDrink(string input,out PotionModel potion)
    {
        EnsureCatalog();
        var aliases=_catalogDrinkAliases??=BuildCatalogAliases();
        string key=CatalogNameKey(input);string? entry=null;
        if(!aliases.TryGetValue(key,out entry))
        {
            key=Regex.Replace(key,@"^(?:给我一杯|我要一杯|来一杯|来一瓶|一杯|一瓶|a cup of |a bottle of )", "");
            aliases.TryGetValue(key,out entry);
        }
        potion=entry!=null&&CatalogByEntry.TryGetValue(entry,out var model)?model:null!;return potion!=null;
    }
    private static NamedDrinkResult BrewCatalogDrink(Rng rng,string input,PotionModel original)
    {
        var request=ParseDrinkName(input);int density=rng.NextInt(6);
        // Naming a real potion pours that potion: its own effect always runs untouched. Rarity does
        // not dilute the bottle, it makes the machine more willing to top the cup up with something
        // else — and what it adds is random in direction and target, so a rare cup can come out
        // soured just as easily as blessed.
        var potion=original.ToMutable();
        var extras=ContaminationExtras(rng,original,density);
        // The extras ride on the native use task, so every topped-up cup needs the hook.
        if(extras.Count>0)EnsureCatalogUseHook(original);
        var recipe=new BrewRecipe(request.Name,extras,original.Id.Entry,VisualSeed:rng.NextInt(int.MaxValue),
            NativePotionEntry:original.Id.Entry);AttachRecipe(potion,recipe);
        return new NamedDrinkResult(potion,request);
    }
    // Purity is the only thing rarity changes: 70 / 45 / 25 percent clean, and 1–2 / 1–3 / 1–4 extra
    // entries otherwise. The extras are never picked to suit the bottle.
    private static List<BrewIngredient> ContaminationExtras(Rng rng,PotionModel original,int density)
    {
        var extras=new List<BrewIngredient>();
        int rank=original.Rarity switch{PotionRarity.Common=>0,PotionRarity.Uncommon=>1,PotionRarity.Rare=>2,_=>1};
        int pureChance=original.Rarity switch{PotionRarity.Common=>70,PotionRarity.Uncommon=>45,PotionRarity.Rare=>25,_=>45};
        if(rng.NextInt(100)<pureChance)return extras;
        int count=rng.NextInt(1,rank+2);
        // A fairy in a bottle already exacts its own price, so it never comes out as a mild cup.
        if(original is FairyInABottle)count=Math.Max(2,count);
        for(int i=0;i<count;i++)
        {
            var extra=RandomExtra(rng,original,density,extras);
            if(extra!=null)extras.Add(extra);
        }
        return extras;
    }
    private static BrewIngredient? RandomExtra(Rng rng,PotionModel original,int density,IReadOnlyList<BrewIngredient> existing)
    {
        var used=new HashSet<BrewEffect>(existing.Select(item=>item.Effect));
        // Out of combat there is nothing to afflict, so those cups can only carry effects that
        // resolve on the drinker — a limit of the game, not a kindness.
        if(original.Usage==PotionUsage.AnyTime)
        {
            BrewEffect[] safe=[BrewEffect.Heal,BrewEffect.HealPercent,BrewEffect.MaxHp,BrewEffect.Damage,BrewEffect.Regen,BrewEffect.Block];
            var open=safe.Where(effect=>!used.Contains(effect)).ToArray();
            var pick=open.Length>0?rng.NextItem(open):rng.NextItem(safe);
            return new BrewIngredient(pick,Math.Max(1,RollAmount(rng,pick,density,BrewRecipient.Self)),BrewRecipient.Self,BrewTiming.Immediate);
        }
        // Both sides are on the table, and so is the target: a boon can land on the enemy and a
        // curse can land on the drinker, purely by chance.
        bool helpful=rng.NextInt(2)==0;
        var candidates=Enum.GetValues<BrewEffect>()
            .Where(effect=>effect is not (BrewEffect.HealExtraTurn or BrewEffect.FairyRevive))
            .Where(effect=>PolarityOf(effect)==(helpful?BrewPolarity.Positive:BrewPolarity.Negative))
            .Where(effect=>!used.Contains(effect)).ToArray();
        if(candidates.Length==0)return null;
        var picked=rng.NextItem(candidates);
        bool toEnemy=!IsPlayerOnlyEffect(picked)&&rng.NextInt(2)==0;
        var recipient=toEnemy?BrewRecipient.AllEnemies:BrewRecipient.Self;
        var item=MakeIngredient(rng,picked,density,toEnemy?Feeling.Angry:Feeling.Elegant);
        return item with{Recipient=recipient,Amount=Math.Max(1,RollAmount(rng,picked,density,recipient)),Timing=BrewTiming.Immediate};
    }
    private static readonly HashSet<MethodInfo> ExtraCatalogHooks=new();
    internal static void EnsureCatalogUseHook(PotionModel potion)
    {
        if(potion.GetType().Assembly==typeof(PotionModel).Assembly)return;
        var method=ResolveUseMethod(potion.GetType());
        if(method==null||!ExtraCatalogHooks.Add(method))return;
        try
        {
            new Harmony(Entry.ModId+".scp294.catalog").Patch(method,prefix:new HarmonyMethod(typeof(SCP294CatalogUsePatch),"Prefix"),postfix:new HarmonyMethod(typeof(SCP294CatalogUsePatch),"Postfix"),finalizer:new HarmonyMethod(typeof(SCP294CatalogUsePatch),"Finalizer"));
            Entry.Logger.Info($"[SCP294] 已挂接第三方药水 {method.DeclaringType?.Name}.OnUse，可掺入附加效果");
        }
        catch(Exception ex)
        {
            // A rejected patch must never break the brew: the player still gets the named potion pure.
            ExtraCatalogHooks.Remove(method);
            Entry.Logger.Warn($"[SCP294] 无法挂接第三方药水 {method.DeclaringType?.Name}.OnUse，这杯将保持纯净：{ex.Message}");
        }
    }
    // A foreign potion may implement its use in an intermediate base class, and one that overrides
    // nothing is already covered by the base patch of SCP294CatalogUsePatch. Walk up to the first
    // override that actually has PotionModel's own signature, so nothing is patched by guesswork.
    private static MethodInfo? ResolveUseMethod(Type type)
    {
        var baseUse=AccessTools.DeclaredMethod(typeof(PotionModel),"OnUse");
        for(var current=type;current!=null&&current!=typeof(PotionModel);current=current.BaseType)
        {
            var declared=AccessTools.DeclaredMethod(current,"OnUse");
            if(declared==null||declared==baseUse)continue;
            var parameters=declared.GetParameters();
            if(parameters.Length!=2||parameters[0].ParameterType!=typeof(PlayerChoiceContext)||parameters[1].ParameterType!=typeof(Creature))continue;
            return declared;
        }
        return null;
    }
    internal static async Task ApplyCatalogBonus(PotionModel potion,Task original,PlayerChoiceContext context,Creature? target)
    {
        await original;if(!potion.Owner.Creature.IsAlive||!TryRecipe(potion,out var recipe)||recipe.NativePotionEntry==null||recipe.Ingredients.Count==0)return;
        // A third-party potion that calls base.OnUse would reach both the override hook and the base
        // hook, so the extras are bound to the instance and only ever poured once.
        if(!BonusApplied.TryGetValue(potion,out _))BonusApplied.Add(potion,new object());else return;
        var bonus=(SCP294Potion)ModelDb.Potion<SCP294Potion>().ToMutable();AttachRecipe(bonus,recipe with{NativePotionEntry=null});bonus.Owner=potion.Owner;
        await bonus.OnUse(context,target);
    }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<PotionModel,object> BonusApplied=new();
}

// Native potion classes keep their original targeting, passive hooks and usability.
// Only explicitly augmented instances run the extra effects, inside their normal use task.
[HarmonyPatch]
internal static class SCP294CatalogUsePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        // Patching the base implementation is what makes a potion from another mod work without
        // being listed here: a potion that overrides nothing dispatches straight into this method.
        if(AccessTools.DeclaredMethod(typeof(PotionModel),"OnUse") is { } baseUse)yield return baseUse;
        foreach(var type in typeof(PotionModel).Assembly.GetTypes().Where(t=>typeof(PotionModel).IsAssignableFrom(t)&&!t.IsAbstract))
        {
            var method=AccessTools.DeclaredMethod(type,"OnUse");
            if(method!=null)yield return method;
        }
    }
    private static void Prefix(PotionModel __instance, out IDisposable? __state) =>
        __state = SCP294Potion.IsCatalogBrew(__instance) ? Scp294PotionSources.BeginCatalog(__instance.Owner.Creature) : null;
    private static void Finalizer(IDisposable? __state) => __state?.Dispose();
    private static void Postfix(PotionModel __instance,PlayerChoiceContext __0,Creature? __1,ref Task __result, IDisposable? __state)
    {
        __state?.Dispose();
        if(SCP294Potion.IsCatalogBrew(__instance))__result=SCP294Potion.ApplyCatalogBonus(__instance,__result,__0,__1);
    }
}
