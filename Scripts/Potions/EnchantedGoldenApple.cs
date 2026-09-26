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

namespace OtherworldTreasures.Scripts.Potions;

// 附魔金苹果：生命上限 +40，获得 32 点再生、10 点覆甲。获取来源：先古之民。
[RegisterPotion(typeof(SharedPotionPool))]
public class EnchantedGoldenApple : ModPotionTemplate
{
    private const decimal MaxHpGain = 40m;
    private const decimal RegenGain = 32m;
    private const decimal PlatingGain = 10m;

    // Event 稀有度不会被随机抽取（随机只滚 Common/Uncommon/Rare），
    // 所以这瓶药只能通过先古之民的选项获得。
    public override PotionRarity Rarity => PotionRarity.Event;

    public override PotionUsage Usage => PotionUsage.AnyTime;

    public override TargetType TargetType => TargetType.AnyPlayer;

    public override bool CanBeGeneratedInCombat => false;

    public override PotionAssetProfile AssetProfile => new(
        ImagePath: "res://OtherworldTreasures/images/potions/Enchanted_Golden_Apple.png",
        OutlinePath: "res://OtherworldTreasures/images/potions/Enchanted_Golden_Apple.png"
    );

    protected override IEnumerable<DynamicVar> CanonicalVars => [
        new MaxHpVar(MaxHpGain),
        new PowerVar<RegenPower>(RegenGain),
        new PowerVar<PlatingPower>(PlatingGain)
    ];

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        PotionModel.AssertValidForTargetedPotion(target);
        await CreatureCmd.GainMaxHp(target, MaxHpGain);
        await PowerCmd.Apply<RegenPower>(choiceContext, target, RegenGain, target, null);
        await PowerCmd.Apply<PlatingPower>(choiceContext, target, PlatingGain, target, null);
    }
}
