using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
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
// 卡图：所有卡牌使用各自专属素材，由 AlienCardArt 按稳定的类名解析。

// V2.1：完整版本，无升级；保留各卡原有标识和专属图片路径。
public abstract class HeatblastCardBase : ModCardTemplate, IDerivedCard, IAlienCard
{
    protected HeatblastCardBase(int cost, CardType type, CardRarity rarity, TargetType target)
        : base(cost, type, rarity, target, true) { }
    public override int MaxUpgradeLevel => 0;
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => base.AdditionalHoverTips.Concat(AlienCardHoverTips.For(this));
    public override CardAssetProfile AssetProfile => AlienCardArt.Profile(this);
    protected HeatblastPassivePower? Passive => Owner.Creature.GetPower<HeatblastPassivePower>();
    protected Task Draw(PlayerChoiceContext c, int count = 1) => CardPileCmd.Draw(c, count, Owner);
    protected Task Block(CardPlay p, decimal amount) => CreatureCmd.GainBlock(Owner.Creature, amount, ValueProp.Move, p);
    protected async Task Hit(PlayerChoiceContext c, CardPlay p, decimal damage, int hits = 1, Creature? target = null)
    {
        target ??= p.Target;
        if (target is { IsAlive: true })
            await DamageCmd.Attack(damage).FromCard(this, p).Targeting(target).WithHitCount(hits)
                .WithHitFx("vfx/vfx_attack_slash").Execute(c);
    }
    protected async Task HitAll(PlayerChoiceContext c, CardPlay p, decimal damage)
    {
        if (Owner.Creature.CombatState is { } combat && combat.Enemies.Any(e => e.IsAlive))
            await DamageCmd.Attack(damage).FromCard(this, p).TargetingAllOpponents(combat)
                .WithHitFx("vfx/vfx_attack_slash").Execute(c);
    }
    protected async Task ScorchAll(PlayerChoiceContext c, int amount)
    {
        if (Owner.Creature.CombatState is not { } combat) return;
        foreach (var enemy in combat.Enemies.Where(e => e.IsAlive).ToList())
            await AlienScorch.Apply(c, enemy, amount, this);
    }
}

// 通用 8 张。
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastFireball : HeatblastCardBase
{
    public HeatblastFireball() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(8m, ValueProp.Move), new DynamicVar("Scorch", 2m)];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        await Hit(c, p, DynamicVars.Damage.BaseValue);
        if (p.Target is { IsAlive: true }) await AlienScorch.Apply(c, p.Target, 2, this);
    }
}
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastFlameJet : HeatblastCardBase
{
    public HeatblastFlameJet() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(7m, ValueProp.Move), new DynamicVar("DrawCards", 1m)];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        bool scorched = p.Target?.HasPower<AlienScorchPower>() == true;
        await Hit(c, p, DynamicVars.Damage.BaseValue);
        if (scorched) await Draw(c);
    }
}
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastSparkSplash : HeatblastCardBase
{
    public HeatblastSparkSplash() : base(0, CardType.Attack, CardRarity.Common, TargetType.AllEnemies) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(4m, ValueProp.Move)];
    protected override Task OnPlay(PlayerChoiceContext c, CardPlay p) => HitAll(c, p, DynamicVars.Damage.BaseValue);
}
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastMagmaEruption : HeatblastCardBase
{
    public HeatblastMagmaEruption() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Burns", 2m)];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        if (Owner.Creature.CombatState is { } combat) await HeatblastBurns.AddToHand(combat, Owner, 2);
    }
}
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastMagmaArmor : HeatblastCardBase
{
    public HeatblastMagmaArmor() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(8m, ValueProp.Move), new DynamicVar("BonusBlock", 6m)];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        await Block(p, DynamicVars.Block.BaseValue);
        if (await HeatblastStatus.ExhaustSelected(c, Owner, this) > 0) await Block(p, DynamicVars["BonusBlock"].BaseValue);
    }
}
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastFlameDash : HeatblastCardBase
{
    public HeatblastFlameDash() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(8m, ValueProp.Move), new EnergyVar("EnergyGain", 1)];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        bool scorched = p.Target?.HasPower<AlienScorchPower>() == true;
        await Hit(c, p, DynamicVars.Damage.BaseValue);
        if (scorched) await HeatblastEnergy.GainExtra(c, Owner, 1m);
    }
}
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastHeatWave : HeatblastCardBase
{
    public HeatblastHeatWave() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Weak", 1m), new DynamicVar("Scorch", 2m)];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        if (Owner.Creature.CombatState is not { } combat) return;
        foreach (var enemy in combat.Enemies.Where(e => e.IsAlive).ToList())
        {
            await PowerCmd.Apply<WeakPower>(c, enemy, 1m, Owner.Creature, this);
            await AlienScorch.Apply(c, enemy, 2, this);
        }
    }
}
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastSearingAbsorb : HeatblastCardBase
{
    public HeatblastSearingAbsorb() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new EnergyVar("EnergyGain", 1), new DynamicVar("DrawCards", 1m)];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        if (await HeatblastStatus.ExhaustSelected(c, Owner, this) == 0) return;
        await HeatblastEnergy.GainExtra(c, Owner, 1m);
        await Draw(c);
    }
}

