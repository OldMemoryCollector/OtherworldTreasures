using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using OtherworldTreasures.Scripts.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Cards.Alien;

/// <summary>V1.2：30 张完整版本衍生牌，无升级，变回时由 IAlienCard 统一移除。</summary>
public abstract class FourArmsCardBase : ModCardTemplate, IDerivedCard, IAlienCard
{
    protected FourArmsCardBase(int cost, CardType type, CardRarity rarity, TargetType target)
        : base(cost, type, rarity, target, true) { }
    public override int MaxUpgradeLevel => 0;
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => base.AdditionalHoverTips.Concat(AlienCardHoverTips.For(this));
    public override CardAssetProfile AssetProfile => AlienCardArt.Profile(this);
    protected FourArmsPassivePower? Passive => Owner.Creature.GetPower<FourArmsPassivePower>();
    protected decimal Strength => FourArmsPassivePower.Strength(Owner.Creature);
    protected Task Draw(PlayerChoiceContext context, int count) => CardPileCmd.Draw(context, count, Owner);
    protected Task Block(decimal amount, CardPlay play) => CreatureCmd.GainBlock(Owner.Creature, amount, ValueProp.Move, play);
    protected async Task Hit(PlayerChoiceContext context, CardPlay play, decimal damage, int hits = 1, Creature? target = null)
    {
        target ??= play.Target;
        if (target == null) return;
        if (target.IsAlive)
            await DamageCmd.Attack(damage).FromCard(this, play).Targeting(target)
                .WithHitCount(hits).WithHitFx("vfx/vfx_attack_blunt").Execute(context);
    }
    protected async Task HitAll(PlayerChoiceContext context, CardPlay play, decimal damage, int hits = 1)
    {
        if (Owner.Creature.CombatState is not { } combat) return;
        if (combat.Enemies.Any(e => e.IsAlive))
            await DamageCmd.Attack(damage).FromCard(this, play).TargetingAllOpponents(combat)
                .WithHitCount(hits).WithHitFx("vfx/vfx_attack_blunt").Execute(context);
    }
}

