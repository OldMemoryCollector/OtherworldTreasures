using System.Text;
using System.Text.RegularExpressions;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;
using OtherworldTreasures.Scripts.Events;
namespace OtherworldTreasures.Scripts.Potions;

// One named effect, with the target decided by the clause it was written in.
internal sealed record RequestedEffect(BrewEffect Effect, bool ToEnemy, bool AllEnemies);
internal sealed record NamedDrinkRequest(string Name, IReadOnlyList<RequestedEffect> Effects, bool Enemy, bool AllEnemies,
    string? Family = null, bool FamilyPositive = true);
internal sealed record NamedDrinkResult(PotionModel Potion, NamedDrinkRequest Request);

public sealed partial class SCP294Potion
{
    internal const int DrinkInputLimit = 80;
    private sealed record DrinkKeyword(BrewEffect Effect, string[] Aliases);

    // SCP-294 returns whatever liquid is named. Effects alone are not enough: a real drink name, a
    // nonsense name and a full effect list are three different requests and must be read differently.

    // 1) Explicit effect keywords. These win over everything else, because the player stated what it does.
    private static readonly DrinkKeyword[] DrinkKeywords =
    [
        new(BrewEffect.Heal, ["回血", "治疗", "回血药", "恢复生命", "恢复血", "补血", "血量", "生命值", "heal", "healing", "回復"]),
        new(BrewEffect.AmbergrisHeal, ["龙涎香", "百分比回血", "ambergris"]),
        new(BrewEffect.HealPercent, ["百分比回血", "恢复百分比", "按比例恢复", "percent heal", "percentage"]),
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

    // 2) Liquid families. Chinese is head-final, so the family is the match that ends last in the
    // phrase: 「恶魔的奶」is milk, 「上帝的毒」is poison. Modifiers before the head only bend its tone.
    private sealed record DrinkFamily(string Name, string[] Keywords, BrewEffect[] Effects, bool Positive);
    private static readonly DrinkFamily[] DrinkFamilies =
    [
        new("咖啡", ["美式咖啡", "美式", "拿铁", "卡布奇诺", "摩卡咖啡", "浓缩咖啡", "馥芮白", "焦糖玛奇朵", "香草拿铁", "榛果拿铁", "椰乳拿铁", "燕麦拿铁", "冷萃咖啡", "冰滴咖啡", "阿芙佳朵", "抹茶拿铁", "焙茶拿铁", "爱尔兰咖啡", "越南蛋咖啡", "土耳其咖啡", "咖啡"],
            [BrewEffect.GainEnergy, BrewEffect.Focus, BrewEffect.Block, BrewEffect.DrawCards], true),
        new("奶茶", ["珍珠奶茶", "布丁奶茶", "仙草奶茶", "椰果奶茶", "红豆奶茶", "芋泥奶茶", "芋圆奶茶", "黑糖奶茶", "奶盖乌龙", "芝士奶盖茶", "奶茶"],
            [BrewEffect.Regen, BrewEffect.Heal, BrewEffect.Block, BrewEffect.Buffer], true),
        new("茶", ["绿茶", "红茶", "乌龙茶", "普洱茶", "白茶", "黄茶", "茉莉花茶", "龙井茶", "铁观音", "大红袍", "金骏眉", "桂花乌龙", "玫瑰红茶", "菊花茶", "洛神花茶", "冰红茶", "柠檬红茶", "蜜桃乌龙", "百香果绿茶", "葡萄乌龙", "荔枝红茶", "草莓茉莉", "苹果肉桂茶", "金桔柠檬茶", "柚子茶", "蜂蜜柠檬茶", "橙香红茶", "青提茉莉", "凤梨乌龙", "杨梅绿茶", "鸳鸯奶茶", "港式奶茶", "泰式奶茶", "印度香料奶茶", "土耳其红茶", "摩洛哥薄荷茶", "越南滴漏咖啡", "马黛茶", "康普茶", "开菲尔", "拉西", "荞麦茶", "大麦茶", "凉茶", "冬瓜茶", "竹蔗茅根水", "罗汉果茶", "绿豆汤", "红豆汤", "桂圆红枣茶", "姜枣茶", "蜂蜜柚子茶", "酸梅汤", "冰糖雪梨", "银耳莲子羹", "酒酿圆子饮", "茶", "汤"],
            [BrewEffect.Heal, BrewEffect.Regen, BrewEffect.Buffer, BrewEffect.MaxHp], true),
        new("果汁", ["橙汁", "苹果汁", "葡萄汁", "西瓜汁", "芒果汁", "菠萝汁", "桃汁", "梨汁", "荔枝汁", "石榴汁", "蔓越莓汁", "蓝莓汁", "草莓汁", "猕猴桃汁", "番石榴汁", "百香果", "汁"],
            [BrewEffect.Heal, BrewEffect.GainEnergy, BrewEffect.Strength], true),
        new("牛奶", ["牛奶", "巧克力奶", "草莓牛奶", "香蕉牛奶", "木瓜牛奶", "芒果牛奶", "哈密瓜牛奶", "椰奶", "酸奶", "酸奶饮品", "乳酸菌饮料", "优酪乳", "炼乳饮", "羊奶", "蜂蜜牛奶", "豆浆", "黑豆浆", "燕麦奶", "杏仁奶", "核桃奶", "花生奶", "米乳", "芝麻糊", "紫米露", "红豆薏米饮", "玉米汁", "青稞奶", "豆乳", "奶", "乳"],
            [BrewEffect.Regen, BrewEffect.Heal, BrewEffect.Block], true),
        new("汽水", ["可乐", "无糖可乐", "柠檬汽水", "橙味汽水", "葡萄汽水", "苹果汽水", "荔枝汽水", "姜汁汽水", "奶油汽水", "沙士", "苏打水", "青柠气泡水", "白桃气泡水", "西柚气泡水", "莓果气泡水", "盐汽水", "格瓦斯", "无酒精莫吉托", "无酒精果味宾治", "汽水", "气泡水"],
            [BrewEffect.GainEnergy, BrewEffect.Dexterity, BrewEffect.Block], true),
        new("水", ["矿泉水", "纯净水", "温水", "冰水", "柠檬水", "黄瓜薄荷水", "椰子水", "蜂蜜水", "电解质水", "热巧克力", "可可", "薄荷可可", "白开水", "白水", "水", "water"],
            [BrewEffect.Heal, BrewEffect.Artifact, BrewEffect.Regen], true),
        new("能量", ["能量饮料", "运动饮料", "维生素饮料", "能量水"],
            [BrewEffect.GainEnergy, BrewEffect.Dexterity, BrewEffect.Strength], true),
        new("酒", ["啤酒", "葡萄酒", "威士忌", "伏特加", "朗姆酒", "龙舌兰", "白兰地", "清酒", "烧酒", "黄酒", "米酒", "白酒", "茅台", "二锅头", "鸡尾酒", "香槟", "果酒", "梅酒", "酒", "wine", "beer", "whisky", "vodka"],
            [BrewEffect.Strength, BrewEffect.Dexterity, BrewEffect.Block], true),
        new("血", ["血", "血液", "blood", "human blood"], [BrewEffect.Damage, BrewEffect.StrengthDown, BrewEffect.Weak], false),
        new("毒", ["毒", "毒药", "毒水", "poison"], [BrewEffect.Poison, BrewEffect.Weak, BrewEffect.Vulnerable], false),
        new("岩浆", ["岩浆", "熔岩", "熔融", "lava"], [BrewEffect.Damage, BrewEffect.Poison, BrewEffect.Frail], false),
        new("燃料", ["燃料", "汽油", "柴油", "煤油", "gasoline", "fuel"], [BrewEffect.Damage, BrewEffect.Poison, BrewEffect.Weak], false),
        new("酸", ["酸液", "强酸", "硫酸", "胃酸", "acid"], [BrewEffect.Poison, BrewEffect.Vulnerable, BrewEffect.Frail], false),
        new("圣水", ["圣水", "holy water"], [BrewEffect.Heal, BrewEffect.Artifact, BrewEffect.Regen], true),
        new("神圣", ["上帝", "神明", "神", "天使", "圣光", "天堂", "信仰", "god", "angel", "divine"], [BrewEffect.Heal, BrewEffect.Artifact, BrewEffect.Regen, BrewEffect.MaxHp], true),
        new("爱", ["爱情", "爱意", "恋", "心动", "思念", "心意", "爱", "love"], [BrewEffect.Regen, BrewEffect.Heal, BrewEffect.Buffer, BrewEffect.Absorption], true),
        new("完美", ["完美", "究极", "极致", "理想", "perfection", "perfect"], [BrewEffect.Artifact, BrewEffect.Strength, BrewEffect.Dexterity, BrewEffect.Block], true),
        new("梦", ["梦境", "梦魇", "幻梦", "幻想", "梦", "dream"], [BrewEffect.DrawCards, BrewEffect.Clarity, BrewEffect.Radiance, BrewEffect.Focus], true),
        new("龙", ["巨龙", "龙血", "龙", "dragon"], [BrewEffect.Strength, BrewEffect.Plating, BrewEffect.Thorns, BrewEffect.Damage], true),
        new("妖精", ["妖精", "精灵", "仙灵", "fairy", "sprite"], [BrewEffect.FairyRevive, BrewEffect.Buffer, BrewEffect.Absorption], true),
        new("恶魔", ["恶魔", "魔鬼", "邪魔", "demon", "devil"], [BrewEffect.Damage, BrewEffect.Doom, BrewEffect.Vulnerable, BrewEffect.Weak], false),
        new("深渊", ["深渊", "虚空", "混沌", "abyss", "void", "chaos"], [BrewEffect.Doom, BrewEffect.Damage, BrewEffect.Poison, BrewEffect.Frail], false),
        new("死亡", ["死亡", "亡者", "终结", "葬", "死", "death"], [BrewEffect.Doom, BrewEffect.Damage, BrewEffect.StrengthDown], false),
        new("时间", ["时间", "永恒", "刹那", "time", "eternity"], [BrewEffect.ExtraTurn, BrewEffect.NextTurnEnergy, BrewEffect.NextTurnDraw], true),
        new("星", ["星辰", "星光", "月光", "月亮", "太阳", "星", "star", "moon", "sun"], [BrewEffect.Radiance, BrewEffect.Clarity, BrewEffect.Focus, BrewEffect.Block], true),
        new("幸运", ["幸运", "好运", "奇迹", "命运", "luck", "fortune"], [BrewEffect.GainEnergy, BrewEffect.DrawCards, BrewEffect.MaxHp], true),
        new("冰", ["寒冰", "冰霜", "霜", "冰雪", "冰", "ice", "frost"], [BrewEffect.Block, BrewEffect.Buffer, BrewEffect.Thorns], true),
        new("雷", ["闪电", "雷霆", "雷", "lightning", "thunder"], [BrewEffect.Damage, BrewEffect.Thorns, BrewEffect.Dexterity], true),
        new("火焰", ["烈焰", "火焰", "炎", "fire", "flame"], [BrewEffect.Damage, BrewEffect.Strength, BrewEffect.Thorns], true)
    ];

    // Tone words. They never pick a family; they only bend the head noun's polarity, so
    // 「恶魔的奶」is a harmful milk and 「天使的毒」is a helpful poison.
    private static readonly string[] DarkWords =
    ["毒", "腐", "烂", "朽", "死", "亡", "尸", "血", "暗", "黑", "诅咒", "恶魔", "魔", "邪", "鬼", "痛苦", "绝望", "恨", "噩", "深渊", "虚空", "恐惧", "悲", "怒", "疯狂", "灾", "祸", "蛇", "蛛", "蛆", "硫", "焦", "熔", "衰", "苦", "伤", "泪", "诅咒"];
    private static readonly string[] LightWords =
    ["圣", "光", "明", "天使", "神", "上帝", "爱", "希望", "幸运", "幸福", "善良", "纯", "洁", "净", "治愈", "生命", "春", "花", "蜜", "甜", "温柔", "完美", "奇迹", "祝福", "恩", "仙", "精灵", "龙", "星", "月", "梦"];

    private static readonly string[] EnemyWords = ["敌人", "敌方", "敌方目标", "enemy", "enemies"];
    private static readonly string[] AllWords = ["全体", "所有", "全部", "all enemies"];

    // Contamination pools. A requested effect that helps the player may smuggle in harm,
    // and a requested effect that hurts the player may smuggle in help.
    private static readonly BrewEffect[] HarmfulToSelf =
        [BrewEffect.Damage, BrewEffect.Weak, BrewEffect.Vulnerable, BrewEffect.Frail, BrewEffect.StrengthDown, BrewEffect.DexterityDown, BrewEffect.Poison];
    private static readonly BrewEffect[] HelpfulToSelf =
        [BrewEffect.Heal, BrewEffect.Block, BrewEffect.Strength, BrewEffect.Dexterity, BrewEffect.Regen, BrewEffect.Artifact, BrewEffect.Buffer];

    // Quantities are decoration: "500 damage" is still one Damage ingredient at the machine's own size.
    // Every extra entry independently rolls one opposite effect: 40%, then +15% per earlier entry (capped 90%).
    private const int BaseContaminationChance = 40;
    private const int ContaminationStep = 15;
    private const int ContaminationCap = 90;
    // Naming something aims the machine; it never commands it. Read these three as the odds of the
    // machine ignoring a specific part of the request, not as feature switches.
    private const int RequestedKeepChance = 70;      // typed effect is actually poured
    private const int FamilyContaminationChance = 35; // a pleasant family still sours one entry

    private static readonly string[] DrinkPrefixes =
    ["请给我一杯", "请给我一瓶", "给我来一杯", "给我来一瓶", "我要一杯", "我要一瓶", "给我一杯", "给我一瓶",
        "来一杯", "来一瓶", "来杯", "来瓶", "一杯", "一瓶", "a cup of ", "a bottle of ", "one cup of ", "cup of "];

    internal static string NormalizeDrinkName(string? input)
    {
        string value=(input??"").Normalize(NormalizationForm.FormKC);
        value=Regex.Replace(value,@"[^\p{L}\p{N}\p{Zs}，。！？、：；,.!?%()+·-]", " ");
        value=Regex.Replace(value,@"\s+", " ").Trim();
        return string.Concat(value.EnumerateRunes().Take(DrinkInputLimit).Select(r=>r.ToString()));
    }
    // 「给我一杯」/「来一瓶」are politeness, not part of the name. The machine should read 「XX的奶」.
    internal static string CoreDrinkName(string name)
    {
        string value=name.Trim(' ','。','.');bool changed=true;
        while(changed)
        {
            changed=false;
            foreach(string prefix in DrinkPrefixes)
            {
                if(value.Length<=prefix.Length||!value.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))continue;
                value=value[prefix.Length..].TrimStart(' ');changed=true;break;
            }
        }
        return value.Trim();
    }
    internal static NamedDrinkRequest ParseDrinkName(string? input)
    {
        string name=CoreDrinkName(NormalizeDrinkName(input));
        if(name.Length==0)return new NamedDrinkRequest("",[],false,false);
        bool enemy=EnemyWords.Any(s=>name.Contains(s,StringComparison.OrdinalIgnoreCase));
        bool all=enemy&&AllWords.Any(s=>name.Contains(s,StringComparison.OrdinalIgnoreCase));
        var effects=MatchEffects(name);
        if(effects.Count>0)return new NamedDrinkRequest(name,effects,enemy,all);
        var family=MatchDrinkFamily(name);
        if(family==null)
        {
            // Nothing recognizable: pick a family from the name itself, so the same nonsense always
            // reads the same way instead of rerolling into white noise.
            var guess=DrinkFamilies[(int)(HashDrinkName(name)%(uint)DrinkFamilies.Length)];
            return new NamedDrinkRequest(name,[],enemy,all,guess.Name,ResolveTone(guess,name));
        }
        return new NamedDrinkRequest(name,[],enemy,all,family.Name,ResolveTone(family,name));
    }
    // Each comma clause carries its own target, so 「恢复自身血量，对所有敌人造成伤害」heals you and
    // hits them, instead of aiming the heal at the enemy.
    private static List<RequestedEffect> MatchEffects(string name)
    {
        var result=new List<RequestedEffect>();
        foreach(string clause in Regex.Split(name,@"[，。、；;,.!?！？：:]+"))
        {
            string text=clause.Trim();if(text.Length==0)continue;
            bool enemy=EnemyWords.Any(word=>text.Contains(word,StringComparison.OrdinalIgnoreCase));
            bool all=enemy&&AllWords.Any(word=>text.Contains(word,StringComparison.OrdinalIgnoreCase));
            foreach(var effect in SelectEffects(text))
                if(!result.Any(item=>item.Effect==effect))result.Add(new RequestedEffect(effect,enemy,all));
        }
        if(result.Any(item=>item.Effect==BrewEffect.HealPercent))result.RemoveAll(item=>item.Effect==BrewEffect.Heal);
        return result.Take(6).ToList();
    }
    private static BrewEffect[] SelectEffects(string text)
    {
        var hits=new List<(int At,int Length,BrewEffect Effect)>();
        foreach(var keyword in DrinkKeywords)foreach(string alias in keyword.Aliases)
        {
            int start=0;
            while(start<text.Length)
            {
                int at=text.IndexOf(alias,start,StringComparison.OrdinalIgnoreCase);if(at<0)break;start=at+alias.Length;
                if(alias.All(c=>c<128)&&((at>0&&char.IsLetter(text[at-1])&&text[at-1]<128)||(start<text.Length&&char.IsLetter(text[start])&&text[start]<128)))continue;
                string prefix=text[Math.Max(0,at-6)..at];
                if(Regex.IsMatch(prefix,@"(?:不要|不想|不需要|不加|别|不要加|without |no )$",RegexOptions.IgnoreCase))continue;
                hits.Add((at,alias.Length,keyword.Effect));
            }
        }
        var percent=Regex.Match(text,@"\d{1,3}\s*%");
        if(percent.Success&&(text.Contains('血')||text.Contains("生命")))hits.Add((percent.Index,1,BrewEffect.HealPercent));
        // A longer phrase wins over its substring, e.g. lowering Strength, percentage heal or random Orbs.
        var selected=new List<(int At,int Length,BrewEffect Effect)>();
        foreach(var hit in hits.OrderByDescending(h=>h.Length).ThenBy(h=>h.At))
            if(!selected.Any(s=>hit.At<s.At+s.Length&&s.At<hit.At+hit.Length))selected.Add(hit);
        return selected.OrderBy(h=>h.At).Select(h=>h.Effect).Distinct().ToArray();
    }
    private static bool IsFavorableRequest(BrewEffect effect,bool enemy) => enemy
        ? PolarityOf(effect)==BrewPolarity.Negative : PolarityOf(effect)==BrewPolarity.Positive;
    private static bool FavorableToPlayer(BrewEffect effect,bool toEnemy) => IsFavorableRequest(effect,toEnemy);
    private static DrinkFamily? MatchDrinkFamily(string name)
    {
        DrinkFamily? best=null;int bestEnd=-1,bestLength=0;
        foreach(var family in DrinkFamilies)foreach(string keyword in family.Keywords)
        {
            int at=name.LastIndexOf(keyword,StringComparison.OrdinalIgnoreCase);if(at<0)continue;
            int end=at+keyword.Length;
            if(end>bestEnd||(end==bestEnd&&keyword.Length>bestLength)){best=family;bestEnd=end;bestLength=keyword.Length;}
        }
        return best;
    }
    private static bool ResolveTone(DrinkFamily family,string name)
    {
        bool positive=family.Positive;
        int dark=CountWords(name,DarkWords),light=CountWords(name,LightWords);
        if(!positive&&light>dark)return true;
        if(positive&&dark>light)return false;
        return positive;
    }
    private static int CountWords(string name,IReadOnlyList<string> words)
    {
        var matched=words.Where(word=>name.Contains(word,StringComparison.OrdinalIgnoreCase)).ToList();
        return matched.Count(word=>!matched.Any(other=>other.Length>word.Length&&other.Contains(word,StringComparison.OrdinalIgnoreCase)));
    }
    private static uint HashDrinkName(string name)
    {
        uint hash=2166136261;
        foreach(byte value in Encoding.UTF8.GetBytes(name)){hash^=value;hash*=16777619;}
        return hash;
    }
    private static BrewEffect[] EffectsOfTone(bool positive) => positive
        ? [BrewEffect.Heal, BrewEffect.HealPercent, BrewEffect.Regen, BrewEffect.Block, BrewEffect.Strength,
            BrewEffect.Dexterity, BrewEffect.Artifact, BrewEffect.Buffer, BrewEffect.DrawCards, BrewEffect.GainEnergy,
            BrewEffect.Absorption, BrewEffect.MaxHp, BrewEffect.Focus]
        : [BrewEffect.Damage, BrewEffect.Weak, BrewEffect.Vulnerable, BrewEffect.Frail, BrewEffect.Poison,
            BrewEffect.StrengthDown, BrewEffect.DexterityDown, BrewEffect.Doom, BrewEffect.Demise, BrewEffect.Shrink];

