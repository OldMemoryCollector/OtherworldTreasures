using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using OtherworldTreasures.Scripts.Alien;
using OtherworldTreasures.Scripts.Powers;
using OtherworldTreasures.Scripts.Relics;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Cards.Alien;

// 火焰人（Heatblast）专属牌组：变身期间临时换入，变回时整体移出战斗。
// 全部实现 IAlienCard（变身结束时据此识别并移除）与 IDerivedCard（避免被随机生成）。
// 卡池：全部注册进 HeatblastCardPool（共享卡池），只为在图鉴里单列一类，不进奖励池。
// 卡图：每张用一张原版铁战士卡图，方便辨认。

// ===== 卡图路径（取自原版 ironclad 卡图集）=====
internal static class HeatblastCardArt
{
    private const string Dir = "res://images/atlases/card_atlas.sprites/ironclad/";

    // 通用牌
    internal const string Fireball = Dir + "infernal_blade.tres";
    internal const string FlameJet = Dir + "cinder.tres";
    internal const string SparkSplash = Dir + "strike_ironclad.tres";
    internal const string MagmaEruption = Dir + "inferno.tres";
    internal const string MagmaArmor = Dir + "flame_barrier.tres";
    internal const string FlameDash = Dir + "bludgeon.tres";
    internal const string HeatWave = Dir + "conflagration.tres";
    internal const string SearingAbsorb = Dir + "bloodletting.tres";

    // 烈焰爆破
    internal const string Ignite = Dir + "brand.tres";
    internal const string CombustionReaction = Dir + "stoke.tres";
    internal const string MagmaBurst = Dir + "molten_fist.tres";
    internal const string Detonate = Dir + "tear_asunder.tres";
    internal const string ChainCombustion = Dir + "pillage.tres";
    internal const string SolarFlare = Dir + "pyre.tres";
    internal const string Supernova = Dir + "hellraiser.tres";

    // 熔岩塑形
    internal const string FlameWall = Dir + "stone_armor.tres";
    internal const string ScorchingFeedback = Dir + "spite.tres";
    internal const string MagmaFist = Dir + "mangle.tres";
    internal const string ScorchingDomain = Dir + "demon_form.tres";
    internal const string EmberBackflow = Dir + "feel_no_pain.tres";
    internal const string MagmaShaping = Dir + "dark_embrace.tres";
    internal const string ScorchingCore = Dir + "inflame.tres";

    // 喷射推进
    internal const string FlamePropulsion = Dir + "offering.tres";
    internal const string JetDash = Dir + "twin_strike.tres";
    internal const string AerialSpray = Dir + "pommel_strike.tres";
    internal const string FlameGlide = Dir + "shrug_it_off.tres";
    internal const string BurstTakeoff = Dir + "stomp.tres";
    internal const string Supercombustion = Dir + "juggernaut.tres";
    internal const string MeteorImpact = Dir + "body_slam.tres";

    // 先古：用原版先古卡的卡图（先古卡图在 card_atlas.sprites/event 下，不是角色卡池）
    internal const string HellfireRain = "res://images/atlases/card_atlas.sprites/event/brightest_flame.tres";
}

// 通用牌与专精牌的共同基类：注册到火焰人卡池、标记衍生/外星牌、图鉴可见、不消耗不保留
public abstract class HeatblastCardBase : ModCardTemplate, IDerivedCard, IAlienCard
{
    protected HeatblastCardBase(int energyCost, CardType type, CardRarity rarity, TargetType targetType)
        : base(energyCost, type, rarity, targetType, true) { }
}

// ====================================================================
// 通用牌 8 张
// ====================================================================

// 1. 火球：造成 6 点伤害。给予目标 1 层灼伤。
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastFireball : HeatblastCardBase
{
    public HeatblastFireball() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }
    public override CardAssetProfile AssetProfile => new(PortraitPath: HeatblastCardArt.Fireball);
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(6m, ValueProp.Move),
        new DynamicVar("Scorch", 1m),
    ];
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var target = cardPlay.Target;
        if (target == null) return;
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay).Targeting(target)
            .WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
        await AlienScorch.Apply(choiceContext, target, (int)DynamicVars["Scorch"].BaseValue, this);
    }
}

// 2. 火焰喷射：造成 7 点伤害。给予目标 2 层灼伤。若目标已有灼伤，抽 1 张牌。
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastFlameJet : HeatblastCardBase
{
    public HeatblastFlameJet() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }
    public override CardAssetProfile AssetProfile => new(PortraitPath: HeatblastCardArt.FlameJet);
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(7m, ValueProp.Move),
        new DynamicVar("Scorch", 2m),
        new DynamicVar("DrawCards", 1m),
    ];
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var target = cardPlay.Target;
        if (target == null) return;
        bool hadScorch = target.HasPower<AlienScorchPower>();
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay).Targeting(target)
            .WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
        await AlienScorch.Apply(choiceContext, target, (int)DynamicVars["Scorch"].BaseValue, this);
        if (hadScorch)
        {
            await CardPileCmd.Draw(choiceContext, (int)DynamicVars["DrawCards"].BaseValue, Owner);
        }
    }
}