// 通用 8 张。
[RegisterCard(typeof(FourArmsCardPool))]
public class FourArmsHeavyPunch : FourArmsCardBase
{
    public FourArmsHeavyPunch() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(12m, ValueProp.Move)];
    protected override Task OnPlay(PlayerChoiceContext c, CardPlay p) => Hit(c, p, DynamicVars.Damage.BaseValue);
}
[RegisterCard(typeof(FourArmsCardPool))]
public class FourArmsDoublePunch : FourArmsCardBase
{
    public FourArmsDoublePunch() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(4m, ValueProp.Move), new DynamicVar("Hits", 2m)];
    protected override Task OnPlay(PlayerChoiceContext c, CardPlay p)
        => Hit(c, p, DynamicVars.Damage.BaseValue, Passive?.DoubledBy(p) == true ? 3 : 2);
}
[RegisterCard(typeof(FourArmsCardPool))]
public class FourArmsQuickJab : FourArmsCardBase
{
    public FourArmsQuickJab() : base(0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(4m, ValueProp.Move), new EnergyVar("EnergyGain", 1)];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        // 官方历史包含变身前的出牌及尚未结束的嵌套出牌，不以变身后的攻击计数判断先手。
        bool first = !CombatManager.Instance.History.CardPlaysStarted.Any(e =>
            e.CardPlay != p && e.CardPlay.Player == Owner && e.HappenedThisTurn(Owner.Creature.CombatState));
        await Hit(c, p, DynamicVars.Damage.BaseValue);
        if (first) await PlayerCmd.GainEnergy(1m, Owner);
    }
}
[RegisterCard(typeof(FourArmsCardPool))]
public class FourArmsSweepingStrike : FourArmsCardBase
{
    public FourArmsSweepingStrike() : base(1, CardType.Attack, CardRarity.Common, TargetType.AllEnemies) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(7m, ValueProp.Move), new DynamicVar("DrawCards", 1m)];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        var enemies = Owner.Creature.CombatState?.Enemies.Where(e => e.IsAlive).ToList() ?? [];
        await HitAll(c, p, DynamicVars.Damage.BaseValue);
        if (enemies.Any(e => e.IsDead)) await Draw(c, 1);
    }
}
[RegisterCard(typeof(FourArmsCardPool))]
public class FourArmsShoulderCharge : FourArmsCardBase
{
    public FourArmsShoulderCharge() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(8m, ValueProp.Move), new DynamicVar("Vulnerable", 1m)];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        if (p.Target == null) return;
        await Hit(c, p, DynamicVars.Damage.BaseValue);
        if (p.Target.IsAlive) await PowerCmd.Apply<VulnerablePower>(c, p.Target, 1m, Owner.Creature, this);
    }
}
[RegisterCard(typeof(FourArmsCardPool))]
public class FourArmsGuard : FourArmsCardBase
{
    public FourArmsGuard() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(16m, ValueProp.Move)];
    protected override Task OnPlay(PlayerChoiceContext c, CardPlay p) => Block(DynamicVars.Block.BaseValue, p);
}
[RegisterCard(typeof(FourArmsCardPool))]
public class FourArmsGrapple : FourArmsCardBase
{
    public FourArmsGrapple() : base(1, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(8m, ValueProp.Move), new DynamicVar("Weak", 1m)];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        await Block(DynamicVars.Block.BaseValue, p);
        if (p.Target is { IsAlive: true }) await PowerCmd.Apply<WeakPower>(c, p.Target, 1m, Owner.Creature, this);
    }
}
[RegisterCard(typeof(FourArmsCardPool))]
public class FourArmsCombatBreathing : FourArmsCardBase
{
    public FourArmsCombatBreathing() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("DrawCards", 1m), new BlockVar(4m, ValueProp.Move)];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        await Draw(c, 1);
        if (Passive?.AttacksThisTurn > 0) await Block(DynamicVars.Block.BaseValue, p);
    }
}

// 狂暴连击 7 张。
[RegisterCard(typeof(FourArmsCardPool))]
public class FourArmsGaleUppercut : FourArmsCardBase
{
    public FourArmsGaleUppercut() : base(0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(5m, ValueProp.Move), new DynamicVar("DrawCards", 1m)];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        bool combo = Passive?.PreviousCardWasAttack == true;
        await Hit(c, p, DynamicVars.Damage.BaseValue);
        if (combo) await Draw(c, 1);
    }
}
[RegisterCard(typeof(FourArmsCardPool))]
public class FourArmsTriplePunch : FourArmsCardBase
{
    public FourArmsTriplePunch() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(3m, ValueProp.Move), new DynamicVar("Hits", 3m)];
    protected override Task OnPlay(PlayerChoiceContext c, CardPlay p) => Hit(c, p, DynamicVars.Damage.BaseValue, 3);
}
[RegisterCard(typeof(FourArmsCardPool))]
public class FourArmsPursuingStrike : FourArmsCardBase
{
    public FourArmsPursuingStrike() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(8m, ValueProp.Move), new EnergyVar("EnergyGain", 1)];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        bool combo = Passive?.PreviousCardWasAttack == true;
        await Hit(c, p, DynamicVars.Damage.BaseValue);
        if (combo) await PlayerCmd.GainEnergy(1m, Owner);
    }
}
[RegisterCard(typeof(FourArmsCardPool))]
public class FourArmsAlternatingPunches : FourArmsCardBase
{
    public FourArmsAlternatingPunches() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(5m, ValueProp.Move), new DynamicVar("Hits", 2m), new DynamicVar("DrawCards", 1m)];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        bool combo = Passive?.AttacksBeforeCurrent >= 2;
        await Hit(c, p, DynamicVars.Damage.BaseValue, 2);
        if (combo) await Draw(c, 1);
    }
}
[RegisterCard(typeof(FourArmsCardPool))]
public class FourArmsComboRhythm : FourArmsCardBase
{
    public FourArmsComboRhythm() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("DrawCards", 1m)];
    protected override Task OnPlay(PlayerChoiceContext c, CardPlay p) => Draw(c, Passive?.AttacksThisTurn >= 2 ? 2 : 1);
}
[RegisterCard(typeof(FourArmsCardPool))]
public class FourArmsRapidOffensive : FourArmsCardBase
{
    public FourArmsRapidOffensive() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("DrawCards", 2m)];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        await Draw(c, 2);
        if (Passive is { } passive) passive.NextAttackFree = true;
    }
}
[RegisterCard(typeof(FourArmsCardPool))]
public class FourArmsFrenzy : FourArmsCardBase
{
    public FourArmsFrenzy() : base(1, CardType.Power, CardRarity.Rare, TargetType.Self) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("DrawCards", 1m), new EnergyVar("EnergyGain", 1)];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
        => await PowerCmd.Apply<FourArmsFrenzyPower>(c, Owner.Creature, 1m, Owner.Creature, this);
}

