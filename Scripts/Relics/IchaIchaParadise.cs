using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Relics;

// 亲热天堂（来自《火影忍者》）：
// 前 3 个玩家回合开始时各获得 1 点能量；第 4 个玩家回合开始时，
// 把 3 张临时【悔恨】洗入抽牌堆。悔恨直接用原版诅咒牌 Regret。
// 图标暂用原版占位图，等美术资源到位再替换。
[RegisterRelic(typeof(SharedRelicPool))]
public class IchaIchaParadise : ModRelicTemplate
{
    private const string PlaceholderIcon = "res://images/powers/missing_power.png";

    // 前几回合给能量
    private const int EnergyTurns = 3;

    // 每回合获得的能量
    private const int EnergyGainPerTurn = 1;

    // 第 4 回合洗入的悔恨张数
    private const int RegretCount = 3;

    private int _turnsSeen;

    // 已经过了几个玩家回合。读档后不能重新数，否则会重复发能量。
    [SavedProperty]
    public int TurnsSeen
    {
        get => _turnsSeen;
        set
        {
            AssertMutable();
            _turnsSeen = value;
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

    // 描述里的 {Energy:energyIcons()} 靠这个渲染成能量球。
    // 变量名要用 EnergyVar 的默认名 Energy，自定义名在遗物描述里不被识别。
    protected override IEnumerable<DynamicVar> CanonicalVars => [new EnergyVar(EnergyGainPerTurn)];

    public override RelicAssetProfile AssetProfile => new(
        IconPath: PlaceholderIcon,
        IconOutlinePath: PlaceholderIcon,
        BigIconPath: PlaceholderIcon
    );

    // 每场战斗重新数
    public override async Task BeforeCombatStartLate()
    {
        TurnsSeen = 0;
        await base.BeforeCombatStartLate();
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner)
        {
            await base.AfterPlayerTurnStart(choiceContext, player);
            return;
        }

        TurnsSeen++;
        var turn = TurnsSeen;

        if (turn <= EnergyTurns)
        {
            Flash();
            await PlayerCmd.GainEnergy(1m, player);
        }
        else if (turn == EnergyTurns + 1)
        {
            await ShuffleRegrets(player);
        }

        await base.AfterPlayerTurnStart(choiceContext, player);
    }

    private async Task ShuffleRegrets(Player player)
    {
        var combat = player.Creature?.CombatState;
        if (combat == null)
        {
            return;
        }

        Flash();
        for (var i = 0; i < RegretCount; i++)
        {
            var card = combat.CreateCard(ModelDb.Card<Regret>(), player);
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Draw, player);
        }

        Entry.Logger.Info($"[IchaIchaParadise] 第 {TurnsSeen} 回合，洗入 {RegretCount} 张悔恨");
    }
}