// 3. 火花飞溅：造成 3 点伤害。抽 1 张牌。若本回合已燃尽过状态牌，额外给予目标 1 层灼伤。
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastSparkSplash : HeatblastCardBase
{
    public HeatblastSparkSplash() : base(0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }
    public override CardAssetProfile AssetProfile => new(PortraitPath: HeatblastCardArt.SparkSplash);
    public override IEnumerable<CardKeyword> CanonicalKeywords => [MyKeywords.Incinerate];
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(3m, ValueProp.Move),
        new DynamicVar("DrawCards", 1m),
        new DynamicVar("BonusScorch", 1m),
    ];
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var target = cardPlay.Target;
        if (target == null) return;
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay).Targeting(target)
            .WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
        await CardPileCmd.Draw(choiceContext, (int)DynamicVars["DrawCards"].BaseValue, Owner);

        var passive = Owner.Creature?.GetPower<HeatblastPassivePower>();
        if (passive != null && passive.HasBurnedStatusThisTurn)
        {
            await AlienScorch.Apply(choiceContext, target, (int)DynamicVars["BonusScorch"].BaseValue, this);
        }
    }
}

// 4. 岩浆喷涌：将 2 张灼伤状态牌加入手牌。
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastMagmaEruption : HeatblastCardBase
{
    public HeatblastMagmaEruption() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self) { }
    public override CardAssetProfile AssetProfile => new(PortraitPath: HeatblastCardArt.MagmaEruption);
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("Burns", 2m),
    ];
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int count = (int)DynamicVars["Burns"].BaseValue;
        var combatState = Owner.Creature?.CombatState;
        if (combatState == null) return;
        await HeatblastBurns.AddToHand(combatState, Owner, count);
    }
}

// 5. 熔岩护甲：燃尽手牌中所有状态牌。获得 5 点格挡。每燃尽 1 张状态牌，额外获得 2 点格挡。
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastMagmaArmor : HeatblastCardBase
{
    public HeatblastMagmaArmor() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }
    public override CardAssetProfile AssetProfile => new(PortraitPath: HeatblastCardArt.MagmaArmor);
    public override IEnumerable<CardKeyword> CanonicalKeywords => [MyKeywords.Incinerate];
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new BlockVar(5m, ValueProp.Move),
        new DynamicVar("BonusPerCard", 2m),
    ];
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var statusCards = HeatblastStatus.InHand(Owner);
        var result = await HeatblastStatus.Burn(choiceContext, Owner, statusCards, cardPlay, fromPassive: false);
        decimal block = DynamicVars.Block.BaseValue + result.Count * DynamicVars["BonusPerCard"].BaseValue;
        await CreatureCmd.GainBlock(Owner.Creature!, block, ValueProp.Move, null);
    }
}

// 6. 火焰冲刺：造成 5 点伤害。若目标已有灼伤，获得 1 点能量；否则给予目标 1 层灼伤。
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastFlameDash : HeatblastCardBase
{
    public HeatblastFlameDash() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }
    public override CardAssetProfile AssetProfile => new(PortraitPath: HeatblastCardArt.FlameDash);
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(5m, ValueProp.Move),
        new DynamicVar("EnergyGain", 1m),
        new DynamicVar("Scorch", 1m),
    ];
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var target = cardPlay.Target;
        if (target == null) return;
        bool hadScorch = target.HasPower<AlienScorchPower>();
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay).Targeting(target)
            .WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
        if (hadScorch)
        {
            await HeatblastEnergy.GainExtra(choiceContext, Owner, DynamicVars["EnergyGain"].BaseValue);
        }
        else
        {
            await AlienScorch.Apply(choiceContext, target, (int)DynamicVars["Scorch"].BaseValue, this);
        }
    }
}

