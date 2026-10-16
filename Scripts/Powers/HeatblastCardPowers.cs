using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.ValueProps;
using OtherworldTreasures.Scripts.Alien;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Powers;

/// <summary>只负责选择、消耗；治疗和格挡由官方消耗钩子处理，避免重复触发。</summary>
internal static class HeatblastStatus
{
    internal static List<CardModel> InHand(Player player)
        => player.PlayerCombatState?.Hand.Cards.Where(c => c.Type == CardType.Status).ToList() ?? [];
    internal static bool IsBurnCard(CardModel card) => card is Burn;

    internal static async Task<int> ExhaustSelected(PlayerChoiceContext context, Player player,
        CardModel source, int maxCount = 1, bool upTo = false)
    {
        var available = InHand(player);
        if (available.Count == 0) return 0;
        int maximum = System.Math.Min(maxCount, available.Count);
        var selected = await CardSelectCmd.FromHand(context, player,
            new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, upTo ? 0 : maximum, maximum),
            c => c.Type == CardType.Status, source);
        return await Exhaust(context, player, selected.ToList());
    }

    internal static async Task<int> Exhaust(PlayerChoiceContext context, Player player,
        IReadOnlyList<CardModel> cards, bool skipVisuals = false)
    {
        int count = 0;
        var exhausted = new List<CardModel>();
        foreach (var card in cards.Distinct().ToList())
        {
            if (card.Owner != player || card.Type != CardType.Status || card.Pile?.Type != PileType.Hand) continue;
            if (await CardCmd.Exhaust(context, card, causedByEthereal: false, skipVisuals: skipVisuals) != null)
            {
                count++;
                exhausted.Add(card);
            }
        }
        if (skipVisuals) HandUiSync.RemoveFromHandUi(exhausted);
        return count;
    }
}

public abstract class HeatblastPowerBase : ModPowerTemplate
{
    protected abstract string LocKey { get; }
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerAssetProfile AssetProfile => AlienPowerArt.Profile($"heatblast/{LocKey}");
    public override LocString Title => new("powers", $"OTHERWORLD_TREASURES_POWER_HEATBLAST_{LocKey}.title");
    public override LocString Description => new("powers", $"OTHERWORLD_TREASURES_POWER_HEATBLAST_{LocKey}.description");
}

/// <summary>V2.1：每次给予灼伤额外 +1，含热爆，不限制触发次数。</summary>
[RegisterPower]
public class HeatblastSolarFlarePower : HeatblastPowerBase
{
    protected override string LocKey => "SOLAR_FLARE";
}

/// <summary>任何来源消耗状态牌都触发，非状态牌不触发。</summary>
[RegisterPower]
public class HeatblastScorchingCorePower : HeatblastPowerBase
{
    protected override string LocKey => "SCORCHING_CORE";
    public override async Task AfterCardExhausted(PlayerChoiceContext context, CardModel card, bool causedByEthereal)
    {
        if (card.Owner.Creature == Owner && card.Type == CardType.Status)
            await CreatureCmd.GainBlock(Owner, 4m * Amount, ValueProp.Unpowered, null);
    }
}

/// <summary>每次实际获得额外能量，为下一张攻击的所有段和目标累加 +5；不设次数上限。</summary>
[RegisterPower]
public class HeatblastSupercombustionPower : HeatblastPowerBase
{
    protected override string LocKey => "SUPERCOMBUSTION";
    private decimal _pendingBonus;
    private Dictionary<CardPlay, decimal> _activeBonuses = new();
    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _activeBonuses = new(_activeBonuses);
    }
    internal void OnExtraEnergyGained() => _pendingBonus += 5m * Amount;

    public override Task BeforeCardPlayed(CardPlay play)
    {
        if (play.Player.Creature == Owner && play.Card.Type == CardType.Attack)
        {
            _activeBonuses[play] = _pendingBonus;
            _pendingBonus = 0;
        }
        return Task.CompletedTask;
    }

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (dealer != Owner || cardSource?.Type != CardType.Attack || !props.IsPoweredAttack()) return 0;
        return cardPlay == null ? _pendingBonus : _activeBonuses.GetValueOrDefault(cardPlay);
    }

    public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
    {
        _activeBonuses.Remove(play);
        return Task.CompletedTask;
    }

    // 文本没有“本回合下一张”，未用的强化可以留给下一回合。
    internal void ResetPendingAttack() => _activeBonuses.Clear();
}
