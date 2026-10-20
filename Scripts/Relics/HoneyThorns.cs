using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.ValueProps;
using OtherworldTreasures.Scripts.UI;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Relics;

[RegisterRelic(typeof(SharedRelicPool))]
public sealed class HoneyThorns : ModRelicTemplate
{
    private bool _resolving;
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://OtherworldTreasures/images/relics/HoneyThorns_85.png",
        IconOutlinePath: "res://OtherworldTreasures/images/relics/HoneyThorns_85.png",
        BigIconPath: "res://OtherworldTreasures/images/relics/HoneyThorns.png");
    public override async Task AfterDamageReceived(PlayerChoiceContext context, Creature target, DamageResult result,
        ValueProp props, Creature? dealer, CardModel? source)
    {
        if (_resolving || target != Owner.Creature || target.IsDead || result.UnblockedDamage <= 0 ||
            !CombatManager.Instance.IsInProgress || CombatManager.Instance.IsEnding || target.CombatState is not { } combat ||
            !combat.Enemies.Any(enemy => enemy.IsAlive)) return;
        _resolving = true;
        try
        {
            Flash();
            var rng = Owner.RunState.Rng.Niche;
            int count = rng.NextInt(1, 4);
            for (int i = 0; i < count; i++)
            {
                var enemies = combat.Enemies.Where(enemy => enemy.IsAlive).ToArray();
                if (enemies.Length == 0) break;
                var enemy = rng.NextItem(enemies)!;
                await TerrariaRelicVfx.FlyBee(target, enemy);
                await CreatureCmd.Damage(context, enemy, 3m, ValueProp.Unpowered, target, null, null);
                if (enemy.IsAlive) await PowerCmd.Apply<PoisonPower>(context, enemy, 1m, target, null);
            }
        }
        finally { _resolving = false; }
    }
}