// 7. 热浪：所有敌人获得 1 层虚弱、1 层灼伤。将 1 张灼伤状态牌加入手牌。
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastHeatWave : HeatblastCardBase
{
    public HeatblastHeatWave() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }
    public override CardAssetProfile AssetProfile => new(PortraitPath: HeatblastCardArt.HeatWave);
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("WeakStacks", 1m),
        new DynamicVar("Scorch", 1m),
    ];
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int weak = (int)DynamicVars["WeakStacks"].BaseValue;
        int scorch = (int)DynamicVars["Scorch"].BaseValue;
        var combatState = Owner.Creature?.CombatState;
        if (combatState == null) return;
        foreach (var enemy in combatState.Enemies.Where(e => e.IsAlive).ToList())
        {
            await PowerCmd.Apply<WeakPower>(choiceContext, enemy, weak, Owner.Creature!, this);
            await AlienScorch.Apply(choiceContext, enemy, scorch, this);
        }
        await HeatblastBurns.AddToHand(combatState, Owner, 1);
    }
}

// 8. 炽热吸收：燃尽手牌中 1 张状态牌。若燃尽的是灼伤，获得 1 点能量并获得 5 点格挡。
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastSearingAbsorb : HeatblastCardBase
{
    public HeatblastSearingAbsorb() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }
    public override CardAssetProfile AssetProfile => new(PortraitPath: HeatblastCardArt.SearingAbsorb);
    public override IEnumerable<CardKeyword> CanonicalKeywords => [MyKeywords.Incinerate];
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("EnergyGain", 1m),
        new BlockVar(5m, ValueProp.Move),
    ];
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var statusCards = HeatblastStatus.InHand(Owner);
        if (statusCards.Count == 0) return;
        // 取第一张状态牌燃尽
        var oneCard = new List<CardModel> { statusCards[0] };
        var result = await HeatblastStatus.Burn(choiceContext, Owner, oneCard, cardPlay, fromPassive: false);
        if (result.BurnedAnyBurn)
        {
            await HeatblastEnergy.GainExtra(choiceContext, Owner, DynamicVars["EnergyGain"].BaseValue);
            await CreatureCmd.GainBlock(Owner.Creature!, DynamicVars.Block.BaseValue, ValueProp.Move, null);
        }
    }
}

// ====================================================================
// 专精一：烈焰爆破 7 张
// ====================================================================

// 9. 引燃：造成 5 点伤害。给予目标 2 层灼伤。若目标已有至少 3 层灼伤，额外造成 4 点伤害。
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastIgnite : HeatblastCardBase
{
    public HeatblastIgnite() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }
    public override CardAssetProfile AssetProfile => new(PortraitPath: HeatblastCardArt.Ignite);
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(5m, ValueProp.Move),
        new DynamicVar("Scorch", 2m),
        new DynamicVar("Threshold", 3m),
        new DynamicVar("BonusDamage", 4m),
    ];
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var target = cardPlay.Target;
        if (target == null) return;
        int currentScorch = target.GetPowerAmount<AlienScorchPower>();
        decimal damage = DynamicVars.Damage.BaseValue;
        if (currentScorch >= (int)DynamicVars["Threshold"].BaseValue)
        {
            damage += DynamicVars["BonusDamage"].BaseValue;
        }
        await DamageCmd.Attack(damage)
            .FromCard(this, cardPlay).Targeting(target)
            .WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
        await AlienScorch.Apply(choiceContext, target, (int)DynamicVars["Scorch"].BaseValue, this);
    }
}

// 10. 燃烧反应：本回合下一次给予敌人灼伤时，额外给予 3 层灼伤。抽 1 张牌。
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastCombustionReaction : HeatblastCardBase
{
    public HeatblastCombustionReaction() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }
    public override CardAssetProfile AssetProfile => new(PortraitPath: HeatblastCardArt.CombustionReaction);
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("BonusScorch", 3m),
        new DynamicVar("DrawCards", 1m),
    ];
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var passive = Owner.Creature?.GetPower<HeatblastPassivePower>();
        if (passive != null)
        {
            passive.CombustionReactionBonus += (int)DynamicVars["BonusScorch"].BaseValue;
        }
        await CardPileCmd.Draw(choiceContext, (int)DynamicVars["DrawCards"].BaseValue, Owner);
    }
}

// 11. 熔岩爆裂：造成 10 点伤害。目标每有 1 层灼伤，额外造成 2 点伤害。不会引爆灼伤。
//     若目标没有灼伤，改为造成 13 点伤害。
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastMagmaBurst : HeatblastCardBase
{
    public HeatblastMagmaBurst() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }
    public override CardAssetProfile AssetProfile => new(PortraitPath: HeatblastCardArt.MagmaBurst);
    public override IEnumerable<CardKeyword> CanonicalKeywords => [MyKeywords.Detonate];
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(10m, ValueProp.Move),
        new DynamicVar("BonusPerScorch", 2m),
        new DynamicVar("NoScorchDamage", 13m),
    ];
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var target = cardPlay.Target;
        if (target == null) return;
        int scorch = target.GetPowerAmount<AlienScorchPower>();
        decimal damage = scorch > 0
            ? DynamicVars.Damage.BaseValue + scorch * DynamicVars["BonusPerScorch"].BaseValue
            : DynamicVars["NoScorchDamage"].BaseValue;
        await DamageCmd.Attack(damage)
            .FromCard(this, cardPlay).Targeting(target)
            .WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
    }
}