// 攻守一体 7 张。
[RegisterCard(typeof(FourArmsCardPool))]
public class FourArmsCounterPunch : FourArmsCardBase
{
    public FourArmsCounterPunch() : base(0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(5m, ValueProp.Move), new BlockVar(5m, ValueProp.Move)];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        await Hit(c, p, DynamicVars.Damage.BaseValue);
        if (Owner.Creature.Block > 0) await Block(DynamicVars.Block.BaseValue, p);
    }
}
[RegisterCard(typeof(FourArmsCardPool))]
public class FourArmsElbowChain : FourArmsCardBase
{
    public FourArmsElbowChain() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(3m, ValueProp.Move), new DynamicVar("Hits", 2m), new DynamicVar("Weak", 1m)];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        await Hit(c, p, DynamicVars.Damage.BaseValue, 2);
        if (p.Target is { IsAlive: true }) await PowerCmd.Apply<WeakPower>(c, p.Target, 1m, Owner.Creature, this);
    }
}
[RegisterCard(typeof(FourArmsCardPool))]
public class FourArmsCrossGuard : FourArmsCardBase
{
    public FourArmsCrossGuard() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(12m, ValueProp.Move), new DynamicVar("DrawCards", 1m)];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        await Block(DynamicVars.Block.BaseValue, p);
        if (Passive?.AttacksThisTurn > 0) await Draw(c, 1);
    }
}
[RegisterCard(typeof(FourArmsCardPool))]
public class FourArmsGuardingFootwork : FourArmsCardBase
{
    public FourArmsGuardingFootwork() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(6m, ValueProp.Move), new DynamicVar("DrawCards", 1m)];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        await Block(DynamicVars.Block.BaseValue, p);
        if (Strength >= 4) await Draw(c, 1);
    }
}
[RegisterCard(typeof(FourArmsCardPool))]
public class FourArmsIronSuplex : FourArmsCardBase
{
    public FourArmsIronSuplex() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(14m, ValueProp.Move)];
    protected override Task OnPlay(PlayerChoiceContext c, CardPlay p) => Hit(c, p, DynamicVars.Damage.BaseValue + Owner.Creature.Block);
}
[RegisterCard(typeof(FourArmsCardPool))]
public class FourArmsDefenseToOffense : FourArmsCardBase
{
    public FourArmsDefenseToOffense() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Multiplier", 2m), new DynamicVar("DrawCards", 1m)];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        await Block(System.Math.Max(0, Strength * 2), p);
        await Draw(c, 1);
    }
}
[RegisterCard(typeof(FourArmsCardPool))]
public class FourArmsOffenseDefense : FourArmsCardBase
{
    public FourArmsOffenseDefense() : base(1, CardType.Power, CardRarity.Rare, TargetType.Self) { }
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
        => await PowerCmd.Apply<FourArmsOffenseDefensePower>(c, Owner.Creature, 1m, Owner.Creature, this);
}

