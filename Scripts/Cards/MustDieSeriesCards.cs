using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using OtherworldTreasures.Scripts.Monsters;
using OtherworldTreasures.Scripts.Events;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Cards;

public enum MustDieType
{
    Humanoid,
    Machine,
    Insect,
    Beast,
    SeaMonster,
    Plant,
    Slime,
    Other
}

public abstract class MustDieCardBase : ModCardTemplate, IMustDieSeriesCard, IDerivedCard
{
    protected MustDieCardBase() : base(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy, true) { }

    protected abstract MustDieType TargetCategory { get; }

    public override int MaxUpgradeLevel => 0;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [MyKeywords.Remove];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Damage", 12)];
    protected override CardLocation GetResultLocationForCardPlay() => new(Owner, PileType.None, CardPilePosition.Top);
    public override string Title => new LocString("cards", $"{Id.Entry}.title").GetFormattedText();
    public override CardAssetProfile AssetProfile => new(
        PortraitPath: $"res://OtherworldTreasures/images/cards/must_die/{PortraitName}.png");

    private string PortraitName => TargetCategory switch
    {
        MustDieType.Humanoid => "humanoid",
        MustDieType.Machine => "machine",
        MustDieType.Insect => "insect",
        MustDieType.Beast => "beast",
        MustDieType.SeaMonster => "sea_monster",
        MustDieType.Plant => "plant",
        MustDieType.Slime => "slime",
        MustDieType.Other => "other",
        _ => throw new ArgumentOutOfRangeException(nameof(TargetCategory))
    };

    protected override async Task OnPlay(PlayerChoiceContext context, CardPlay play)
    {
        Creature? target = play.Target;
        if (target == null || !target.IsAlive) return;

        await CreatureCmd.Damage(context, target, DynamicVars["Damage"].BaseValue,
            ValueProp.Move, Owner.Creature, this, play);
        if (!target.IsAlive || !MonsterTypeTags.Matches(target, TargetCategory)) return;

        // Boss bodies resist execution; matching boss targets instead lose half of
        // their remaining HP after the card's ordinary 12 damage has resolved.
        if (MonsterTypeTags.IsBossBody(target))
        {
            int extra = Math.Max(1, target.CurrentHp / 2);
            await CreatureCmd.Damage(context, target, (decimal)extra,
                ValueProp.Move | ValueProp.Unblockable | ValueProp.Unpowered, Owner.Creature, this, play);
            return;
        }

        // The native kill command runs death hooks, loot, and combat bookkeeping,
        // and force bypasses death-prevention / damage-cap powers.
        await CreatureCmd.Kill(target, force: true);
    }
}

[RegisterCard(typeof(TokenCardPool))]
public sealed class HumanoidMustDieCard : MustDieCardBase { protected override MustDieType TargetCategory => MustDieType.Humanoid; }
[RegisterCard(typeof(TokenCardPool))]
public sealed class MachineMustDieCard : MustDieCardBase
{
    protected override MustDieType TargetCategory => MustDieType.Machine;

    protected override IEnumerable<IHoverTip> AdditionalHoverTips
    {
        get
        {
            var tips = base.AdditionalHoverTips;
            if (!IsMutable || Owner == null || !MustDieRunData.ShouldShowMachineLore(Owner.RunState))
                return tips;

            return tips.Append(new HoverTip(
                new LocString("cards", Id.Entry + ".lore.title"),
                new LocString("cards", Id.Entry + ".lore.description")));
        }
    }

    public override Task AfterCombatEnd(CombatRoom room)
    {
        // Deck originals also receive this hook, even when the combat copy was
        // removed after playing. The saved run flag covers all copies and reloads.
        if (IsMutable && Owner != null)
            MustDieRunData.FinishMachineLoreCombat(Owner.RunState);
        return base.AfterCombatEnd(room);
    }
}
[RegisterCard(typeof(TokenCardPool))]
public sealed class InsectMustDieCard : MustDieCardBase { protected override MustDieType TargetCategory => MustDieType.Insect; }
[RegisterCard(typeof(TokenCardPool))]
public sealed class BeastMustDieCard : MustDieCardBase { protected override MustDieType TargetCategory => MustDieType.Beast; }
[RegisterCard(typeof(TokenCardPool))]
public sealed class SeaMonsterMustDieCard : MustDieCardBase { protected override MustDieType TargetCategory => MustDieType.SeaMonster; }
[RegisterCard(typeof(TokenCardPool))]
public sealed class PlantMustDieCard : MustDieCardBase { protected override MustDieType TargetCategory => MustDieType.Plant; }
[RegisterCard(typeof(TokenCardPool))]
public sealed class SlimeMustDieCard : MustDieCardBase { protected override MustDieType TargetCategory => MustDieType.Slime; }
[RegisterCard(typeof(TokenCardPool))]
public sealed class OtherMustDieCard : MustDieCardBase { protected override MustDieType TargetCategory => MustDieType.Other; }
