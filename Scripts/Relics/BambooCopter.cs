using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rooms;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using OtherworldTreasures.Scripts.Powers;

namespace OtherworldTreasures.Scripts.Relics;

// 竹蜻蜓：战斗开始时起飞（被攻击时受到伤害 −50%），被攻击 5 次后失效；落地 10 回合后再次起飞。
// 起飞能力由 BambooCopterFlightPower 承载，本遗物只负责"起飞 → 落地 → 再次起飞"的循环。
[RegisterRelic(typeof(SharedRelicPool))]
public class BambooCopter : ModRelicTemplate, IDoraemonItem
{
    // 起飞后可承受的攻击次数（= power 的 Amount）
    private const decimal FlightCharges = 5m;

    // 落地后需要等待的回合数才会再次起飞（描述中不提及）
    private const int RelandTurns = 10;

    // 战斗中遗物会被克隆，用 static 跨实例共享；每局新 run 在 AfterObtained 重置
    private static int s_landedTurns;

    // 哆啦A梦道具：自定义稀有度
    public override RelicRarity Rarity => ModRelicRarity.Doraemon;

    // 原版 MerchantCost 的 switch 遇到未知稀有度会抛异常，这里直接给值
    public override int MerchantCost => 999999999;

    protected override string IconBaseName => "bamboo_copter";

    protected override string BigIconPath => "res://OtherworldTreasures/images/relics/Bamboo_Copter.jpg";

    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://OtherworldTreasures/images/relics/Bamboo_Copter.jpg",
        IconOutlinePath: "res://OtherworldTreasures/images/relics/Bamboo_Copter.jpg",
        BigIconPath: "res://OtherworldTreasures/images/relics/Bamboo_Copter.jpg"
    );

    // 悬浮提示：预览「起飞」能力（减伤 50% + 5 次承受上限）
    protected override IEnumerable<IHoverTip> AdditionalHoverTips => base.AdditionalHoverTips
        .Append(HoverTipFactory.FromPower<BambooCopterFlightPower>((int)FlightCharges));

    // 每局新 run 重置 static 状态
    public override async Task AfterObtained()
    {
        s_landedTurns = 0;
        await base.AfterObtained();
    }

    // 战斗开始：起飞
    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is CombatRoom)
        {
            s_landedTurns = 0;
            await TakeOff(new ThrowingPlayerChoiceContext(), Owner);
        }
        await base.AfterRoomEntered(room);
    }

    // 玩家回合开始：若已落地则累计等待回合，满 10 回合后再次起飞
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner && player.Creature != null)
        {
            bool flying = player.Creature.Powers.Any(p => p is BambooCopterFlightPower);
            if (!flying)
            {
                s_landedTurns++;
                Entry.Logger.Info($"[BambooCopter] Landed, turn {s_landedTurns}/{RelandTurns}");
                if (s_landedTurns >= RelandTurns)
                {
                    s_landedTurns = 0;
                    await TakeOff(choiceContext, player);
                }
            }
        }
        await base.AfterPlayerTurnStart(choiceContext, player);
    }

    // 战斗结束：清空循环状态
    public override async Task AfterCombatEnd(CombatRoom room)
    {
        s_landedTurns = 0;
        await base.AfterCombatEnd(room);
    }

    private async Task TakeOff(PlayerChoiceContext choiceContext, Player? player)
    {
        var creature = player?.Creature;
        if (creature == null)
        {
            return;
        }
        if (creature.Powers.Any(p => p is BambooCopterFlightPower))
        {
            return;
        }
        await PowerCmd.Apply<BambooCopterFlightPower>(choiceContext, creature, FlightCharges, creature, null);
        Entry.Logger.Info("[BambooCopter] Take off! (damage taken -50%)");
    }
}