// 12. 爆燃：引爆目标身上所有灼伤。每引爆 1 层，额外造成 3 点伤害。然后造成 8 点基础伤害。
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastDetonate : HeatblastCardBase
{
    public HeatblastDetonate() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }
    public override CardAssetProfile AssetProfile => new(PortraitPath: HeatblastCardArt.Detonate);
    public override IEnumerable<CardKeyword> CanonicalKeywords => [MyKeywords.Detonate];
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(8m, ValueProp.Move),
        new DynamicVar("BonusPerScorch", 3m),
    ];
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var target = cardPlay.Target;
        if (target == null) return;
        // 先引爆（拿到层数），再结算伤害
        int stacks = await AlienScorch.Detonate(choiceContext, target);
        decimal damage = DynamicVars.Damage.BaseValue + stacks * DynamicVars["BonusPerScorch"].BaseValue;
        await DamageCmd.Attack(damage)
            .FromCard(this, cardPlay).Targeting(target)
            .WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
    }
}

// 13. 连锁燃烧：造成 4 点伤害。给予目标 2 层灼伤。若此次给予后目标达到至少 5 层灼伤，
//     对另一名随机敌人造成 4 点伤害并给予其 1 层灼伤。若场上没有其他敌人，改为获得 3 点格挡。
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastChainCombustion : HeatblastCardBase
{
    public HeatblastChainCombustion() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }
    public override CardAssetProfile AssetProfile => new(PortraitPath: HeatblastCardArt.ChainCombustion);
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(4m, ValueProp.Move),
        new DynamicVar("Scorch", 2m),
        new DynamicVar("Threshold", 5m),
        new DynamicVar("ChainDamage", 4m),
        new DynamicVar("ChainScorch", 1m),
        new DynamicVar("FallbackBlock", 3m),
    ];
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var target = cardPlay.Target;
        if (target == null) return;
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay).Targeting(target)
            .WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
        await AlienScorch.Apply(choiceContext, target, (int)DynamicVars["Scorch"].BaseValue, this);

        int afterScorch = target.GetPowerAmount<AlienScorchPower>();
        if (afterScorch < (int)DynamicVars["Threshold"].BaseValue) return;

        var combatState = Owner.Creature?.CombatState;
        if (combatState == null) return;
        var others = combatState.Enemies.Where(e => e.IsAlive && e != target).ToList();
        if (others.Count > 0)
        {
            var other = others[(int)(Owner.PlayerRng.Transformations.NextDouble() * others.Count)];
            await DamageCmd.Attack(DynamicVars["ChainDamage"].BaseValue)
                .FromCard(this, cardPlay).Targeting(other)
                .WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
            await AlienScorch.Apply(choiceContext, other, (int)DynamicVars["ChainScorch"].BaseValue, this);
        }
        else
        {
            await CreatureCmd.GainBlock(Owner.Creature!, DynamicVars["FallbackBlock"].BaseValue, ValueProp.Move, null);
        }
    }
}

// 14. 太阳耀斑（核心）：每回合首次给予灼伤 +2 层；每回合开始加入 1 张灼伤牌。
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastSolarFlare : HeatblastCardBase
{
    public HeatblastSolarFlare() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self) { }
    public override CardAssetProfile AssetProfile => new(PortraitPath: HeatblastCardArt.SolarFlare);
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("Bonus", 2m),
    ];
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<HeatblastSolarFlarePower>(choiceContext, Owner.Creature!, 1m, Owner.Creature!, this);
    }
}

// 15. 超新星：燃尽手牌中所有状态牌。每燃尽 1 张，对所有敌人造成 8 点伤害。
//     然后清除所有敌人的灼伤。本次合计清除每 2 层灼伤，获得 1 点格挡。
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastSupernova : HeatblastCardBase
{
    public HeatblastSupernova() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies) { }
    public override CardAssetProfile AssetProfile => new(PortraitPath: HeatblastCardArt.Supernova);
    public override IEnumerable<CardKeyword> CanonicalKeywords => [MyKeywords.Incinerate, MyKeywords.Cleanse];
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("DamagePerCard", 8m),
        new DynamicVar("BlockPer2Scorch", 1m),
    ];
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var combatState = Owner.Creature?.CombatState;
        if (combatState == null) return;

        var statusCards = HeatblastStatus.InHand(Owner);
        var result = await HeatblastStatus.Burn(choiceContext, Owner, statusCards, cardPlay, fromPassive: false);
        if (result.Count > 0)
        {
            decimal damage = result.Count * DynamicVars["DamagePerCard"].BaseValue;
            await DamageCmd.Attack(damage)
                .FromCard(this, cardPlay).TargetingAllOpponents(combatState)
                .WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
        }

        // 清除所有敌人灼伤，统计总层数
        int totalCleared = 0;
        foreach (var enemy in combatState.Enemies.Where(e => e.IsAlive).ToList())
        {
            totalCleared += await AlienScorch.Clear(choiceContext, enemy);
        }
        int block = totalCleared / 2 * (int)DynamicVars["BlockPer2Scorch"].BaseValue;
        if (block > 0)
        {
            await CreatureCmd.GainBlock(Owner.Creature!, block, ValueProp.Move, null);
        }
    }
}

