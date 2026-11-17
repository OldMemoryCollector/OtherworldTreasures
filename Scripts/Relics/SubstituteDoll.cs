using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Relics;

// 替身人偶（来自《火影忍者》）：
// 每场战斗限一次。受到致命伤害时，把这次伤害转移给替身，本次伤害归零。
// 一次战斗只有一次，用掉后本场不再触发。
// 图标暂用原版占位图，等美术资源到位再替换。
[RegisterRelic(typeof(SharedRelicPool))]
public class SubstituteDoll : ModRelicTemplate
{
    private const string PlaceholderIcon = "res://images/powers/missing_power.png";

    private bool _usedThisCombat;

    // 本场是否已经用掉。读档后不能白送第二次，所以要进存档。
    [SavedProperty]
    public bool UsedThisCombat
    {
        get => _usedThisCombat;
        set
        {
            AssertMutable();
            _usedThisCombat = value;
        }
    }

    public override RelicRarity Rarity => RelicRarity.Event;
    #if STS2_113

    protected override bool IsAllowedInternal(IRunState runState)

    #else

    public override bool IsAllowed(IRunState runState)

    #endif
        => DevelopmentContent.Extras;
    public override bool IsAllowedInShops => DevelopmentContent.Extras;

    public override RelicAssetProfile AssetProfile => new(
        IconPath: PlaceholderIcon,
        IconOutlinePath: PlaceholderIcon,
        BigIconPath: PlaceholderIcon
    );

    // 每场战斗重新给一次机会
    public override async Task BeforeCombatStartLate()
    {
        UsedThisCombat = false;
        await base.BeforeCombatStartLate();
    }

    // 致命伤害拦截：这次伤害足以致死时，改成本次不造成伤害
    public override decimal ModifyHpLostAfterOsty(
        Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (Owner?.Creature == null || target != Owner.Creature)
        {
            return amount;
        }

        if (UsedThisCombat || amount < 1m)
        {
            return amount;
        }

        // 不致死就不触发，留给真正要命的那一下
        if (amount < target.CurrentHp)
        {
            return amount;
        }

        UsedThisCombat = true;
        Flash();
        Entry.Logger.Info($"[SubstituteDoll] 替身挡下致命伤害 {amount}（当前生命 {target.CurrentHp}）");
        return 0m;
    }
}
