using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Singleton;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;
using OtherworldTreasures.Scripts.Ancients;
using OtherworldTreasures.Scripts.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Events;

// === 2026-10-17 SCP-914 位移制重做（主动改造，不是故障、也不是回归） ===
// 旧实现把卡牌/遗物的稀有度当成数字直接 ±1 传给 CreateCard/CreateRelic，
// 于是诅咒卡（旧 tier 0）与普通卡同档，任何 +1 分支都会直接吐出蓝卡；
// tier 0 的产出池里还混着诅咒、状态与基础牌，普通卡加工时也可能莫名其妙变成诅咒。
// 现在统一由 Scp914Ranks 决定"往哪一档位移"：
//   粗加工 0/+1/-1，精加工 +1/+2/-1，极精加工成功 +2、失败 -5（＝必然销毁）。
// 结果落到 0 档以下 → 销毁；顶过先古 → 模组专属先古的溢出分支。
// 极精加工失败只销毁投入物，不再附赠【愧疚】（设计稿第 37 行的旧条款已作废）。
[RegisterSharedEvent]
public sealed class Scp914 : OtherworldEvent
{
    private const int CoarseCost = 50;
    private const int FineCost = 120;
    private const int VeryFineCost = 200;
    private const int MustDieSeriesChancePercent = 5;

    private CardModel? _selectedCard;
    private RelicModel? _selectedRelic;
    private bool _finished;

    // Dedicated SCP-914 event background; keep it separate from other event art and card portraits.
    public override EventAssetProfile AssetProfile => new(
        InitialPortraitPath: "res://OtherworldTreasures/images/events/scp_914.png");

    protected override bool AncientExempt => true;

