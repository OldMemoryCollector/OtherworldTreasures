using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using OtherworldTreasures.Scripts.Alien;
using OtherworldTreasures.Scripts.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Cards.Alien;

[RegisterSharedCardPool]
public class GhostfreakCardPool : TypeListCardPoolModel
{
    public override string Title => "ghostfreak";
    public override string EnergyColorName => "ironclad";
    public override string CardFrameMaterialPath => "card_frame_red";
    public override Color DeckEntryCardColor => new("D62000");
    public override Color EnergyOutlineColor => new("802020");
    public override bool IsColorless => false;
}
public enum GhostfreakCardKind { GhostClaw, ColdTouch, PhasePierce, ShadowSweep, SoulScream, DeadStare, ShadowWhisper, Manifestation, SilentAmbush, WallPierce, ColdInvasion, ShadowSkim, SpiritGlide, RealityReverse, Interlace, SoulEntangle, WraithWail, PanicGrab, EvilManifest, NightmareSpread, DespairEcho, TrueForm, SoulTentacle, SoulDevour, MentalHostage, BodySteal, PuppetCounter, MindControl, Dominion, UltimateTerror }
internal readonly record struct GhostfreakCardSpec(int Cost, CardType Type, CardRarity Rarity, TargetType Target, decimal Damage, int Fear, bool Piercing);
internal static class GhostfreakCards
{
    internal static GhostfreakCardSpec Spec(GhostfreakCardKind kind) => kind switch
    {
        GhostfreakCardKind.GhostClaw => new(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy, 8m, 0, false),
        GhostfreakCardKind.ColdTouch => new(0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy, 4m, 1, false),
        GhostfreakCardKind.PhasePierce => new(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy, 7m, 0, true),
        GhostfreakCardKind.ShadowSweep => new(1, CardType.Attack, CardRarity.Common, TargetType.AllEnemies, 6m, 1, false),
        GhostfreakCardKind.SoulScream => new(1, CardType.Skill, CardRarity.Common, TargetType.AllEnemies, 0m, 1, false),
        GhostfreakCardKind.DeadStare => new(1, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy, 0m, 3, false),
        GhostfreakCardKind.ShadowWhisper => new(0, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy, 0m, 1, false),
        GhostfreakCardKind.Manifestation => new(0, CardType.Skill, CardRarity.Common, TargetType.AllEnemies, 0m, 1, false),
        GhostfreakCardKind.SilentAmbush => new(0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy, 5m, 0, true),
        GhostfreakCardKind.WallPierce => new(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy, 11m, 1, true),
        GhostfreakCardKind.ColdInvasion => new(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy, 9m, 0, false),
        GhostfreakCardKind.ShadowSkim => new(2, CardType.Attack, CardRarity.Common, TargetType.AllEnemies, 12m, 1, true),
        GhostfreakCardKind.SpiritGlide => new(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self, 0m, 0, false),
        GhostfreakCardKind.RealityReverse => new(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self, 0m, 2, false),
        GhostfreakCardKind.Interlace => new(1, CardType.Power, CardRarity.Rare, TargetType.Self, 0m, 0, false),
        GhostfreakCardKind.SoulEntangle => new(0, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy, 0m, 2, false),
        GhostfreakCardKind.WraithWail => new(1, CardType.Skill, CardRarity.Common, TargetType.AllEnemies, 0m, 3, false),
        GhostfreakCardKind.PanicGrab => new(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy, 8m, 2, false),
        GhostfreakCardKind.EvilManifest => new(2, CardType.Attack, CardRarity.Common, TargetType.AllEnemies, 10m, 2, false),
        GhostfreakCardKind.NightmareSpread => new(1, CardType.Skill, CardRarity.Uncommon, TargetType.AllEnemies, 0m, 2, false),
        GhostfreakCardKind.DespairEcho => new(2, CardType.Skill, CardRarity.Uncommon, TargetType.AllEnemies, 0m, 0, false),
        GhostfreakCardKind.TrueForm => new(1, CardType.Power, CardRarity.Rare, TargetType.Self, 0m, 0, false),
        GhostfreakCardKind.SoulTentacle => new(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy, 8m, 0, false),
        GhostfreakCardKind.SoulDevour => new(0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy, 4m, 0, false),
        GhostfreakCardKind.MentalHostage => new(1, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy, 0m, 2, false),
        GhostfreakCardKind.BodySteal => new(1, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy, 0m, 1, false),
        GhostfreakCardKind.PuppetCounter => new(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy, 10m, 0, false),
        GhostfreakCardKind.MindControl => new(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy, 0m, 2, false),
        GhostfreakCardKind.Dominion => new(1, CardType.Power, CardRarity.Rare, TargetType.Self, 0m, 0, false),
        GhostfreakCardKind.UltimateTerror => new(2, CardType.Skill, CardRarity.Ancient, TargetType.AllEnemies, 0m, AlienTerrorPower.SoulFrightThreshold, false),
        _ => throw new System.ArgumentOutOfRangeException(nameof(kind)),
    };
}
public abstract class GhostfreakCardBase : AlienCardBase
{
    protected abstract GhostfreakCardKind Kind { get; }
    protected GhostfreakCardBase(GhostfreakCardKind kind) : base(GhostfreakCards.Spec(kind).Cost, GhostfreakCards.Spec(kind).Type, GhostfreakCards.Spec(kind).Rarity, GhostfreakCards.Spec(kind).Target) { }
    protected GhostfreakPassivePower? Passive => Owner.Creature.GetPower<GhostfreakPassivePower>();
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new DamageVar(GhostfreakCards.Spec(Kind).Damage, ValueProp.Move),
        new DynamicVar("Fear", GhostfreakCards.Spec(Kind).Fear),
        new EnergyVar("EnergyGain", 1),
        new DynamicVar("Intangible", 1),
    ];
    public override CardAssetProfile AssetProfile => AlienCardArt.Profile(this);
    public override IEnumerable<CardKeyword> CanonicalKeywords => Rarity == CardRarity.Ancient ? [CardKeyword.Retain] : [];
    protected override async Task OnPlay(PlayerChoiceContext c, CardPlay p)
    {
        var spec=GhostfreakCards.Spec(Kind);
        bool intangible=Owner.Creature.GetPower<IntangiblePower>() is { Amount: > 0 };
        bool panic=GhostFear.Panicked(p.Target);
        async Task FearTarget(int amount)
        {
            if(p.Target is { IsAlive: true }) await GhostFear.Apply(c,p.Target,amount,Owner.Creature,this);
        }
        async Task<bool> FearAll(int amount)
        {
            bool changed=false;
            foreach(var enemy in Enemies) changed |= await GhostFear.Apply(c,enemy,amount,Owner.Creature,this);
            return changed;
        }
        switch(Kind)
        {
            case GhostfreakCardKind.GhostClaw:
                bool feared = p.Target?.GetPower<AlienTerrorPower>() is { Amount: > 0 };
                await Hit(c,p,p.Target,DynamicVars.Damage.BaseValue);
                if(feared) await Draw(c);
                break;
            case GhostfreakCardKind.PhasePierce:
                bool blocked = p.Target?.Block > 0;
                await Hit(c,p,p.Target,DynamicVars.Damage.BaseValue,piercing:true);
                if(blocked) await Draw(c);
                break;
            case GhostfreakCardKind.SoulTentacle:
                if(panic && p.Target is { } tentacle)
                    await CreatureCmd.LoseBlock(c,tentacle,tentacle.Block,Owner.Creature);
                await Hit(c,p,p.Target,DynamicVars.Damage.BaseValue);
                break;
            case GhostfreakCardKind.ColdTouch:
            case GhostfreakCardKind.WallPierce:
            case GhostfreakCardKind.PanicGrab:
                await Hit(c,p,p.Target,DynamicVars.Damage.BaseValue,piercing:spec.Piercing);
                await FearTarget(spec.Fear); break;
            case GhostfreakCardKind.ShadowSweep:
            case GhostfreakCardKind.EvilManifest:
                await HitAll(c,p,DynamicVars.Damage.BaseValue); await FearAll(spec.Fear); break;
            case GhostfreakCardKind.SoulScream:
                await FearAll(spec.Fear); await Draw(c,2); break;
            case GhostfreakCardKind.WraithWail:
                await FearAll(spec.Fear); break;
            case GhostfreakCardKind.DeadStare:
            case GhostfreakCardKind.SoulEntangle:
                await FearTarget(spec.Fear); break;
            case GhostfreakCardKind.ShadowWhisper:
                await Draw(c); await FearTarget(spec.Fear); break;
            case GhostfreakCardKind.Manifestation:
                if(await FearAll(1)) { await PlayerCmd.GainEnergy(1m,Owner); await Draw(c); } break;
            case GhostfreakCardKind.SilentAmbush:
                await Hit(c,p,p.Target,DynamicVars.Damage.BaseValue,piercing:true); break;
            case GhostfreakCardKind.ColdInvasion:
                await Hit(c,p,p.Target,DynamicVars.Damage.BaseValue);
                await PowerCmd.Apply<IntangiblePower>(c,Owner.Creature,DynamicVars["Intangible"].BaseValue,Owner.Creature,this);
                break;
            case GhostfreakCardKind.ShadowSkim:
                await HitAll(c,p,DynamicVars.Damage.BaseValue,piercing:true); if(!intangible) await FearAll(1); break;
            case GhostfreakCardKind.SpiritGlide:
                if(Passive is { } glide && await glide.ConsumeIntangible(c,this)) { await Draw(c,2); await Block(p,6m); }
                else await Draw(c); break;
            case GhostfreakCardKind.RealityReverse:
                if(Passive is { } reverse && await reverse.ConsumeIntangible(c,this)) reverse.NextAttackReversed=true; break;
            case GhostfreakCardKind.Interlace:
                await PowerCmd.Apply<GhostfreakInterlacePower>(c,Owner.Creature,1m,Owner.Creature,this); break;
            case GhostfreakCardKind.NightmareSpread:
                var before=Enemies.ToDictionary(e=>e,e=>GhostFear.Panicked(e));
                foreach(var enemy in before.Keys)
                {
                    await GhostFear.Apply(c,enemy,2,Owner.Creature,this);
                    if(!before[enemy]) await GhostFear.Apply(c,enemy,2,Owner.Creature,this);
                }
                break;
            case GhostfreakCardKind.DespairEcho:
                foreach(var enemy in Enemies)
                    await GhostFear.Apply(c,enemy,(int)System.Math.Ceiling((enemy.GetPower<AlienTerrorPower>()?.Amount??0)/2m),Owner.Creature,this);
                break;
            case GhostfreakCardKind.TrueForm:
                await PowerCmd.Apply<GhostfreakTrueFormPower>(c,Owner.Creature,1m,Owner.Creature,this); break;
            case GhostfreakCardKind.SoulDevour:
                await Hit(c,p,p.Target,DynamicVars.Damage.BaseValue); if(!panic) await Draw(c); break;
            case GhostfreakCardKind.MentalHostage:
                await FearTarget(2);
                if(p.Target is { IsAlive: true }) await PowerCmd.Apply<WeakPower>(c,p.Target,2m,Owner.Creature,this); break;
            case GhostfreakCardKind.BodySteal:
                await Block(p,System.Math.Min(15,p.Target?.Block??0)); await FearTarget(1); break;
            case GhostfreakCardKind.PuppetCounter:
                await Hit(c,p,p.Target,DynamicVars.Damage.BaseValue+(panic?p.Target?.GetPower<AlienTerrorPower>()?.Amount??0:0)); break;
            case GhostfreakCardKind.MindControl:
                await FearTarget(2); if(!panic) { await PlayerCmd.GainEnergy(1m,Owner); await Draw(c,2); } break;
            case GhostfreakCardKind.Dominion:
                await PowerCmd.Apply<GhostfreakDominionPower>(c,Owner.Creature,1m,Owner.Creature,this); break;
            case GhostfreakCardKind.UltimateTerror:
                await FearAll((int)DynamicVars["Fear"].BaseValue);
                foreach(var enemy in Enemies.Where(GhostFear.Escaping).ToList()) await GhostFear.DefeatByFear(enemy);
                break;
        }
    }
}
[RegisterCard(typeof(GhostfreakCardPool))]
public class GhostfreakGhostClaw : GhostfreakCardBase
{
    protected override GhostfreakCardKind Kind => GhostfreakCardKind.GhostClaw;
    public GhostfreakGhostClaw() : base(GhostfreakCardKind.GhostClaw) { }
}
[RegisterCard(typeof(GhostfreakCardPool))]
public class GhostfreakColdTouch : GhostfreakCardBase
{
    protected override GhostfreakCardKind Kind => GhostfreakCardKind.ColdTouch;
    public GhostfreakColdTouch() : base(GhostfreakCardKind.ColdTouch) { }
}
[RegisterCard(typeof(GhostfreakCardPool))]
public class GhostfreakPhasePierce : GhostfreakCardBase
{
    protected override GhostfreakCardKind Kind => GhostfreakCardKind.PhasePierce;
    public GhostfreakPhasePierce() : base(GhostfreakCardKind.PhasePierce) { }
}
[RegisterCard(typeof(GhostfreakCardPool))]
public class GhostfreakShadowSweep : GhostfreakCardBase
{
    protected override GhostfreakCardKind Kind => GhostfreakCardKind.ShadowSweep;
    public GhostfreakShadowSweep() : base(GhostfreakCardKind.ShadowSweep) { }
}
[RegisterCard(typeof(GhostfreakCardPool))]
public class GhostfreakSoulScream : GhostfreakCardBase
{
    protected override GhostfreakCardKind Kind => GhostfreakCardKind.SoulScream;
    public GhostfreakSoulScream() : base(GhostfreakCardKind.SoulScream) { }
}
[RegisterCard(typeof(GhostfreakCardPool))]
public class GhostfreakDeadStare : GhostfreakCardBase
{
    protected override GhostfreakCardKind Kind => GhostfreakCardKind.DeadStare;
    public GhostfreakDeadStare() : base(GhostfreakCardKind.DeadStare) { }
}
[RegisterCard(typeof(GhostfreakCardPool))]
public class GhostfreakShadowWhisper : GhostfreakCardBase
{
    protected override GhostfreakCardKind Kind => GhostfreakCardKind.ShadowWhisper;
    public GhostfreakShadowWhisper() : base(GhostfreakCardKind.ShadowWhisper) { }
}
[RegisterCard(typeof(GhostfreakCardPool))]
public class GhostfreakManifestation : GhostfreakCardBase
{
    protected override GhostfreakCardKind Kind => GhostfreakCardKind.Manifestation;
    public GhostfreakManifestation() : base(GhostfreakCardKind.Manifestation) { }
}
[RegisterCard(typeof(GhostfreakCardPool))]
public class GhostfreakSilentAmbush : GhostfreakCardBase
{
    protected override GhostfreakCardKind Kind => GhostfreakCardKind.SilentAmbush;
    public GhostfreakSilentAmbush() : base(GhostfreakCardKind.SilentAmbush) { }
}
[RegisterCard(typeof(GhostfreakCardPool))]
public class GhostfreakWallPierce : GhostfreakCardBase
{
    protected override GhostfreakCardKind Kind => GhostfreakCardKind.WallPierce;
    public GhostfreakWallPierce() : base(GhostfreakCardKind.WallPierce) { }
}
[RegisterCard(typeof(GhostfreakCardPool))]
public class GhostfreakColdInvasion : GhostfreakCardBase
{
    protected override GhostfreakCardKind Kind => GhostfreakCardKind.ColdInvasion;
    public GhostfreakColdInvasion() : base(GhostfreakCardKind.ColdInvasion) { }
}
[RegisterCard(typeof(GhostfreakCardPool))]
public class GhostfreakShadowSkim : GhostfreakCardBase
{
    protected override GhostfreakCardKind Kind => GhostfreakCardKind.ShadowSkim;
    public GhostfreakShadowSkim() : base(GhostfreakCardKind.ShadowSkim) { }
}
[RegisterCard(typeof(GhostfreakCardPool))]
public class GhostfreakSpiritGlide : GhostfreakCardBase
{
    protected override GhostfreakCardKind Kind => GhostfreakCardKind.SpiritGlide;
    public GhostfreakSpiritGlide() : base(GhostfreakCardKind.SpiritGlide) { }
}
[RegisterCard(typeof(GhostfreakCardPool))]
public class GhostfreakRealityReverse : GhostfreakCardBase
{
    protected override GhostfreakCardKind Kind => GhostfreakCardKind.RealityReverse;
    public GhostfreakRealityReverse() : base(GhostfreakCardKind.RealityReverse) { }
}
[RegisterCard(typeof(GhostfreakCardPool))]
public class GhostfreakInterlace : GhostfreakCardBase
{
    protected override GhostfreakCardKind Kind => GhostfreakCardKind.Interlace;
    public GhostfreakInterlace() : base(GhostfreakCardKind.Interlace) { }
}
[RegisterCard(typeof(GhostfreakCardPool))]
public class GhostfreakSoulEntangle : GhostfreakCardBase
{
    protected override GhostfreakCardKind Kind => GhostfreakCardKind.SoulEntangle;
    public GhostfreakSoulEntangle() : base(GhostfreakCardKind.SoulEntangle) { }
}
[RegisterCard(typeof(GhostfreakCardPool))]
public class GhostfreakWraithWail : GhostfreakCardBase
{
    protected override GhostfreakCardKind Kind => GhostfreakCardKind.WraithWail;
    public GhostfreakWraithWail() : base(GhostfreakCardKind.WraithWail) { }
}
[RegisterCard(typeof(GhostfreakCardPool))]
public class GhostfreakPanicGrab : GhostfreakCardBase
{
    protected override GhostfreakCardKind Kind => GhostfreakCardKind.PanicGrab;
    public GhostfreakPanicGrab() : base(GhostfreakCardKind.PanicGrab) { }
}
[RegisterCard(typeof(GhostfreakCardPool))]
public class GhostfreakEvilManifest : GhostfreakCardBase
{
    protected override GhostfreakCardKind Kind => GhostfreakCardKind.EvilManifest;
    public GhostfreakEvilManifest() : base(GhostfreakCardKind.EvilManifest) { }
}
[RegisterCard(typeof(GhostfreakCardPool))]
public class GhostfreakNightmareSpread : GhostfreakCardBase
{
    protected override GhostfreakCardKind Kind => GhostfreakCardKind.NightmareSpread;
    public GhostfreakNightmareSpread() : base(GhostfreakCardKind.NightmareSpread) { }
}
[RegisterCard(typeof(GhostfreakCardPool))]
public class GhostfreakDespairEcho : GhostfreakCardBase
{
    protected override GhostfreakCardKind Kind => GhostfreakCardKind.DespairEcho;
    public GhostfreakDespairEcho() : base(GhostfreakCardKind.DespairEcho) { }
}
[RegisterCard(typeof(GhostfreakCardPool))]
public class GhostfreakTrueForm : GhostfreakCardBase
{
    protected override GhostfreakCardKind Kind => GhostfreakCardKind.TrueForm;
    public GhostfreakTrueForm() : base(GhostfreakCardKind.TrueForm) { }
}
[RegisterCard(typeof(GhostfreakCardPool))]
public class GhostfreakSoulTentacle : GhostfreakCardBase
{
    protected override GhostfreakCardKind Kind => GhostfreakCardKind.SoulTentacle;
    public GhostfreakSoulTentacle() : base(GhostfreakCardKind.SoulTentacle) { }
}
[RegisterCard(typeof(GhostfreakCardPool))]
public class GhostfreakSoulDevour : GhostfreakCardBase
{
    protected override GhostfreakCardKind Kind => GhostfreakCardKind.SoulDevour;
    public GhostfreakSoulDevour() : base(GhostfreakCardKind.SoulDevour) { }
}
[RegisterCard(typeof(GhostfreakCardPool))]
public class GhostfreakMentalHostage : GhostfreakCardBase
{
    protected override GhostfreakCardKind Kind => GhostfreakCardKind.MentalHostage;
    public GhostfreakMentalHostage() : base(GhostfreakCardKind.MentalHostage) { }
}
[RegisterCard(typeof(GhostfreakCardPool))]
public class GhostfreakBodySteal : GhostfreakCardBase
{
    protected override GhostfreakCardKind Kind => GhostfreakCardKind.BodySteal;
    public GhostfreakBodySteal() : base(GhostfreakCardKind.BodySteal) { }
}
[RegisterCard(typeof(GhostfreakCardPool))]
public class GhostfreakPuppetCounter : GhostfreakCardBase
{
    protected override GhostfreakCardKind Kind => GhostfreakCardKind.PuppetCounter;
    public GhostfreakPuppetCounter() : base(GhostfreakCardKind.PuppetCounter) { }
}
[RegisterCard(typeof(GhostfreakCardPool))]
public class GhostfreakMindControl : GhostfreakCardBase
{
    protected override GhostfreakCardKind Kind => GhostfreakCardKind.MindControl;
    public GhostfreakMindControl() : base(GhostfreakCardKind.MindControl) { }
}
[RegisterCard(typeof(GhostfreakCardPool))]
public class GhostfreakDominion : GhostfreakCardBase
{
    protected override GhostfreakCardKind Kind => GhostfreakCardKind.Dominion;
    public GhostfreakDominion() : base(GhostfreakCardKind.Dominion) { }
}
[RegisterCard(typeof(GhostfreakCardPool))]
public class GhostfreakUltimateTerror : GhostfreakCardBase
{
    protected override GhostfreakCardKind Kind => GhostfreakCardKind.UltimateTerror;
    public GhostfreakUltimateTerror() : base(GhostfreakCardKind.UltimateTerror) { }
}
