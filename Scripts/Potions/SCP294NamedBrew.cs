using System.Text;
using System.Text.RegularExpressions;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using OtherworldTreasures.Scripts.Events;
namespace OtherworldTreasures.Scripts.Potions;

internal sealed record NamedDrinkRequest(string Name, IReadOnlyList<BrewEffect> Effects, bool Enemy, bool AllEnemies, bool Water, int FavorableCount);
internal enum BrewAccident { None, HpLoss, Curse, FreeRedo }
internal sealed record NamedDrinkResult(PotionModel Potion, NamedDrinkRequest Request, BrewAccident Accident, int Damage);

public sealed partial class SCP294Potion
{
    internal const int DrinkInputLimit = 80;
    private sealed record DrinkKeyword(BrewEffect Effect, string[] Aliases);
    // Synonyms count as one request. "Perfect" asks for protection, not the rare all-buffs preset.
    private static readonly DrinkKeyword[] DrinkKeywords =
    [
        new(BrewEffect.Heal, ["回血", "治疗", "回血药", "恢复生命", "恢复血", "补血", "heal", "healing", "回復"]),
        new(BrewEffect.AmbergrisHeal, ["龙涎香", "百分比回血", "ambergris"]),
        new(BrewEffect.FairyRevive, ["保命", "复活", "救命", "仙灵", "不死", "revive", "fairy"]),
        new(BrewEffect.Artifact, ["完美", "人工制品", "免疫负面", "解毒", "perfect", "artifact"]),
        new(BrewEffect.Strength, ["力量", "力气", "strength"]),
        new(BrewEffect.Dexterity, ["敏捷", "dexterity"]),
        new(BrewEffect.Block, ["格挡", "防御", "block", "defense"]),
        new(BrewEffect.Absorption, ["伤害吸收", "吸收伤害", "absorption"]),
        new(BrewEffect.Regen, ["再生", "持续回血", "regeneration", "regen"]),
        new(BrewEffect.Buffer, ["缓冲", "免伤", "buffer"]),
        new(BrewEffect.Intangible, ["无实体", "intangible"]),
        new(BrewEffect.Thorns, ["荆棘", "反伤", "thorns"]),
        new(BrewEffect.Plating, ["覆甲", "plating"]),
        new(BrewEffect.Focus, ["集中", "focus"]),
        new(BrewEffect.Ritual, ["仪式", "ritual"]),
        new(BrewEffect.DrawCards, ["抽牌", "draw cards", "draw"]),
        new(BrewEffect.GainEnergy, ["能量", "energy"]),
        new(BrewEffect.ExtraTurn, ["额外回合", "额外行动", "extra turn"]),
        new(BrewEffect.MaxHp, ["生命上限", "最大生命", "max hp"]),
        new(BrewEffect.Weak, ["虚弱", "weak"]),
        new(BrewEffect.Vulnerable, ["易伤", "vulnerable"]),
        new(BrewEffect.Frail, ["脆弱", "frail"]),
        new(BrewEffect.Poison, ["中毒", "毒药", "毒水", "上毒", "poison"]),
        new(BrewEffect.Doom, ["灾厄", "doom"]),
        new(BrewEffect.Damage, ["伤害", "掉血", "扣血", "火焰", "damage"]),
        new(BrewEffect.StrengthDown, ["降低力量", "减少力量", "削弱力量", "strength down"]),
        new(BrewEffect.DexterityDown, ["降低敏捷", "减少敏捷", "削弱敏捷", "dexterity down"]),
        new(BrewEffect.ChannelRandomOrbs, ["随机充能球", "random orb"]),
        new(BrewEffect.ChannelSameOrbs, ["充能球", "orb"])
    ];
    internal static string NormalizeDrinkName(string? input)
    {
        string value=(input??"").Normalize(NormalizationForm.FormKC);
        value=Regex.Replace(value,@"[^\p{L}\p{N}\p{Zs}，。！？、：；,.!?%()+·-]", " ");
        value=Regex.Replace(value,@"\s+", " ").Trim();
        return string.Concat(value.EnumerateRunes().Take(DrinkInputLimit).Select(r=>r.ToString()));
    }
    internal static NamedDrinkRequest ParseDrinkName(string? input)
    {
        string name=NormalizeDrinkName(input);bool enemy=new[]{"敌人","敌方","敌方目标","enemy","enemies"}.Any(s=>name.Contains(s,StringComparison.OrdinalIgnoreCase));
        bool all=enemy&&new[]{"全体","所有","all enemies"}.Any(s=>name.Contains(s,StringComparison.OrdinalIgnoreCase));
        var hits=new List<(int At,int Length,BrewEffect Effect)>();
        foreach(var keyword in DrinkKeywords)foreach(string alias in keyword.Aliases)
        {
            int start=0;
            while(start<name.Length)
            {
                int at=name.IndexOf(alias,start,StringComparison.OrdinalIgnoreCase);if(at<0)break;start=at+alias.Length;
                if(alias.All(c=>c<128)&&((at>0&&char.IsLetter(name[at-1])&&name[at-1]<128)||(start<name.Length&&char.IsLetter(name[start])&&name[start]<128)))continue;
                string prefix=name[Math.Max(0,at-6)..at];
                if(Regex.IsMatch(prefix,@"(?:不要|不想|不需要|不加|别|不要加|without |no )$",RegexOptions.IgnoreCase))continue;
                hits.Add((at,alias.Length,keyword.Effect));
            }
        }
        // A longer phrase wins over its substring, e.g. lowering Strength, percentage heal or random Orbs.
        var selected=new List<(int At,int Length,BrewEffect Effect)>();
        foreach(var hit in hits.OrderByDescending(h=>h.Length).ThenBy(h=>h.At))
            if(!selected.Any(s=>hit.At<s.At+s.Length&&s.At<hit.At+hit.Length))selected.Add(hit);
        var effects=selected.OrderBy(h=>h.At).Select(h=>h.Effect).Distinct().Take(6).ToArray();
        int favorable=effects.Count(effect=>IsFavorableRequest(effect,enemy));
        bool water=effects.Length==0&&new[]{"水","一杯水","白水","白开水","纯净水","矿泉水","water","a cup of water"}.Contains(name.ToLowerInvariant());
        return new NamedDrinkRequest(name,effects,enemy,all,water,favorable);
    }
    private static bool IsFavorableRequest(BrewEffect effect,bool enemy) => enemy
        ? PolarityOf(effect)==BrewPolarity.Negative : PolarityOf(effect)==BrewPolarity.Positive;
    internal static string DrinkRecognition(NamedDrinkRequest request)
    {
        string en=LocManager.Instance.Language;
        if(request.Effects.Count==0)return "";
        string names=string.Join("、",request.Effects.Select(e=>e==BrewEffect.FairyRevive?Scp294InputText.Text("保命","Survival","生存"):EffectName(e)));
        int risks=Math.Max(0,request.FavorableCount-1);
        string target=request.Enemy?Scp294InputText.Text("敌方","enemy","敵"):Scp294InputText.Text("自身","yourself","自身");
        string text=Scp294InputText.Text($"识别：{names}。目标倾向{target}。\n保留这些药效，再混入随机效果；额外风险{risks}项。",$"Found: {names}. Target: {target}.\nKeep these effects plus a random effect; {risks} added risks.",$"認識：{names}。対象：{target}。\n指定効果とランダム効果、追加リスク{risks}個。");
        return FormatPotionText(text,en);
    }
    internal static NamedDrinkResult BrewNamed(Rng rng,string input,bool allowFreeRedo=true)
    {
        if(TryFindCatalogDrink(input,out var original))return BrewCatalogDrink(rng,input,original,allowFreeRedo);
        var request=ParseDrinkName(input);if(request.Name.Length==0)throw new ArgumentException("Empty drink name",nameof(input));
        var ingredients=new List<BrewIngredient>();int density=rng.NextInt(6);
        foreach(var effect in request.Effects)
        {
            var ingredient=MakeIngredient(rng,effect,density,Feeling.Elegant);
            if(request.Enemy && !IsPlayerOnlyEffect(effect))
            {var target=request.AllEnemies?BrewRecipient.AllEnemies:BrewRecipient.SelectedEnemy;ingredient=ingredient with{Recipient=target,Amount=RollAmount(rng,effect,density,target)};}
            ingredients.Add(ingredient with{Amount=Math.Max(1,ingredient.Amount),Timing=BrewTiming.Immediate});
        }
        var pool=Enum.GetValues<BrewEffect>().Where(e=>e!=BrewEffect.HealExtraTurn&&!request.Effects.Contains(e)).ToArray();
        if(request.Effects.Count==0)
        {
            int count=rng.NextInt(1,6);for(int i=0;i<count;i++)ingredients.Add(UnrecognizedIngredient(rng,density,request.AllEnemies));
        }
        else ingredients.Add(MakeIngredient(rng,rng.NextItem(pool),density,request.Enemy?Feeling.Angry:Feeling.Strange));
        BrewEffect[] harm=request.Enemy?[BrewEffect.Heal,BrewEffect.Strength,BrewEffect.Block,BrewEffect.Regen,BrewEffect.Artifact,BrewEffect.Buffer]
            :[BrewEffect.Damage,BrewEffect.Weak,BrewEffect.Vulnerable,BrewEffect.Frail,BrewEffect.StrengthDown,BrewEffect.DexterityDown];
        for(int i=1;i<request.FavorableCount;i++)
        {
            var effect=rng.NextItem(harm);var recipient=request.Enemy?(request.AllEnemies?BrewRecipient.AllEnemies:BrewRecipient.SelectedEnemy):BrewRecipient.Self;
            ingredients.Add(new BrewIngredient(effect,Math.Max(1,RollAmount(rng,effect,density,recipient)),recipient));
        }
        if(request.Effects.Contains(BrewEffect.FairyRevive))AddResurrectionRisks(rng,ingredients,density);
        string name=string.Concat(request.Name.EnumerateRunes().Take(24).Select(r=>r.ToString()))+(request.Name.EnumerateRunes().Count()>24?"…":"");
        string bottle=ChooseNativeEntry(rng,NativeBottleEntries);
        var recipe=new BrewRecipe(name,ingredients,bottle,LiquidEntries:ChooseLiquidEntries(rng,bottle,ingredients.Count),VisualStyle:RollVisualStyle(rng),VisualSeed:rng.NextInt(int.MaxValue));
        var potion=(SCP294Potion)ModelDb.Potion<SCP294Potion>().ToMutable();AttachRecipe(potion,recipe);
        int roll=rng.NextInt(100);var accident=roll<4?BrewAccident.HpLoss:roll<7?BrewAccident.Curse:roll<10&&allowFreeRedo?BrewAccident.FreeRedo:BrewAccident.None;
        return new NamedDrinkResult(potion,request,accident,accident==BrewAccident.HpLoss?rng.NextInt(3,9):0);
    }
    private static BrewIngredient UnrecognizedIngredient(Rng rng,int density,bool allEnemies)
    {
        int targetRoll=rng.NextInt(100);bool enemy=targetRoll is >=40 and <75 or >=90;
        bool positive=targetRoll is >=40 and <90;
        var candidates=Enum.GetValues<BrewEffect>().Where(effect=>effect!=BrewEffect.HealExtraTurn&&
            (PolarityOf(effect)==BrewPolarity.Positive)==positive&&(!enemy||!IsPlayerOnlyEffect(effect))).ToArray();
        var effect=rng.NextItem(candidates);var item=MakeIngredient(rng,effect,density,Feeling.Elegant);
        var recipient=enemy?(allEnemies?BrewRecipient.AllEnemies:BrewRecipient.SelectedEnemy):BrewRecipient.Self;
        return item with{Recipient=recipient,Amount=RollAmount(rng,effect,density,recipient),Timing=BrewTiming.Immediate};
    }

}

internal static class Scp294InputText
{
    internal static string Text(string zh,string en,string jp)=>LocManager.Instance.Language switch{"eng"=>en,"jpn"=>jp,_=>zh};
}