// 烈焰爆破 7 张。
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastIgnite : HeatblastCardBase
{
    public HeatblastIgnite() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(7m, ValueProp.Move), new DynamicVar("Scorch", 3m)];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        await Hit(c, p, DynamicVars.Damage.BaseValue);
        if (p.Target is { IsAlive: true }) await AlienScorch.Apply(c, p.Target, 3, this);
    }
}
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastCombustionReaction : HeatblastCardBase
{
    public HeatblastCombustionReaction() : base(0, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Scorch", 2m), new DynamicVar("DrawCards", 1m)];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        if (p.Target != null) await AlienScorch.Apply(c, p.Target, 2, this);
        await Draw(c);
    }
}
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastMagmaBurst : HeatblastCardBase
{
    public HeatblastMagmaBurst() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(8m, ValueProp.Move), new DynamicVar("BonusPerScorch", 2m)];
    protected override Task OnPlay(PlayerChoiceContext c, CardPlay p)
        => Hit(c, p, DynamicVars.Damage.BaseValue + (p.Target?.GetPower<AlienScorchPower>()?.Amount ?? 0) * 2m);
}
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastDetonate : HeatblastCardBase
{
    public HeatblastDetonate() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("DamagePerScorch", 5m)];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        if (p.Target == null) return;
        int stacks = await AlienScorch.Clear(c, p.Target);
        if (stacks > 0) await Hit(c, p, stacks * DynamicVars["DamagePerScorch"].BaseValue);
    }
}
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastChainCombustion : HeatblastCardBase
{
    public HeatblastChainCombustion() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(7m, ValueProp.Move), new DynamicVar("Scorch", 1m)];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        await Hit(c, p, DynamicVars.Damage.BaseValue);
        await ScorchAll(c, 1);
    }
}
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastSupernova : HeatblastCardBase
{
    public HeatblastSupernova() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(10m, ValueProp.Move), new DynamicVar("Scorch", 3m)];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        await HitAll(c, p, DynamicVars.Damage.BaseValue);
        await ScorchAll(c, 3);
    }
}
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastSolarFlare : HeatblastCardBase
{
    public HeatblastSolarFlare() : base(1, CardType.Power, CardRarity.Rare, TargetType.Self) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Bonus", 1m)];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
        => await PowerCmd.Apply<HeatblastSolarFlarePower>(c, Owner.Creature, 1m, Owner.Creature, this);
}

// 熔岩塑形 7 张。
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastFlameWall : HeatblastCardBase
{
    public HeatblastFlameWall() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(8m, ValueProp.Move), new DynamicVar("BonusPerCard", 4m)];
    protected override Task OnPlay(PlayerChoiceContext c, CardPlay p)
        => Block(p, DynamicVars.Block.BaseValue + HeatblastStatus.InHand(Owner).Count * DynamicVars["BonusPerCard"].BaseValue);
}
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastScorchingFeedback : HeatblastCardBase
{
    public HeatblastScorchingFeedback() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6m, ValueProp.Move)];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        if (await HeatblastStatus.ExhaustSelected(c, Owner, this) > 0) await HitAll(c, p, DynamicVars.Damage.BaseValue);
    }
}
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastMagmaFist : HeatblastCardBase
{
    public HeatblastMagmaFist() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(8m, ValueProp.Move), new DynamicVar("BonusDamage", 8m)];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        await Hit(c, p, DynamicVars.Damage.BaseValue);
        if (await HeatblastStatus.ExhaustSelected(c, Owner, this) > 0) await Hit(c, p, DynamicVars["BonusDamage"].BaseValue);
    }
}
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastScorchingDomain : HeatblastCardBase
{
    public HeatblastScorchingDomain() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(8m, ValueProp.Move), new DynamicVar("BonusBlock", 8m)];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        await Block(p, DynamicVars.Block.BaseValue);
        if (Passive?.HasBurnedStatusThisTurn == true) await Block(p, DynamicVars["BonusBlock"].BaseValue);
    }
}
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastEmberBackflow : HeatblastCardBase
{
    public HeatblastEmberBackflow() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("MaxCards", 2m), new DynamicVar("Heal", 4m)];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        int count = await HeatblastStatus.ExhaustSelected(c, Owner, this, 2, upTo: true);
        if (count > 0) await CreatureCmd.Heal(Owner.Creature, count * DynamicVars["Heal"].BaseValue);
    }
}
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastMagmaShaping : HeatblastCardBase
{
    public HeatblastMagmaShaping() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Burns", 2m), new DynamicVar("DrawCards", 1m)];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        if (Owner.Creature.CombatState is { } combat) await HeatblastBurns.AddToHand(combat, Owner, 2);
        await Draw(c);
    }
}
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastScorchingCore : HeatblastCardBase
{
    public HeatblastScorchingCore() : base(1, CardType.Power, CardRarity.Rare, TargetType.Self) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(4m, ValueProp.Unpowered)];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
        => await PowerCmd.Apply<HeatblastScorchingCorePower>(c, Owner.Creature, 1m, Owner.Creature, this);
}