// ====================================================================
// 专精二：熔岩塑形 7 张
// ====================================================================

// 9. 炽焰护壁：获得 7 点格挡。燃尽手牌中 1 张灼伤。若燃尽的是灼伤，额外获得 5 点格挡。
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastFlameWall : HeatblastCardBase
{
    public HeatblastFlameWall() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }
    public override CardAssetProfile AssetProfile => new(PortraitPath: HeatblastCardArt.FlameWall);
    public override IEnumerable<CardKeyword> CanonicalKeywords => [MyKeywords.Incinerate];
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new BlockVar(7m, ValueProp.Move),
        new DynamicVar("BonusBlock", 5m),
    ];
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        decimal block = DynamicVars.Block.BaseValue;
        var burns = HeatblastStatus.BurnsInHand(Owner);
        if (burns.Count > 0)
        {
            var oneCard = new List<CardModel> { burns[0] };
            var result = await HeatblastStatus.Burn(choiceContext, Owner, oneCard, cardPlay, fromPassive: false);
            if (result.BurnedAnyBurn)
            {
                block += DynamicVars["BonusBlock"].BaseValue;
            }
        }
        await CreatureCmd.GainBlock(Owner.Creature!, block, ValueProp.Move, null);
    }
}

// 10. 灼热反噬：直到回合结束，每当你燃尽一张状态牌，对所有敌人造成 2 点伤害。每回合最多 4 次。
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastScorchingFeedback : HeatblastCardBase
{
    public HeatblastScorchingFeedback() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }
    public override CardAssetProfile AssetProfile => new(PortraitPath: HeatblastCardArt.ScorchingFeedback);
    public override IEnumerable<CardKeyword> CanonicalKeywords => [MyKeywords.Incinerate];
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("Damage", 2m),
        new DynamicVar("MaxTriggers", 4m),
    ];
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<HeatblastScorchingFeedbackPower>(choiceContext, Owner.Creature!, 1m, Owner.Creature!, this);
    }
}

// 11. 熔岩拳：造成 8 点伤害。燃尽手牌中 1 张状态牌。若燃尽的是灼伤，获得 5 点格挡。
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastMagmaFist : HeatblastCardBase
{
    public HeatblastMagmaFist() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }
    public override CardAssetProfile AssetProfile => new(PortraitPath: HeatblastCardArt.MagmaFist);
    public override IEnumerable<CardKeyword> CanonicalKeywords => [MyKeywords.Incinerate];
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(8m, ValueProp.Move),
        new DynamicVar("Block", 5m),
    ];
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var target = cardPlay.Target;
        if (target == null) return;
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay).Targeting(target)
            .WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);

        var statusCards = HeatblastStatus.InHand(Owner);
        if (statusCards.Count > 0)
        {
            var oneCard = new List<CardModel> { statusCards[0] };
            var result = await HeatblastStatus.Burn(choiceContext, Owner, oneCard, cardPlay, fromPassive: false);
            if (result.BurnedAnyBurn)
            {
                await CreatureCmd.GainBlock(Owner.Creature!, DynamicVars["Block"].BaseValue, ValueProp.Move, null);
            }
        }
    }
}

// 12. 炽热领域：回合结束时，若本回合燃尽过至少 3 张状态牌，获得 7 点格挡。
//     若此时仍有敌人拥有灼伤，额外获得 3 点格挡。
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastScorchingDomain : HeatblastCardBase
{
    public HeatblastScorchingDomain() : base(2, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }
    public override CardAssetProfile AssetProfile => new(PortraitPath: HeatblastCardArt.ScorchingDomain);
    public override IEnumerable<CardKeyword> CanonicalKeywords => [MyKeywords.Incinerate];
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("Block", 7m),
        new DynamicVar("BonusBlock", 3m),
    ];
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<HeatblastScorchingDomainPower>(choiceContext, Owner.Creature!, 1m, Owner.Creature!, this);
    }
}

