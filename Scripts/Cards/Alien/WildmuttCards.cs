using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using OtherworldTreasures.Scripts.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using Godot;

namespace OtherworldTreasures.Scripts.Cards.Alien;

[RegisterSharedCardPool]
public class WildmuttCardPool : TypeListCardPoolModel
{
    public override string Title => "wildmutt";
    public override string EnergyColorName => "ironclad";
    public override string CardFrameMaterialPath => "card_frame_red";
    public override Color DeckEntryCardColor => new("D62000");
    public override Color EnergyOutlineColor => new("802020");
    public override bool IsColorless => false;
}
public enum WildmuttCardKind { SwiftClaw, FangBite, SavagePounce, QuadrupedSweep, GroundRush, ScentTrail, AgileDodge, BeastGrowl, AirbornePounce, WallClimbStrike, RapidReturn, HighDive, VaultDodge, Parkour, LeapingInstinct, BlindSpotClaw, WindCounter, HeatPursuit, GillSniff, SoundLocation, TasteTrail, ThermalSense, RendingClaws, ThroatBite, BonePounce, TripleBite, BloodScent, HuntCharge, FeedingFrenzy, PrimalHunt }
internal readonly record struct WildmuttCardSpec(int Cost, CardType Type, CardRarity Rarity, TargetType Target, decimal Damage, decimal Block);
internal static class WildmuttCards
{
    internal static WildmuttCardSpec Spec(WildmuttCardKind kind) => kind switch
    {
        WildmuttCardKind.SwiftClaw => new(0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy, 4m, 0m),
        WildmuttCardKind.FangBite => new(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy, 8m, 0m),
        WildmuttCardKind.SavagePounce => new(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy, 9m, 4m),
        WildmuttCardKind.QuadrupedSweep => new(1, CardType.Attack, CardRarity.Common, TargetType.AllEnemies, 6m, 0m),
        WildmuttCardKind.GroundRush => new(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy, 7m, 0m),
        WildmuttCardKind.ScentTrail => new(0, CardType.Skill, CardRarity.Common, TargetType.Self, 0m, 0m),
        WildmuttCardKind.AgileDodge => new(1, CardType.Skill, CardRarity.Common, TargetType.Self, 0m, 10m),
        WildmuttCardKind.BeastGrowl => new(1, CardType.Skill, CardRarity.Common, TargetType.Self, 0m, 0m),
        WildmuttCardKind.AirbornePounce => new(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy, 10m, 0m),
        WildmuttCardKind.WallClimbStrike => new(0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy, 4m, 0m),
        WildmuttCardKind.RapidReturn => new(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy, 7m, 0m),
        WildmuttCardKind.HighDive => new(2, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy, 16m, 0m),
        WildmuttCardKind.VaultDodge => new(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self, 0m, 7m),
        WildmuttCardKind.Parkour => new(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self, 0m, 5m),
        WildmuttCardKind.LeapingInstinct => new(1, CardType.Power, CardRarity.Rare, TargetType.Self, 0m, 6m),
        WildmuttCardKind.BlindSpotClaw => new(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy, 8m, 0m),
        WildmuttCardKind.WindCounter => new(0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy, 5m, 6m),
        WildmuttCardKind.HeatPursuit => new(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy, 8m, 0m),
        WildmuttCardKind.GillSniff => new(0, CardType.Skill, CardRarity.Common, TargetType.Self, 0m, 0m),
        WildmuttCardKind.SoundLocation => new(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self, 0m, 6m),
        WildmuttCardKind.TasteTrail => new(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self, 0m, 0m),
        WildmuttCardKind.ThermalSense => new(1, CardType.Power, CardRarity.Rare, TargetType.Self, 0m, 8m),
        WildmuttCardKind.RendingClaws => new(0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy, 4m, 0m),
        WildmuttCardKind.ThroatBite => new(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy, 10m, 0m),
        WildmuttCardKind.BonePounce => new(2, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy, 16m, 0m),
        WildmuttCardKind.TripleBite => new(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy, 4m, 0m),
        WildmuttCardKind.BloodScent => new(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self, 0m, 0m),
        WildmuttCardKind.HuntCharge => new(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self, 0m, 0m),
        WildmuttCardKind.FeedingFrenzy => new(1, CardType.Power, CardRarity.Rare, TargetType.Self, 0m, 0m),
        WildmuttCardKind.PrimalHunt => new(2, CardType.Attack, CardRarity.Ancient, TargetType.Self, 5m, 0m),
        _ => throw new System.ArgumentOutOfRangeException(nameof(kind)),
    };
}
public abstract class WildmuttCardBase : AlienCardBase
{
    protected abstract WildmuttCardKind Kind { get; }
    protected WildmuttCardBase(WildmuttCardKind kind) : base(WildmuttCards.Spec(kind).Cost, WildmuttCards.Spec(kind).Type, WildmuttCards.Spec(kind).Rarity, WildmuttCards.Spec(kind).Target) { }
    protected WildmuttPassivePower? Hunt => Owner.Creature.GetPower<WildmuttPassivePower>();
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(WildmuttCards.Spec(Kind).Damage, ValueProp.Move),
        new BlockVar(WildmuttCards.Spec(Kind).Block, ValueProp.Move),
        new EnergyVar("EnergyGain", 1),
    ];
    public override CardAssetProfile AssetProfile => AlienCardArt.Profile(this);
    public override IEnumerable<CardKeyword> CanonicalKeywords => Rarity == CardRarity.Ancient ? [CardKeyword.Retain]
        : Kind == WildmuttCardKind.WallClimbStrike ? [CardKeyword.Exhaust] : [];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        if (Hunt is { } hunt) await hunt.RefreshPrey(c);
        bool prey = Hunt?.IsPrey(p.Target) == true;
        bool attacks = Hunt?.PreyAttacks == true;
        bool belowHalf = Hunt?.PreyBelowHalf == true;
        decimal damage = DynamicVars.Damage.BaseValue;
        switch (Kind)
        {
            case WildmuttCardKind.SwiftClaw:
                await Hit(c,p,p.Target,damage); break;
            case WildmuttCardKind.FangBite:
                await Hit(c,p,p.Target,damage);
                if(p.Target is { IsAlive: true }) await PowerCmd.Apply<WeakPower>(c,p.Target,1m,Owner.Creature,this); break;
            case WildmuttCardKind.SavagePounce:
                await Hit(c,p,p.Target,damage); if(prey) await Block(p,4m); break;
            case WildmuttCardKind.WallClimbStrike:
                await Hit(c,p,p.Target,damage); if(prey) await Draw(c); break;
            case WildmuttCardKind.QuadrupedSweep:
                await HitAll(c,p,damage); break;
            case WildmuttCardKind.GroundRush:
                await Hit(c,p,p.Target,damage); if(Hunt is { } rush) rush.NextSkillReduced=true; break;
            case WildmuttCardKind.ScentTrail:
                await Draw(c,belowHalf?2:1); break;
            case WildmuttCardKind.BloodScent:
                if(Hunt is { } blood) blood.NextPreyBonusDouble=true; break;
            case WildmuttCardKind.AgileDodge:
                await Block(p,DynamicVars.Block.BaseValue+(attacks?4m:0m)); break;
            case WildmuttCardKind.BeastGrowl:
                if(Hunt?.Prey is { } growl) await PowerCmd.Apply<WeakPower>(c,growl,2m,Owner.Creature,this);
                await Draw(c); break;
            case WildmuttCardKind.AirbornePounce:
                await Hit(c,p,p.Target,damage); if(prey) await Draw(c); break;
            case WildmuttCardKind.RapidReturn:
                await Hit(c,p,p.Target,damage); if(p.Target?.IsDead==true) await PlayerCmd.GainEnergy(1m,Owner); break;
            case WildmuttCardKind.HighDive:
                await Hit(c,p,p.Target,damage+(Hunt?.PlayedSkillThisTurn==true?7m:0m)); break;
            case WildmuttCardKind.VaultDodge:
                await Block(p,DynamicVars.Block.BaseValue); if(Hunt is { } vault) vault.NextAttackReduced=true; break;
            case WildmuttCardKind.Parkour:
                await Draw(c,2); await Block(p,DynamicVars.Block.BaseValue); break;
            case WildmuttCardKind.LeapingInstinct:
                await PowerCmd.Apply<WildmuttLeapingInstinctPower>(c,Owner.Creature,1m,Owner.Creature,this); break;
            case WildmuttCardKind.BlindSpotClaw:
                if(prey && p.Target!=null) await PowerCmd.Apply<VulnerablePower>(c,p.Target,1m,Owner.Creature,this);
                await Hit(c,p,p.Target,damage); break;
            case WildmuttCardKind.WindCounter:
                await Hit(c,p,p.Target,damage); if(attacks) await Block(p,6m); break;
            case WildmuttCardKind.HeatPursuit:
                await Hit(c,p,p.Target,damage+(Hunt?.Prey!=null && !attacks?6m:0m)); break;
            case WildmuttCardKind.GillSniff:
                await Draw(c,2);
                var discarded = (await CardSelectCmd.FromHandForDiscard(c,Owner,new CardSelectorPrefs(CardSelectorPrefs.DiscardSelectionPrompt,1),null,this)).FirstOrDefault();
                if(discarded!=null) await CardCmd.Discard(c,discarded); break;
            case WildmuttCardKind.SoundLocation:
                var selected = (await CardSelectCmd.FromCombatPile(c,Owner.PlayerCombatState!.DrawPile,
                    Owner,new CardSelectorPrefs(new LocString("cards",
                        "OTHERWORLD_TREASURES_CARD_WILDMUTT_SOUND_LOCATION.selectionScreenPrompt"),1),card=>card.Type==CardType.Attack)).FirstOrDefault();
                if(selected!=null) await CardPileCmd.Add(selected,PileType.Hand);
                await Block(p,DynamicVars.Block.BaseValue); break;
            case WildmuttCardKind.TasteTrail:
                bool wounded=Hunt?.Prey is { } tasted && tasted.CurrentHp<tasted.MaxHp;
                await Draw(c);
                if(wounded) await PlayerCmd.GainEnergy(1m,Owner); break;
            case WildmuttCardKind.ThermalSense:
                await Block(p,8m); await PowerCmd.Apply<WildmuttThermalSensePower>(c,Owner.Creature,1m,Owner.Creature,this); break;
            case WildmuttCardKind.RendingClaws:
                await Hit(c,p,p.Target,damage+(belowHalf?3m:0m)); break;
            case WildmuttCardKind.ThroatBite:
                await Hit(c,p,p.Target,damage,piercing:prey); break;
            case WildmuttCardKind.BonePounce:
                await Hit(c,p,p.Target,damage+(p.Target?.HasPower<WeakPower>()==true?5m:0m)); break;
            case WildmuttCardKind.TripleBite:
                await Hit(c,p,p.Target,damage,3); break;
            case WildmuttCardKind.HuntCharge:
                if(Hunt?.Prey is { } charged) await PowerCmd.Apply<VulnerablePower>(c,charged,2m,Owner.Creature,this);
                await Draw(c); break;
            case WildmuttCardKind.FeedingFrenzy:
                await PowerCmd.Apply<WildmuttFeedingFrenzyPower>(c,Owner.Creature,1m,Owner.Creature,this); break;
            case WildmuttCardKind.PrimalHunt:
                for(int repeat=0;repeat<3;repeat++)
                {
                    var target=Hunt?.Prey;
                    if(target==null) break;
                    await Hit(c,p,target,damage,3);
                    if(!target.IsDead) break;
                    if(Hunt is { } next) await next.RefreshPrey(c);
                }
                break;
        }
    }
}
[RegisterCard(typeof(WildmuttCardPool))]
public class WildmuttSwiftClaw : WildmuttCardBase
{
    protected override WildmuttCardKind Kind => WildmuttCardKind.SwiftClaw;
    public WildmuttSwiftClaw() : base(WildmuttCardKind.SwiftClaw) { }
}
[RegisterCard(typeof(WildmuttCardPool))]
public class WildmuttFangBite : WildmuttCardBase
{
    protected override WildmuttCardKind Kind => WildmuttCardKind.FangBite;
    public WildmuttFangBite() : base(WildmuttCardKind.FangBite) { }
}
[RegisterCard(typeof(WildmuttCardPool))]
public class WildmuttSavagePounce : WildmuttCardBase
{
    protected override WildmuttCardKind Kind => WildmuttCardKind.SavagePounce;
    public WildmuttSavagePounce() : base(WildmuttCardKind.SavagePounce) { }
}
[RegisterCard(typeof(WildmuttCardPool))]
public class WildmuttQuadrupedSweep : WildmuttCardBase
{
    protected override WildmuttCardKind Kind => WildmuttCardKind.QuadrupedSweep;
    public WildmuttQuadrupedSweep() : base(WildmuttCardKind.QuadrupedSweep) { }
}
[RegisterCard(typeof(WildmuttCardPool))]
public class WildmuttGroundRush : WildmuttCardBase
{
    protected override WildmuttCardKind Kind => WildmuttCardKind.GroundRush;
    public WildmuttGroundRush() : base(WildmuttCardKind.GroundRush) { }
}
[RegisterCard(typeof(WildmuttCardPool))]
public class WildmuttScentTrail : WildmuttCardBase
{
    protected override WildmuttCardKind Kind => WildmuttCardKind.ScentTrail;
    public WildmuttScentTrail() : base(WildmuttCardKind.ScentTrail) { }
}
[RegisterCard(typeof(WildmuttCardPool))]
public class WildmuttAgileDodge : WildmuttCardBase
{
    protected override WildmuttCardKind Kind => WildmuttCardKind.AgileDodge;
    public WildmuttAgileDodge() : base(WildmuttCardKind.AgileDodge) { }
}
[RegisterCard(typeof(WildmuttCardPool))]
public class WildmuttBeastGrowl : WildmuttCardBase
{
    protected override WildmuttCardKind Kind => WildmuttCardKind.BeastGrowl;
    public WildmuttBeastGrowl() : base(WildmuttCardKind.BeastGrowl) { }
}
[RegisterCard(typeof(WildmuttCardPool))]
public class WildmuttAirbornePounce : WildmuttCardBase
{
    protected override WildmuttCardKind Kind => WildmuttCardKind.AirbornePounce;
    public WildmuttAirbornePounce() : base(WildmuttCardKind.AirbornePounce) { }
}
[RegisterCard(typeof(WildmuttCardPool))]
public class WildmuttWallClimbStrike : WildmuttCardBase
{
    protected override WildmuttCardKind Kind => WildmuttCardKind.WallClimbStrike;
    public WildmuttWallClimbStrike() : base(WildmuttCardKind.WallClimbStrike) { }
}
[RegisterCard(typeof(WildmuttCardPool))]
public class WildmuttRapidReturn : WildmuttCardBase
{
    protected override WildmuttCardKind Kind => WildmuttCardKind.RapidReturn;
    public WildmuttRapidReturn() : base(WildmuttCardKind.RapidReturn) { }
}
[RegisterCard(typeof(WildmuttCardPool))]
public class WildmuttHighDive : WildmuttCardBase
{
    protected override WildmuttCardKind Kind => WildmuttCardKind.HighDive;
    public WildmuttHighDive() : base(WildmuttCardKind.HighDive) { }
}
[RegisterCard(typeof(WildmuttCardPool))]
public class WildmuttVaultDodge : WildmuttCardBase
{
    protected override WildmuttCardKind Kind => WildmuttCardKind.VaultDodge;
    public WildmuttVaultDodge() : base(WildmuttCardKind.VaultDodge) { }
}
[RegisterCard(typeof(WildmuttCardPool))]
public class WildmuttParkour : WildmuttCardBase
{
    protected override WildmuttCardKind Kind => WildmuttCardKind.Parkour;
    public WildmuttParkour() : base(WildmuttCardKind.Parkour) { }
}
[RegisterCard(typeof(WildmuttCardPool))]
public class WildmuttLeapingInstinct : WildmuttCardBase
{
    protected override WildmuttCardKind Kind => WildmuttCardKind.LeapingInstinct;
    public WildmuttLeapingInstinct() : base(WildmuttCardKind.LeapingInstinct) { }
}
[RegisterCard(typeof(WildmuttCardPool))]
public class WildmuttBlindSpotClaw : WildmuttCardBase
{
    protected override WildmuttCardKind Kind => WildmuttCardKind.BlindSpotClaw;
    public WildmuttBlindSpotClaw() : base(WildmuttCardKind.BlindSpotClaw) { }
}
[RegisterCard(typeof(WildmuttCardPool))]
public class WildmuttWindCounter : WildmuttCardBase
{
    protected override WildmuttCardKind Kind => WildmuttCardKind.WindCounter;
    public WildmuttWindCounter() : base(WildmuttCardKind.WindCounter) { }
}
[RegisterCard(typeof(WildmuttCardPool))]
public class WildmuttHeatPursuit : WildmuttCardBase
{
    protected override WildmuttCardKind Kind => WildmuttCardKind.HeatPursuit;
    public WildmuttHeatPursuit() : base(WildmuttCardKind.HeatPursuit) { }
}
[RegisterCard(typeof(WildmuttCardPool))]
public class WildmuttGillSniff : WildmuttCardBase
{
    protected override WildmuttCardKind Kind => WildmuttCardKind.GillSniff;
    public WildmuttGillSniff() : base(WildmuttCardKind.GillSniff) { }
}
[RegisterCard(typeof(WildmuttCardPool))]
public class WildmuttSoundLocation : WildmuttCardBase
{
    protected override WildmuttCardKind Kind => WildmuttCardKind.SoundLocation;
    public WildmuttSoundLocation() : base(WildmuttCardKind.SoundLocation) { }
}
[RegisterCard(typeof(WildmuttCardPool))]
public class WildmuttTasteTrail : WildmuttCardBase
{
    protected override WildmuttCardKind Kind => WildmuttCardKind.TasteTrail;
    public WildmuttTasteTrail() : base(WildmuttCardKind.TasteTrail) { }
}
[RegisterCard(typeof(WildmuttCardPool))]
public class WildmuttThermalSense : WildmuttCardBase
{
    protected override WildmuttCardKind Kind => WildmuttCardKind.ThermalSense;
    public WildmuttThermalSense() : base(WildmuttCardKind.ThermalSense) { }
}
[RegisterCard(typeof(WildmuttCardPool))]
public class WildmuttRendingClaws : WildmuttCardBase
{
    protected override WildmuttCardKind Kind => WildmuttCardKind.RendingClaws;
    public WildmuttRendingClaws() : base(WildmuttCardKind.RendingClaws) { }
}
[RegisterCard(typeof(WildmuttCardPool))]
public class WildmuttThroatBite : WildmuttCardBase
{
    protected override WildmuttCardKind Kind => WildmuttCardKind.ThroatBite;
    public WildmuttThroatBite() : base(WildmuttCardKind.ThroatBite) { }
}
[RegisterCard(typeof(WildmuttCardPool))]
public class WildmuttBonePounce : WildmuttCardBase
{
    protected override WildmuttCardKind Kind => WildmuttCardKind.BonePounce;
    public WildmuttBonePounce() : base(WildmuttCardKind.BonePounce) { }
}
[RegisterCard(typeof(WildmuttCardPool))]
public class WildmuttTripleBite : WildmuttCardBase
{
    protected override WildmuttCardKind Kind => WildmuttCardKind.TripleBite;
    public WildmuttTripleBite() : base(WildmuttCardKind.TripleBite) { }
}
[RegisterCard(typeof(WildmuttCardPool))]
public class WildmuttBloodScent : WildmuttCardBase
{
    protected override WildmuttCardKind Kind => WildmuttCardKind.BloodScent;
    public WildmuttBloodScent() : base(WildmuttCardKind.BloodScent) { }
}
[RegisterCard(typeof(WildmuttCardPool))]
public class WildmuttHuntCharge : WildmuttCardBase
{
    protected override WildmuttCardKind Kind => WildmuttCardKind.HuntCharge;
    public WildmuttHuntCharge() : base(WildmuttCardKind.HuntCharge) { }
}
[RegisterCard(typeof(WildmuttCardPool))]
public class WildmuttFeedingFrenzy : WildmuttCardBase
{
    protected override WildmuttCardKind Kind => WildmuttCardKind.FeedingFrenzy;
    public WildmuttFeedingFrenzy() : base(WildmuttCardKind.FeedingFrenzy) { }
}
[RegisterCard(typeof(WildmuttCardPool))]
public class WildmuttPrimalHunt : WildmuttCardBase
{
    protected override WildmuttCardKind Kind => WildmuttCardKind.PrimalHunt;
    public WildmuttPrimalHunt() : base(WildmuttCardKind.PrimalHunt) { }
}