    internal static NamedDrinkResult BrewNamed(Rng rng,string input)
    {
        if(TryFindCatalogDrink(input,out var original))return BrewCatalogDrink(rng,input,original);
        var request=ParseDrinkName(input);if(request.Name.Length==0)throw new ArgumentException("Empty drink name",nameof(input));
        int density=rng.NextInt(6);
        var ingredients=request.Effects.Count>0?BuildRequestedEffects(rng,request,density):BuildFamilyEffects(rng,request,density);
        string name=string.Concat(request.Name.EnumerateRunes().Take(24).Select(r=>r.ToString()))+(request.Name.EnumerateRunes().Count()>24?"…":"");
        string bottle=ChooseNativeEntry(rng,NativeBottleEntries);
        var recipe=new BrewRecipe(name,ingredients,bottle,LiquidEntries:ChooseLiquidEntries(rng,bottle,ingredients.Count),VisualStyle:RollVisualStyle(rng),VisualSeed:rng.NextInt(int.MaxValue));
        var potion=(SCP294Potion)ModelDb.Potion<SCP294Potion>().ToMutable();AttachRecipe(potion,recipe);
        return new NamedDrinkResult(potion,request);
    }
    // Every named effect is kept, but at the machine's own scale. Each one then rolls once for a single
    // opposite effect, with the odds climbing per entry (40 / 55 / 70 / 85 / 90 ...).
    private static List<BrewIngredient> BuildRequestedEffects(Rng rng,NamedDrinkRequest request,int density)
    {
        var ingredients=new List<BrewIngredient>();
        var used=new HashSet<BrewEffect>(request.Effects.Select(entry=>entry.Effect));
        for(int i=0;i<request.Effects.Count;i++)
        {
            var entry=request.Effects[i];var effect=entry.Effect;
            // A typed effect is a heading, not a promise: sometimes the machine pours a sibling
            // effect from the same side instead of the one that was asked for.
            if(rng.NextInt(100)>=RequestedKeepChance)
            {
                bool askedToHurt=entry.ToEnemy&&!IsPlayerOnlyEffect(effect);
                var side=(IsFavorableRequest(effect,askedToHurt)&&!askedToHurt?EffectsOfTone(true):EffectsOfTone(false))
                    .Where(candidate=>!used.Contains(candidate)&&(!askedToHurt||!IsPlayerOnlyEffect(candidate))).ToArray();
                if(side.Length>0){effect=rng.NextItem(side);used.Add(effect);}
            }
            bool toEnemy=entry.ToEnemy&&!IsPlayerOnlyEffect(effect);
            var recipient=toEnemy?(entry.AllEnemies?BrewRecipient.AllEnemies:BrewRecipient.SelectedEnemy):BrewRecipient.Self;
            var item=MakeIngredient(rng,effect,density,toEnemy?Feeling.Angry:Feeling.Elegant);
            ingredients.Add(item with{Recipient=recipient,Amount=Math.Max(1,RollAmount(rng,effect,density,recipient)),Timing=BrewTiming.Immediate});
            int chance=Math.Min(ContaminationCap,BaseContaminationChance+i*ContaminationStep);
            if(rng.NextInt(100)>=chance)continue;
            var pool=(FavorableToPlayer(effect,toEnemy)?HarmfulToSelf:HelpfulToSelf).Where(candidate=>!used.Contains(candidate)).ToArray();
            if(pool.Length==0)continue;
            var extra=rng.NextItem(pool);used.Add(extra);
            ingredients.Add(new BrewIngredient(extra,Math.Max(1,RollAmount(rng,extra,density,BrewRecipient.Self)),BrewRecipient.Self));
        }
        return ingredients;
    }
    private static List<BrewIngredient> BuildFamilyEffects(Rng rng,NamedDrinkRequest request,int density)
    {
        var family=DrinkFamilies.First(item=>item.Name==request.Family);
        var effects=request.FamilyPositive==family.Positive?family.Effects:EffectsOfTone(request.FamilyPositive);
        bool toEnemy=!request.FamilyPositive||request.Enemy;
        var recipient=toEnemy?(request.AllEnemies?BrewRecipient.AllEnemies:BrewRecipient.SelectedEnemy):BrewRecipient.Self;
        var pool=effects.ToList();
        int count=rng.NextInt(2,Math.Clamp(pool.Count,2,4)+1);
        var ingredients=new List<BrewIngredient>();
        for(int i=0;i<count&&pool.Count>0;i++)
        {
            var effect=rng.NextItem(pool);pool.Remove(effect);
            var item=MakeIngredient(rng,effect,density,Feeling.Elegant);
            ingredients.Add(item with{Recipient=recipient,Amount=Math.Max(1,RollAmount(rng,effect,density,recipient)),Timing=BrewTiming.Immediate});
            // A pleasant family is still not a safe cup: one entry may come out sour.
            if(rng.NextInt(100)>=FamilyContaminationChance)continue;
            bool helpsPlayer=(recipient==BrewRecipient.Self)==(PolarityOf(effect)==BrewPolarity.Positive);
            var sour=(helpsPlayer?HarmfulToSelf:HelpfulToSelf)
                .Where(candidate=>ingredients.All(existing=>existing.Effect!=candidate)).ToArray();
            if(sour.Length==0)continue;
            var bad=rng.NextItem(sour);
            ingredients.Add(new BrewIngredient(bad,Math.Max(1,RollAmount(rng,bad,density,BrewRecipient.Self)),BrewRecipient.Self,BrewTiming.Immediate));
        }
        return ingredients;
    }
}

internal static class Scp294InputText
{
    internal static string Text(string zh,string en,string jp)=>LocManager.Instance.Language switch{"eng"=>en,"jpn"=>jp,_=>zh};
}