// 13. 余烬回流：燃尽手牌中所有灼伤。每燃尽 1 张，获得 2 点格挡并回复 1 点生命。
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastEmberBackflow : HeatblastCardBase
{
    public HeatblastEmberBackflow() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }
    public override CardAssetProfile AssetProfile => new(PortraitPath: HeatblastCardArt.EmberBackflow);
    public override IEnumerable<CardKeyword> CanonicalKeywords => [MyKeywords.Incinerate];
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new BlockVar(2m, ValueProp.Move),
        new DynamicVar("Heal", 1m),
    ];
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var burns = HeatblastStatus.BurnsInHand(Owner);
        var result = await HeatblastStatus.Burn(choiceContext, Owner, burns, cardPlay, fromPassive: false);
        if (result.Count > 0)
        {
            await CreatureCmd.GainBlock(Owner.Creature!, result.Count * DynamicVars.Block.BaseValue, ValueProp.Move, null);
            await CreatureCmd.Heal(Owner.Creature!, result.Count * DynamicVars["Heal"].BaseValue);
        }
    }
}

// 14. 熔火塑形：将 2 张灼伤状态牌加入手牌。本回合下一张燃尽状态牌的牌不消耗能量。
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastMagmaShaping : HeatblastCardBase
{
    public HeatblastMagmaShaping() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }
    public override CardAssetProfile AssetProfile => new(PortraitPath: HeatblastCardArt.MagmaShaping);
    public override IEnumerable<CardKeyword> CanonicalKeywords => [MyKeywords.Incinerate];
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("Burns", 2m),
    ];
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        int count = (int)DynamicVars["Burns"].BaseValue;
        var combatState = Owner.Creature?.CombatState;
        if (combatState == null) return;
        await HeatblastBurns.AddToHand(combatState, Owner, count);
        var passive = Owner.Creature?.GetPower<HeatblastPassivePower>();
        if (passive != null)
        {
            passive.NextBurnCardFreeEnergy = true;
        }
    }
}

// 15. 灼热核心（核心）：每回合累计燃尽到第 3 张状态牌时，对所有敌人造成 6 点伤害并获得 6 点格挡。每回合 1 次。
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastScorchingCore : HeatblastCardBase
{
    public HeatblastScorchingCore() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self) { }
    public override CardAssetProfile AssetProfile => new(PortraitPath: HeatblastCardArt.ScorchingCore);
    public override IEnumerable<CardKeyword> CanonicalKeywords => [MyKeywords.Incinerate];
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("Damage", 6m),
        new DynamicVar("Block", 6m),
    ];
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<HeatblastScorchingCorePower>(choiceContext, Owner.Creature!, 1m, Owner.Creature!, this);
    }
}

// ====================================================================
// 专精三：喷射推进 7 张
// ====================================================================

// 9. 火焰推进：燃尽手牌中 1 张灼伤。获得 2 点能量。如果这是本回合第一次燃尽状态牌，抽 1 张牌。
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastFlamePropulsion : HeatblastCardBase
{
    public HeatblastFlamePropulsion() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }
    public override CardAssetProfile AssetProfile => new(PortraitPath: HeatblastCardArt.FlamePropulsion);
    public override IEnumerable<CardKeyword> CanonicalKeywords => [MyKeywords.Incinerate];
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("EnergyGain", 2m),
        new DynamicVar("DrawCards", 1m),
    ];
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var passive = Owner.Creature?.GetPower<HeatblastPassivePower>();
        bool wasFirstBurn = passive != null && passive.BurnedStatusCountThisTurn == 0;

        var burns = HeatblastStatus.BurnsInHand(Owner);
        if (burns.Count > 0)
        {
            var oneCard = new List<CardModel> { burns[0] };
            await HeatblastStatus.Burn(choiceContext, Owner, oneCard, cardPlay, fromPassive: false);
        }

        await HeatblastEnergy.GainExtra(choiceContext, Owner, DynamicVars["EnergyGain"].BaseValue);

        if (wasFirstBurn)
        {
            await CardPileCmd.Draw(choiceContext, (int)DynamicVars["DrawCards"].BaseValue, Owner);
        }
    }
}

// 10. 喷射突进：造成 4 点伤害。若本回合获得过额外能量，额外造成 3 点伤害。
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastJetDash : HeatblastCardBase
{
    public HeatblastJetDash() : base(0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }
    public override CardAssetProfile AssetProfile => new(PortraitPath: HeatblastCardArt.JetDash);
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(4m, ValueProp.Move),
        new DynamicVar("BonusDamage", 3m),
    ];
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var target = cardPlay.Target;
        if (target == null) return;
        decimal damage = DynamicVars.Damage.BaseValue;
        var passive = Owner.Creature?.GetPower<HeatblastPassivePower>();
        if (passive != null && passive.HasGainedExtraEnergyThisTurn)
        {
            damage += DynamicVars["BonusDamage"].BaseValue;
        }
        await DamageCmd.Attack(damage)
            .FromCard(this, cardPlay).Targeting(target)
            .WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
    }
}

