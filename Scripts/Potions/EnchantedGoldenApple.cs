using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.PotionPools;
using MegaCrit.Sts2.Core.Models.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using OtherworldTreasures.Scripts.Powers;
using MegaCrit.Sts2.Core.HoverTips;

namespace OtherworldTreasures.Scripts.Potions;

// 宝箱额外掉落，不进入先古之民或随机药水池。
[RegisterPotion(typeof(SharedPotionPool))]
public class EnchantedGoldenApple : ModPotionTemplate
{
    private const decimal TemporaryHealthGain = 40m;
    private const decimal RegenGain = 6m;

    public override PotionRarity Rarity => PotionRarity.Event;

    public override PotionUsage Usage => PotionUsage.CombatOnly;
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => AppleHoverTips.For(true);

    public override TargetType TargetType => TargetType.AnyPlayer;

    public override bool CanBeGeneratedInCombat => false;

    public override PotionAssetProfile AssetProfile => new(
        ImagePath: "res://OtherworldTreasures/images/potions/Enchanted_Golden_Apple.png",
        OutlinePath: "res://OtherworldTreasures/images/potions/Enchanted_Golden_Apple.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new PowerVar<TemporaryHealthPower>(TemporaryHealthGain),
        new PowerVar<RegenPower>(RegenGain)
    ];

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        PotionModel.AssertValidForTargetedPotion(target);
        await PowerCmd.Apply<TemporaryHealthPower>(choiceContext, target, TemporaryHealthGain, target, null);
        await PowerCmd.Apply<RegenPower>(choiceContext, target, RegenGain, target, null);
        await PowerCmd.Apply<AppleResistancePower>(choiceContext, target, 1, target, null);
        await PowerCmd.Apply<AppleFireResistancePower>(choiceContext, target, 1, target, null);
    }
}
