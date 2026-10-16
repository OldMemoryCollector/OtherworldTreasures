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

// 商店专属，效果限本场战斗。
[RegisterPotion(typeof(SharedPotionPool))]
public class GoldenApple : ModPotionTemplate
{
    private const decimal TemporaryHealthGain = 10m;
    private const decimal RegenGain = 3m;

    // 自定义稀有度「极稀有」（原版枚举无法扩展，用未定义值；随机抽药不会滚到这个值）
    public override PotionRarity Rarity => ModPotionRarity.VeryRare;

    public override PotionUsage Usage => PotionUsage.CombatOnly;
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => AppleHoverTips.For(false);

    public override TargetType TargetType => TargetType.AnyPlayer;

    // 不作为战斗内随机掉落（获取来源限定为商店）
    public override bool CanBeGeneratedInCombat => false;

    public override PotionAssetProfile AssetProfile => new(
        ImagePath: "res://OtherworldTreasures/images/potions/Golden_Apple.png",
        OutlinePath: "res://OtherworldTreasures/images/potions/Golden_Apple.png"
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
    }
}