// 11. 空中连喷：对同一目标造成 3 次 3 点伤害，每段独立结算格挡。
//     本回合每获得 1 点额外能量，最后一击额外造成 1 点伤害。
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastAerialSpray : HeatblastCardBase
{
    public HeatblastAerialSpray() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }
    public override CardAssetProfile AssetProfile => new(PortraitPath: HeatblastCardArt.AerialSpray);
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(3m, ValueProp.Move),
        new DynamicVar("Hits", 3m),
        new DynamicVar("BonusPerEnergy", 1m),
    ];
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var target = cardPlay.Target;
        if (target == null) return;
        int hits = (int)DynamicVars["Hits"].BaseValue;
        var passive = Owner.Creature?.GetPower<HeatblastPassivePower>();
        int extraEnergy = passive?.ExtraEnergyGainedThisTurn ?? 0;

        for (int i = 0; i < hits; i++)
        {
            decimal damage = DynamicVars.Damage.BaseValue;
            // 最后一击加成本
            if (i == hits - 1)
            {
                damage += extraEnergy * DynamicVars["BonusPerEnergy"].BaseValue;
            }
            await DamageCmd.Attack(damage)
                .FromCard(this, cardPlay).Targeting(target)
                .WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
        }
    }
}

// 12. 火焰滑翔：获得 4 点格挡。抽 1 张牌。若本回合燃尽过灼伤，获得 1 点能量。
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastFlameGlide : HeatblastCardBase
{
    public HeatblastFlameGlide() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self) { }
    public override CardAssetProfile AssetProfile => new(PortraitPath: HeatblastCardArt.FlameGlide);
    public override IEnumerable<CardKeyword> CanonicalKeywords => [MyKeywords.Incinerate];
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new BlockVar(4m, ValueProp.Move),
        new DynamicVar("DrawCards", 1m),
        new DynamicVar("EnergyGain", 1m),
    ];
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(Owner.Creature!, DynamicVars.Block.BaseValue, ValueProp.Move, null);
        await CardPileCmd.Draw(choiceContext, (int)DynamicVars["DrawCards"].BaseValue, Owner);

        var passive = Owner.Creature?.GetPower<HeatblastPassivePower>();
        // "燃尽过灼伤" = 本回合燃尽过至少 1 张灼伤牌（简化为燃尽过状态牌，因为灼伤是主要状态牌来源）
        if (passive != null && passive.HasBurnedStatusThisTurn)
        {
            await HeatblastEnergy.GainExtra(choiceContext, Owner, DynamicVars["EnergyGain"].BaseValue);
        }
    }
}

// 13. 爆裂起飞：获得 1 点能量。抽 1 张牌。将 1 张灼伤状态牌加入手牌。
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastBurstTakeoff : HeatblastCardBase
{
    public HeatblastBurstTakeoff() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }
    public override CardAssetProfile AssetProfile => new(PortraitPath: HeatblastCardArt.BurstTakeoff);
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("EnergyGain", 1m),
        new DynamicVar("DrawCards", 1m),
    ];
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await HeatblastEnergy.GainExtra(choiceContext, Owner, DynamicVars["EnergyGain"].BaseValue);
        await CardPileCmd.Draw(choiceContext, (int)DynamicVars["DrawCards"].BaseValue, Owner);

        var combatState = Owner.Creature?.CombatState;
        if (combatState == null) return;
        await HeatblastBurns.AddToHand(combatState, Owner, 1);
    }
}

// 14. 超燃加速（核心）：本回合每当获得额外能量，下一次攻击 +3 伤害。每回合最多 3 次。
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastSupercombustion : HeatblastCardBase
{
    public HeatblastSupercombustion() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self) { }
    public override CardAssetProfile AssetProfile => new(PortraitPath: HeatblastCardArt.Supercombustion);
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DynamicVar("BonusDamage", 3m),
        new DynamicVar("MaxTriggers", 3m),
    ];
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<HeatblastSupercombustionPower>(choiceContext, Owner.Creature!, 1m, Owner.Creature!, this);
    }
}