// 毁灭重击 7 张。
[RegisterCard(typeof(FourArmsCardPool))]
public class FourArmsRockBreaker : FourArmsCardBase
{
    public FourArmsRockBreaker() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(10m, ValueProp.Move), new DynamicVar("Vulnerable", 1m)];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        if (p.Target == null) return;
        // BeforeCardPlayed 已完成被动翻倍，易伤必须先于本牌伤害。
        if (Strength >= 4) await PowerCmd.Apply<VulnerablePower>(c, p.Target, 1m, Owner.Creature, this);
        await Hit(c, p, DynamicVars.Damage.BaseValue);
    }
}
[RegisterCard(typeof(FourArmsCardPool))]
public class FourArmsGroundClap : FourArmsCardBase
{
    public FourArmsGroundClap() : base(1, CardType.Attack, CardRarity.Common, TargetType.AllEnemies) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(8m, ValueProp.Move), new DynamicVar("Weak", 1m)];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        await HitAll(c, p, DynamicVars.Damage.BaseValue);
        if (Strength >= 8 && Owner.Creature.CombatState is { } combat)
            foreach (var enemy in combat.Enemies.Where(e => e.IsAlive).ToList())
                await PowerCmd.Apply<WeakPower>(c, enemy, 1m, Owner.Creature, this);
    }
}
[RegisterCard(typeof(FourArmsCardPool))]
public class FourArmsMightyThrow : FourArmsCardBase
{
    public FourArmsMightyThrow() : base(2, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(18m, ValueProp.Move), new DynamicVar("SplashDamage", 8m)];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        if (p.Target == null || Owner.Creature.CombatState is not { } combat) return;
        var others = combat.Enemies.Where(e => e.IsAlive && e != p.Target).ToList();
        await Hit(c, p, DynamicVars.Damage.BaseValue);
        foreach (var enemy in others) await Hit(c, p, DynamicVars["SplashDamage"].BaseValue, target: enemy);
    }
}
[RegisterCard(typeof(FourArmsCardPool))]
public class FourArmsConcussiveCharge : FourArmsCardBase
{
    public FourArmsConcussiveCharge() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("DrawCards", 1m), new DynamicVar("BonusDamage", 2m)];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        await Draw(c, 1);
        if (Passive is { } passive) passive.NextAttackDamageBonus += DynamicVars["BonusDamage"].BaseValue;
    }
}
[RegisterCard(typeof(FourArmsCardPool))]
public class FourArmsTitanPunch : FourArmsCardBase
{
    public FourArmsTitanPunch() : base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(16m, ValueProp.Move), new DynamicVar("Multiplier", 2m)];
    protected override Task OnPlay(PlayerChoiceContext c, CardPlay p) => Hit(c, p, DynamicVars.Damage.BaseValue + Strength * 2);
}
[RegisterCard(typeof(FourArmsCardPool))]
public class FourArmsSiegeStance : FourArmsCardBase
{
    public FourArmsSiegeStance() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("DrawCards", 1m)];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        await Draw(c, 1);
        if (Passive is { } passive) passive.NextHeavyAttackDouble = true;
    }
}
[RegisterCard(typeof(FourArmsCardPool))]
public class FourArmsAftershock : FourArmsCardBase
{
    public FourArmsAftershock() : base(1, CardType.Power, CardRarity.Rare, TargetType.Self) { }
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
        => await PowerCmd.Apply<FourArmsAftershockPower>(c, Owner.Creature, 1m, Owner.Creature, this);
}

// 先古：10.14 调整增加保留，不增加消耗。
[RegisterCard(typeof(FourArmsCardPool))]
public class FourArmsAncient : FourArmsCardBase
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain];
    public FourArmsAncient() : base(3, CardType.Attack, CardRarity.Ancient, TargetType.AllEnemies) { }
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(1m, ValueProp.Move), new DynamicVar("Hits", 4m)];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        await FourArmsPassivePower.DoubleStrength(c, Owner.Creature, this);
        await HitAll(c, p, DynamicVars.Damage.BaseValue, 4);
    }
}