    public override bool IsAllowed(IRunState runState) => base.IsAllowed(runState) &&
        runState.Players.Any(p => HasCards(p) || HasRelics(p));

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        var options = new List<EventOption>();
        if (HasCards(Owner))
            options.Add(new EventOption(this, SelectCard, InitialOptionKey("CARD")));
        if (HasRelics(Owner))
            options.Add(new EventOption(this, SelectRelic, InitialOptionKey("RELIC")));
        options.Add(Leave());
        return options;
    }

    private static bool HasCards(MegaCrit.Sts2.Core.Entities.Players.Player player) =>
        player.Deck.Cards.Any(IsProcessableCard);

    private static bool HasRelics(MegaCrit.Sts2.Core.Entities.Players.Player player) =>
        player.Relics.Any(IsSafelyRemovableRelic);

    // 2026-10-18：带【永恒】的诅咒原本不可移除，允许投入精炼机并据此移出牌组。
    private static bool IsEternalCurse(CardModel card) =>
        card.Type == CardType.Curse && card.Keywords.Contains(CardKeyword.Eternal);

    private static bool IsProcessableCard(CardModel card) =>
        (card.IsRemovable || IsEternalCurse(card)) &&
        Scp914Ranks.RankOf(card.Rarity) != Scp914Ranks.Destroyed;

    // IsTradable excludes all Ancient relics. SCP-914 explicitly accepts them,
    // while retaining the safety exclusions that prevent loss of one-shot effects,
    // pets, waxed or exhausted relics.
    // 2026-10-17：稀有度不再是过滤条件——先古、商店、事件、起始遗物都能投入，
    // 旧实现把 Shop/Event/Starter/None 排除在外，玩家反馈"部分遗物无法选中"。
    private static bool IsSafelyRemovableRelic(RelicModel relic) =>
        !relic.IsUsedUp && !relic.HasUponPickupEffect && !relic.IsMelted && !relic.IsWax &&
        !relic.SpawnsPets && Scp914Ranks.RankOf(relic.Rarity) != Scp914Ranks.Destroyed;

    private async Task SelectCard()
    {
        // 用 FromDeckGeneric 而不是 FromDeckForRemoval：后者写死了 c.IsRemovable，会把【永恒】诅咒挡在外面。
        var selected = (await CardSelectCmd.FromDeckGeneric(
            Owner,
            new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt, 1),
            IsProcessableCard)).FirstOrDefault();
        if (selected == null)
        {
            // 2026-10-17：原版事件会先禁用全部选项按钮再等待本回调，
            // 这里裸 return 会让界面卡死（点跳过/取消后没有任何响应）。
            // 取消时统一回到初始页，由新生成的选项按钮恢复可点状态。
            SetEventState(InitialDescription, GenerateInitialOptions());
            return;
        }
        _selectedCard = selected;
        _selectedRelic = null;
        ShowProcessingOptions("CARD_SELECTED", selected.Title, [HoverTipFactory.FromCard(selected)]);
    }

    private Task SelectRelic()
    {
        // 2026-10-17：不再调用原版遗物选择界面（遗物多时会单行溢出屏幕、取消还会卡死），
        // 改为进入"投料页"，由玩家直接点顶部遗物栏里任意一件遗物投入。
        Scp914Pickup.Arm(this);
        SetEventState(PageDescription("RELIC_PICK"), new[]
        {
            new EventOption(this, ReturnToInitial, ModOptionKey("RELIC_PICK", "BACK"))
        });
        return Task.CompletedTask;
    }

    // 遗物栏左键接管（见 Scp914Pickup.cs 的补丁）点击后的回调。
    internal void OnRelicPickedFromBar(RelicModel relic)
    {
        if (_finished) return;
        Scp914Pickup.Disarm();
        if (!IsSafelyRemovableRelic(relic) || !Owner.Relics.Contains(relic))
        {
            SetEventState(PageDescription("INPUT_UNAVAILABLE"), new[]
            {
                new EventOption(this, ReturnToInitial, ModOptionKey("INPUT_UNAVAILABLE", "BACK"))
            });
            return;
        }
        _selectedRelic = relic;
        _selectedCard = null;
        ShowProcessingOptions("RELIC_SELECTED", relic.Title.GetFormattedText(), relic.HoverTips);
    }

    private void ShowProcessingOptions(string page, string itemName, IEnumerable<IHoverTip> tips)
    {
        var description = PageDescription(page);
        description.Add("Item", itemName);
        SetEventState(description, new[]
        {
            ProcessingOption(page, "COARSE", CoarseCost, tips),
            ProcessingOption(page, "FINE", FineCost, tips),
            ProcessingOption(page, "VERY_FINE", VeryFineCost, tips),
            new EventOption(this, ReturnToInitial, ModOptionKey(page, "BACK"))
        });
    }

    private EventOption ProcessingOption(string page, string mode, int cost, IEnumerable<IHoverTip> tips) =>
        new(this, Owner.Gold >= cost ? () => Process(mode, cost) : null, ModOptionKey(page, mode), tips);

    private Task ReturnToInitial()
    {
        Scp914Pickup.Disarm();
        _selectedCard = null;
        _selectedRelic = null;
        SetEventState(InitialDescription, GenerateInitialOptions());
        return Task.CompletedTask;
    }

    private async Task Process(string mode, int cost)
    {
        if (_finished) return;
        if (_selectedCard != null && (!Owner.Deck.Cards.Contains(_selectedCard) || !IsProcessableCard(_selectedCard)) ||
            _selectedRelic != null && (!Owner.Relics.Contains(_selectedRelic) || !IsSafelyRemovableRelic(_selectedRelic)))
        {
            SetEventState(PageDescription("INPUT_UNAVAILABLE"), new[]
            {
                new EventOption(this, ReturnToInitial, ModOptionKey("INPUT_UNAVAILABLE", "BACK"))
            });
            return;
        }

        var plan = _selectedCard != null ? PlanCard(mode, _selectedCard) :
            _selectedRelic != null ? PlanRelic(mode, _selectedRelic) : null;
        if (plan == null)
        {
            SetEventState(PageDescription("NO_RESULT"), new[]
            {
                new EventOption(this, ReopenProcessingOptions, ModOptionKey("NO_RESULT", "BACK"))
            });
            return;
        }

        // Claim the run-wide slot before consuming the input or gold. The token
        // cards are otherwise absent from normal card pools and can only be made here.
        if (plan.Cards.Any(card => card is IMustDieSeriesCard) &&
            !MustDieRunData.TryClaim(Owner.RunState))
        {
            SetEventState(PageDescription("NO_RESULT"), new[]
            {
                new EventOption(this, ReopenProcessingOptions, ModOptionKey("NO_RESULT", "BACK"))
            });
            return;
        }

        await PlayerCmd.LoseGold(cost, Owner, GoldLossType.Spent);
        if (_selectedCard != null)
        {
            await CardPileCmd.RemoveFromDeck(_selectedCard);
            var addedCards = new List<CardPileAddResult>();
            foreach (var card in plan.Cards)
                addedCards.Add(await CardPileCmd.Add(card, PileType.Deck));
            if (addedCards.Count > 0)
                CardCmd.PreviewCardPileAdd(addedCards, 1.1f);
        }
        else if (_selectedRelic != null)
        {
            await RelicCmd.Remove(_selectedRelic);
            foreach (var relic in plan.Relics)
                await RelicCmd.Obtain(relic, Owner);
        }

        // 加工完不结束事件：回到初始页，玩家想走再点离开（用户 2026-10-17）。
        Scp914Pickup.Disarm();
        var resultDescription = PageDescription(plan.ResultPage);
        IReadOnlyList<string> outputNames = plan.Cards.Count > 0
            ? plan.Cards.Select(card => Scp914Colors.Tag(Scp914Colors.Of(card.Rarity), card.Title)).ToArray()
            : plan.Relics.Select(relic => Scp914Colors.Tag(Scp914Colors.Of(relic.Rarity), relic.Title.GetFormattedText())).ToArray();
        if (outputNames.Count > 0)
            resultDescription.Add("Items", string.Join(LocManager.Instance.Language == "zhs" ? "、" : ", ", outputNames));
        SetEventState(resultDescription, new[]
        {
            new EventOption(this, ReturnToInitial, ModOptionKey("RESULT", "MORE")),
            Leave()
        });
    }

    private Task ReopenProcessingOptions()
    {
        if (_selectedCard != null)
            ShowProcessingOptions("CARD_SELECTED", _selectedCard.Title, [HoverTipFactory.FromCard(_selectedCard)]);
        else if (_selectedRelic != null)
            ShowProcessingOptions("RELIC_SELECTED", _selectedRelic.Title.GetFormattedText(), _selectedRelic.HoverTips);
        else
            SetEventState(InitialDescription, GenerateInitialOptions());
        return Task.CompletedTask;
    }

    // === 位移制：卡牌 ===
    // 只决定"往哪一档走"，产物由 CardStep 统一翻译。
    private ProcessingPlan? PlanCard(string mode, CardModel original)
    {
        int rank = Scp914Ranks.RankOf(original.Rarity);
        if (mode == "COARSE")
        {
            // 1:1 档：以原地置换为主，但仍会小幅升/降档（用户 2026-10-17 强调：1:1 也可能升或降）。
            int roll = Rng.NextInt(100);
            if (roll < 75)
            {
                // 诅咒没有别的同档物品可选，出来的还是诅咒（用户 2026-10-17「诅咒变诅咒」）。
                if (rank == Scp914Ranks.MinRank)
                {
                    return CardsPlan([CreateCurseCard(original.Id)], "CURSE_EXCHANGE_CARD");
                }
                return CardsPlan([CreateCard(rank, false, original.Id)], "SAME_CARD");
            }
            if (roll < 95) return CardStep(Scp914Ranks.Move(rank, 1), "UPGRADED_CARD");
            return CardStep(Scp914Ranks.Move(rank, -1), "DOWNGRADED_CARD");
        }

        if (mode == "FINE")
        {
            // 精加工（2026-10-18 调整）：60% 升一档、35% 品质不变、5% 升两档。
            int roll = Rng.NextInt(100);
            if (roll < 60) return CardStep(Scp914Ranks.Move(rank, 1), "UPGRADED_CARD");
            if (roll < 95)
            {
                if (rank == Scp914Ranks.MinRank) return CardsPlan([CreateCurseCard(original.Id)], "CURSE_EXCHANGE_CARD");
                return CardsPlan([CreateCard(rank, false, original.Id)], "SAME_CARD");
            }
            return CardStep(Scp914Ranks.Move(rank, 2), "GREAT_SUCCESS_CARD");
        }

        if (mode == "VERY_FINE")
        {
            if (Rng.NextInt(100) >= Scp914Ranks.VeryFineSuccessPercent(rank))
            {
                // 失败按位移 -5 处理，必然低于 0 档 → 销毁；不再给【愧疚】。
                return DestroyPlan("DAMAGED_CARD");
            }
            if (TryCreateMustDieSeriesCard(out var seriesCard))
                return CardsPlan([seriesCard], "MUST_DIE_CARD");
            return CardStep(Scp914Ranks.Move(rank, 2), "VERY_FINE_SUCCESS_CARD");
        }
        return null;
    }

    // 目标档位 → 实际产物：销毁 / 双份稀有 / 先古特殊 / 单张该档位卡。
    private ProcessingPlan? CardStep(int targetRank, string page)
    {
        if (Scp914Ranks.IsDestroyed(targetRank)) return DestroyPlan("DESTROYED_CARD");
        if (Scp914Ranks.IsOverflow(targetRank))
        {
            return CardsPlan([CreateModuleCard(Scp914Ranks.MaxRank)], "ANCIENT_SPECIAL_CARD");
        }
        if (Scp914Ranks.IsDouble(targetRank))
        {
            // 2026-10-18：档 3 的产物统一为「一件稀有 + 一件罕见」（卡牌与遗物一一对应）。
            return CardsPlan([CreateCard(2, false), CreateCard(1, false)], "RARE_AND_UNCOMMON_CARDS");
        }
        return CardsPlan([CreateCard(targetRank, false)], page);
    }

    // === 位移制：遗物 ===
    private ProcessingPlan? PlanRelic(string mode, RelicModel original)
    {
        int rank = Scp914Ranks.RankOf(original.Rarity);
        if (mode == "COARSE")
        {
            // 1:1 档：以原地置换为主，但仍会小幅升/降档（用户 2026-10-17 强调：1:1 也可能升或降）。
            int roll = Rng.NextInt(100);
            if (roll < 75) return RelicsPlan([CreateRelic(rank, false, original.Id)], "SAME_RELIC");
            if (roll < 95) return RelicStep(Scp914Ranks.Move(rank, 1), "UPGRADED_RELIC");
            return RelicStep(Scp914Ranks.Move(rank, -1), "DOWNGRADED_RELIC");
        }

        if (mode == "FINE")
        {
            // 精加工（2026-10-18 调整）：60% 升一档、35% 品质不变、5% 升两档。
            int roll = Rng.NextInt(100);
            if (roll < 60) return RelicStep(Scp914Ranks.Move(rank, 1), "UPGRADED_RELIC");
            if (roll < 95) return RelicsPlan([CreateRelic(rank, false, original.Id)], "SAME_RELIC");
            return RelicStep(Scp914Ranks.Move(rank, 2), "GREAT_SUCCESS_RELIC");
        }

        if (mode == "VERY_FINE")
        {
            if (Rng.NextInt(100) >= Scp914Ranks.VeryFineSuccessPercent(rank))
            {
                return DestroyPlan("DAMAGED_RELIC");
            }
            if (TryCreateMustDieSeriesCard(out var seriesCard))
                return CardsPlan([seriesCard], "MUST_DIE_CARD");
            return RelicStep(Scp914Ranks.Move(rank, 2), "VERY_FINE_SUCCESS_RELIC");
        }
        return null;
    }

    private ProcessingPlan? RelicStep(int targetRank, string page)
    {
        if (Scp914Ranks.IsDestroyed(targetRank)) return DestroyPlan("DESTROYED_RELIC");
        if (Scp914Ranks.IsOverflow(targetRank))
        {
            return RelicsPlan([CreateRelic(Scp914Ranks.MaxRank, true)], "ANCIENT_SPECIAL_RELIC");
        }
        if (Scp914Ranks.IsDouble(targetRank))
        {
            // 2026-10-18：档 3 的遗物产物由「两件稀有」改为「一件稀有 + 一件罕见」。
            return RelicsPlan([CreateRelic(2, false), CreateRelic(1, false)], "RARE_AND_UNCOMMON_RELICS");
        }
        return RelicsPlan([CreateRelic(targetRank, false)], page);
    }

    // 产出侧只按"纯稀有度"取牌：0 档只给普通卡，绝不再混入诅咒/状态/基础牌。
    private static bool IsOutputCardRarity(CardRarity rarity, int rank) => rank switch
    {
        0 => rarity == CardRarity.Common,
        1 => rarity == CardRarity.Uncommon,
        2 => rarity == CardRarity.Rare,
        Scp914Ranks.MaxRank => rarity == CardRarity.Ancient,
        _ => false
    };

    private bool TryCreateMustDieSeriesCard(out CardModel? card)
    {
        card = null;
        if (MustDieRunData.HasObtained(Owner.RunState) || Rng.NextInt(100) >= MustDieSeriesChancePercent)
            return false;
        var candidates = ModelDb.AllCards
            .Where(model => model is IMustDieSeriesCard)
            .OrderBy(model => model.Id.Entry, StringComparer.Ordinal)
            .ToList();
        if (candidates.Count == 0) return false;
        card = Owner.RunState.CreateCard(Rng.NextItem(candidates), Owner);
        return true;
    }

    // 产出侧只按纯稀有度取遗物。事件遗物只能投入、永不产出（用户 2026-10-17）。
    private static bool IsOutputRelicRarity(RelicRarity rarity, int rank) => rank switch
    {
        0 => rarity == RelicRarity.Common,
        1 => rarity == RelicRarity.Uncommon,
        2 => rarity is RelicRarity.Rare or RelicRarity.Shop,
        Scp914Ranks.MaxRank => rarity == RelicRarity.Ancient,
        _ => false
    };

    private CardModel? CreateCard(int rank, bool moduleOnly, ModelId? exclude = null)
    {
        IEnumerable<CardModel> pools = Owner.UnlockState.CardPools
            .SelectMany(p => p.GetUnlockedCards(Owner.UnlockState, Owner.RunState.CardMultiplayerConstraint))
            .Where(c => IsOutputCardRarity(c.Rarity, rank) && c.Type != CardType.Quest &&
                        c is not IDerivedCard && c is not IMustDieSeriesCard);
        if (moduleOnly) pools = pools.Where(c => c.GetType().Assembly == typeof(Scp914).Assembly);
        var candidates = pools.DistinctBy(c => c.Id).ToList();
        if (exclude != null && candidates.Count > 1) candidates.RemoveAll(c => c.Id == exclude);
        return candidates.Count == 0 ? null : Owner.RunState.CreateCard(Rng.NextItem(candidates), Owner);
    }

    private CardModel? CreateModuleCard(int rank) => CreateCard(rank, true);

    // 1:1 档里的诅咒：产出仍然是诅咒（用户 2026-10-17「诅咒变诅咒」）。
    private CardModel? CreateCurseCard(ModelId? exclude = null)
    {
        IEnumerable<CardModel> pools = Owner.UnlockState.CardPools
            .SelectMany(p => p.GetUnlockedCards(Owner.UnlockState, Owner.RunState.CardMultiplayerConstraint))
            .Where(c => c.Rarity == CardRarity.Curse && c.Type != CardType.Quest && c is not IDerivedCard);
        var candidates = pools.DistinctBy(c => c.Id).ToList();
        if (exclude != null && candidates.Count > 1) candidates.RemoveAll(c => c.Id == exclude);
        return candidates.Count == 0 ? null : Owner.RunState.CreateCard(Rng.NextItem(candidates), Owner);
    }

    private RelicModel? CreateRelic(int rank, bool moduleOnly, ModelId? exclude = null) =>
        CreateRelics(rank, 1, moduleOnly, exclude)?.FirstOrDefault();

    private IReadOnlyList<RelicModel?>? CreateRelics(int rank, int count, bool moduleOnly, ModelId? exclude = null)
    {
        IEnumerable<RelicModel> pool = Owner.UnlockState.Relics.Where(r => IsOutputRelicRarity(r.Rarity, rank));
        if (moduleOnly) pool = pool.Where(r => r.GetType().Assembly == typeof(Scp914).Assembly);
        var candidates = pool.DistinctBy(r => r.Id)
            .Where(r => r.IsStackable || (!Owner.Relics.Any(owned => owned.Id == r.Id) && _selectedRelic?.Id != r.Id))
            .ToList();
        if (exclude != null) candidates.RemoveAll(r => r.Id == exclude);
        var selected = new List<RelicModel?>(count);
        for (int i = 0; i < count; i++)
        {
            if (candidates.Count == 0) return null;
            var chosen = candidates[Rng.NextInt(candidates.Count)];
            selected.Add(chosen.ToMutable());
            if (!chosen.IsStackable) candidates.RemoveAll(r => r.Id == chosen.Id);
        }
        return selected;
    }

    private static ProcessingPlan? CardsPlan(IReadOnlyList<CardModel?> cards, string page) =>
        cards.Any(c => c == null) ? null : new ProcessingPlan(cards.Cast<CardModel>().ToArray(), [], page);

    private static ProcessingPlan? RelicsPlan(IReadOnlyList<RelicModel?> relics, string page) =>
        relics.Any(r => r == null) ? null : new ProcessingPlan([], relics.Cast<RelicModel>().ToArray(), page);

    private static ProcessingPlan DestroyPlan(string page) => new([], [], page);

    private sealed record ProcessingPlan(
        IReadOnlyList<CardModel> Cards,
        IReadOnlyList<RelicModel> Relics,
        string ResultPage);
}