// 15. 流星冲击：造成 14 点伤害。本回合每打出一张 0 费牌，额外造成 3 点伤害。
//     若本回合累计获得至少 3 点额外能量，给予目标 2 层灼伤。
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastMeteorImpact : HeatblastCardBase
{
    public HeatblastMeteorImpact() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }
    public override CardAssetProfile AssetProfile => new(PortraitPath: HeatblastCardArt.MeteorImpact);
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(14m, ValueProp.Move),
        new DynamicVar("DamagePerZeroCost", 3m),
        new DynamicVar("EnergyThreshold", 3m),
        new DynamicVar("Scorch", 2m),
    ];
    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var target = cardPlay.Target;
        if (target == null) return;
        var passive = Owner.Creature?.GetPower<HeatblastPassivePower>();
        int zeroCosts = passive?.ZeroCostCardsPlayedThisTurn ?? 0;
        decimal damage = DynamicVars.Damage.BaseValue + zeroCosts * DynamicVars["DamagePerZeroCost"].BaseValue;
        await DamageCmd.Attack(damage)
            .FromCard(this, cardPlay).Targeting(target)
            .WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);

        if (passive != null && passive.ExtraEnergyGainedThisTurn >= (int)DynamicVars["EnergyThreshold"].BaseValue)
        {
            await AlienScorch.Apply(choiceContext, target, (int)DynamicVars["Scorch"].BaseValue, this);
        }
    }
}

// ====================================================================
// 专属先古：炎狱天降
// ====================================================================

// 30. 炎狱天降：3 费｜先古｜攻击｜保留｜消耗
// 对所有敌人造成 24 点伤害。引爆所有敌人身上的灼伤。每合计引爆 1 层，额外造成 5 点伤害。
// 然后按流派获得额外效果，最后将 3 张灼伤状态牌加入手牌。
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastHellfireRain : ModCardTemplate, IDerivedCard, IAlienCard
{
    private const int energyCost = 3;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Ancient;
    private const TargetType targetType = TargetType.AllEnemies;
    private const bool shouldShowInCardLibrary = true;

    public HeatblastHellfireRain() : base(energyCost, type, rarity, targetType, shouldShowInCardLibrary) { }

    // 先古卡面风格
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: HeatblastCardArt.HellfireRain,
        VisualStyle: CardVisualStyle.Ancient);

    // 消耗 + 保留
    public override IEnumerable<CardKeyword> CanonicalKeywords => [
        CardKeyword.Exhaust,
        CardKeyword.Retain,
        MyKeywords.Detonate,
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(24m, ValueProp.Move),
        new DynamicVar("BonusPerScorch", 5m),
        new DynamicVar("Burns", 3m),
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var combatState = Owner.Creature?.CombatState;
        if (combatState == null) return;

        // ① 对所有敌人造成基础伤害
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay).TargetingAllOpponents(combatState)
            .WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);

        // ② 引爆所有敌人灼伤，合计层数
        int totalDetonated = 0;
        foreach (var enemy in combatState.Enemies.Where(e => e.IsAlive).ToList())
        {
            totalDetonated += await AlienScorch.Detonate(choiceContext, enemy);
        }

        // 每合计引爆 1 层，额外造成 5 点伤害（全体）
        if (totalDetonated > 0)
        {
            decimal bonusDamage = totalDetonated * DynamicVars["BonusPerScorch"].BaseValue;
            await DamageCmd.Attack(bonusDamage)
                .FromCard(this, cardPlay).TargetingAllOpponents(combatState)
                .WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
        }

        // ③ 按流派获得额外效果
        var passive = Owner.Creature?.GetPower<HeatblastPassivePower>();
        var spec = passive?.CurrentSpec ?? AlienSpecialization.Blast;
        switch (spec)
        {
            case AlienSpecialization.Blast:
                // 每引爆 1 层，额外造成 2 点伤害
                if (totalDetonated > 0)
                {
                    decimal blastBonus = totalDetonated * 2m;
                    await DamageCmd.Attack(blastBonus)
                        .FromCard(this, cardPlay).TargetingAllOpponents(combatState)
                        .WithHitFx("vfx/vfx_attack_slash").Execute(choiceContext);
                }
                break;
            case AlienSpecialization.Shaping:
                // 每引爆 1 层，获得 1 点格挡
                if (totalDetonated > 0)
                {
                    await CreatureCmd.GainBlock(Owner.Creature!, totalDetonated, ValueProp.Move, null);
                }
                break;
            case AlienSpecialization.Propulsion:
                // 每引爆 2 层，获得 1 点能量（向下取整）
                int energyGain = totalDetonated / 2;
                if (energyGain > 0)
                {
                    await HeatblastEnergy.GainExtra(choiceContext, Owner, energyGain);
                }
                break;
        }

        // ④ 将 3 张灼伤状态牌加入手牌
        int burnCount = (int)DynamicVars["Burns"].BaseValue;
        await HeatblastBurns.AddToHand(combatState, Owner, burnCount);
    }
}
