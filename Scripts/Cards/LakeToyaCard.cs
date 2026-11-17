using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.ValueProps;
using OtherworldTreasures.Scripts.Gintama;
using OtherworldTreasures.Scripts.Powers;
using OtherworldTreasures.Scripts.Spider;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Cards;

[RegisterCard(typeof(TokenCardPool))]
public sealed class LakeToyaCard : ModCardTemplate
{
    public LakeToyaCard() : base(1, CardType.Attack, CardRarity.Token, TargetType.AnyEnemy, DevelopmentContent.Gintama) { }
    public override bool CanBeGeneratedInCombat => false;
    public override bool CanBeGeneratedByModifiers => false;
    public override int MaxUpgradeLevel => 0;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain];
    public override CardAssetProfile AssetProfile => new(PortraitPath: "res://images/powers/missing_power.png");
    // Native Shiv uses the same dynamic target-type pattern for its single/AoE forms.
    public override TargetType TargetType => CanUseFinisher ? TargetType.AllEnemies : TargetType.AnyEnemy;
    protected override bool ShouldGlowGoldInternal => CanUseFinisher;
    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new CalculationBaseVar(LakeToyaRules.BaseDamage),
        new ExtraDamageVar(1m),
        new CalculatedDamageVar(ValueProp.Move).WithMultiplier(static (card, _) =>
            LakeToyaRules.MissingHealthBonus(card.Owner.Creature.MaxHp, card.Owner.Creature.CurrentHp))
    ];

    private bool CanUseFinisher
    {
        get
        {
            if (!IsMutable || Owner?.Creature is not { IsAlive: true } player ||
                player.CombatState is not { } combat || !CombatManager.Instance.IsInProgress ||
                CombatManager.Instance.IsOverOrEnding) return false;
            var attacks = new List<LakeToyaAttack>();
            foreach (var enemy in combat.Enemies.Where(enemy => enemy.IsAlive))
            {
                if (enemy.Monster?.NextMove is not { } move) continue;
                foreach (var intent in move.Intents.OfType<AttackIntent>())
                {
                    if (intent.DamageCalc is not { } damageCalc) continue;
                    // Use the owner's target-specific modifiers, never the local viewer in multiplayer.
                    decimal damage = Hook.ModifyDamage(Owner.RunState, combat, player, enemy,
                        damageCalc(), ValueProp.Move, null, null, ModifyDamageHookType.All,
                        CardPreviewMode.None, out _);
                    int hits = intent is MultiAttackIntent ? Math.Max(0, intent.Repeats) : 1;
                    attacks.Add(new LakeToyaAttack((int)Math.Clamp(damage, 0m, int.MaxValue), hits));
                }
            }
            int absorption = (int)Math.Min(int.MaxValue, (long)TemporaryHealthPower.Available(player) +
                Math.Max(0, player.GetPower<SpiderSilkPower>()?.Amount ?? 0));
            return LakeToyaRules.IsLethal(player.CurrentHp, player.Block,
                absorption, player.GetPower<BufferPower>()?.Amount ?? 0, attacks);
        }
    }

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        // Both modes use the identical calculated damage and one native attack command.
        bool finisher = CanUseFinisher;
        var attack = DamageCmd.Attack(DynamicVars.CalculatedDamage).FromCard(this, play);
        if (finisher) attack.TargetingAllOpponents(CombatState!);
        else
        {
            if (play.Target == null) return;
            attack.Targeting(play.Target);
        }
        await attack.WithHitFx(finisher ? "vfx/vfx_giant_horizontal_slash" : "vfx/vfx_attack_slash")
            .Execute(context);
    }
}