// 喷射推进 7 张。
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastFlamePropulsion : HeatblastCardBase
{
    public HeatblastFlamePropulsion() : base(0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(5m, ValueProp.Move), new EnergyVar("EnergyGain", 1)];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        await Hit(c, p, DynamicVars.Damage.BaseValue);
        if (Passive?.HasBurnedStatusThisTurn == true) await HeatblastEnergy.GainExtra(c, Owner, 1m);
    }
}
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastJetDash : HeatblastCardBase
{
    public HeatblastJetDash() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(9m, ValueProp.Move), new DynamicVar("BonusDamage", 6m)];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        await Hit(c, p, DynamicVars.Damage.BaseValue);
        if (Passive?.HasGainedExtraEnergyThisTurn == true) await Hit(c, p, DynamicVars["BonusDamage"].BaseValue);
    }
}
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastAerialSpray : HeatblastCardBase
{
    public HeatblastAerialSpray() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(4m, ValueProp.Move), new DynamicVar("Hits", 3m)];
    protected override Task OnPlay(PlayerChoiceContext c, CardPlay p) => Hit(c, p, DynamicVars.Damage.BaseValue, 3);
}
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastFlameGlide : HeatblastCardBase
{
    public HeatblastFlameGlide() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(8m, ValueProp.Move), new DynamicVar("DrawCards", 1m)];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        await Block(p, DynamicVars.Block.BaseValue);
        await Draw(c);
    }
}
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastBurstTakeoff : HeatblastCardBase
{
    public HeatblastBurstTakeoff() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new EnergyVar("EnergyGain", 1), new DynamicVar("Burns", 1m)];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        await HeatblastEnergy.GainExtra(c, Owner, 1m);
        if (Owner.Creature.CombatState is { } combat) await HeatblastBurns.AddToHand(combat, Owner, 1);
    }
}
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastMeteorImpact : HeatblastCardBase
{
    public HeatblastMeteorImpact() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(18m, ValueProp.Move), new DynamicVar("BonusDamage", 12m)];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        await Hit(c, p, DynamicVars.Damage.BaseValue);
        if (Passive?.HasGainedExtraEnergyThisTurn == true) await Hit(c, p, DynamicVars["BonusDamage"].BaseValue);
    }
}
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastSupercombustion : HeatblastCardBase
{
    public HeatblastSupercombustion() : base(1, CardType.Power, CardRarity.Rare, TargetType.Self) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("BonusDamage", 5m)];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
        => await PowerCmd.Apply<HeatblastSupercombustionPower>(c, Owner.Creature, 1m, Owner.Creature, this);
}

// 先古：三个专精使用同一效果；10.14 调整增加保留。
[RegisterCard(typeof(HeatblastCardPool))]
public class HeatblastHellfireRain : HeatblastCardBase
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain];
    public HeatblastHellfireRain() : base(2, CardType.Attack, CardRarity.Ancient, TargetType.AllEnemies) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Burns", 2m), new DynamicVar("Scorch", 2m), new DynamicVar("DamagePerScorch", 8m)];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        if (Owner.Creature.CombatState is not { } combat) return;
        await HeatblastBurns.AddToHand(combat, Owner, 2);
        await ScorchAll(c, 2);
        await HeatblastStatus.Exhaust(c, Owner, HeatblastStatus.InHand(Owner));
        foreach (var enemy in combat.Enemies.Where(e => e.IsAlive).ToList())
            await Hit(c, p, (enemy.GetPower<AlienScorchPower>()?.Amount ?? 0) * DynamicVars["DamagePerScorch"].BaseValue, target: enemy);
    }
}
