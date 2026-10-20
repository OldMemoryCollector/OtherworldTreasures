using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using OtherworldTreasures.Scripts.Enchantments;
using OtherworldTreasures.Scripts.RestSite;
using STS2RitsuLib.Combat.Ui.ExtraCornerAmountLabels;
using STS2RitsuLib.Interactions.RightClick;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Relics;

[RegisterRelic(typeof(SharedRelicPool))]
public sealed class EnchantingTable : ModRelicTemplate, IModRightClickableRelic,
    IRelicExtraIconAmountLabelSpecsProvider, IRelicExtraIconAmountLabelsChangeSource,
    RelicRightClickSync.ICrossEndRightClickEffect
{
    public const int TemporaryCost = 1;
    public const int PermanentCost = 5;
    public const int RestXp = 15;
    private int _savedTotalExperience;
    private string _temporaryDeckIndexes = "[]";
    private bool _preserveRestSiteChoices;
    private readonly HashSet<Creature> _awardedThisCombat = new();
    private readonly Dictionary<Creature, int> _combatOriginalMaxHp = new();

    [SavedProperty] public int SavedTotalExperience { get => _savedTotalExperience; set { AssertMutable(); _savedTotalExperience = Math.Max(0, value); RefreshUi(); } }
    [SavedProperty] public string TemporaryDeckIndexes { get => _temporaryDeckIndexes; set { AssertMutable(); _temporaryDeckIndexes = string.IsNullOrWhiteSpace(value) ? "[]" : value; } }

    public int Level => ExperienceLevelFromTotal(SavedTotalExperience);
    public int ExperienceIntoLevel => SavedTotalExperience - TotalExperienceForLevel(Level);
    public int ExperienceForNextLevel => TotalExperienceForLevel(Level + 1) - TotalExperienceForLevel(Level);
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://OtherworldTreasures/images/relics/EnchantingTable_85.png",
        IconOutlinePath: "res://OtherworldTreasures/images/relics/EnchantingTable_85.png",
        BigIconPath: "res://OtherworldTreasures/images/relics/EnchantingTable.png");
    public override string PackedIconPath => "res://OtherworldTreasures/images/relics/EnchantingTable_85.png";
    protected override string PackedIconOutlinePath => "res://OtherworldTreasures/images/relics/EnchantingTable_85.png";
    protected override string BigIconPath => "res://OtherworldTreasures/images/relics/EnchantingTable.png";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Level", 0m), new DynamicVar("Progress", 0m), new DynamicVar("NextLevel", 0m)];
    public event Action? RelicExtraIconAmountLabelsInvalidated;

    public override Task AfterObtained() { RepairInvalidEnchantments(Owner); RefreshUi(); return base.AfterObtained(); }

    public override Task BeforeCombatStartLate()
    {
        RepairInvalidEnchantments(Owner);
        _combatOriginalMaxHp.Clear();
        var state = Owner?.Creature.CombatState;
        if (state != null)
            foreach (var enemy in state.Enemies) _combatOriginalMaxHp[enemy] = enemy.MaxHp;
        _awardedThisCombat.Clear();
        return base.BeforeCombatStartLate();
    }

    public override async Task AfterDeath(PlayerChoiceContext context, Creature creature, bool wasRemovalPrevented, float deathAnimLength)
    {
        if (creature.Side == CombatSide.Enemy && !wasRemovalPrevented && _awardedThisCombat.Add(creature))
        {
            int hp = _combatOriginalMaxHp.TryGetValue(creature, out var original) ? original : creature.MaxHp;
            SavedTotalExperience += Math.Max(1, hp / 6);
            RefreshUi();
        }
        await base.AfterDeath(context, creature, wasRemovalPrevented, deathAnimLength);
    }

    public override Task AfterRestSiteHeal(Player player, bool isMimicked)
    {
        if (player == Owner) SavedTotalExperience += RestXp;
        return base.AfterRestSiteHeal(player, isMimicked);
    }
    public override Task AfterRestSiteSmith(Player player)
    {
        if (player == Owner) SavedTotalExperience += RestXp;
        return base.AfterRestSiteSmith(player);
    }

    public override bool TryModifyRestSiteOptions(Player player, ICollection<RestSiteOption> options)
    {
        if (player != Owner || Level < PermanentCost || options.Any(x => x.OptionId == "ENCHANTING_TABLE")) return false;
        if (!Owner.Deck.Cards.Any(HasAnyLegalEnchantment)) return false;
        options.Add(new EnchantingTableForgeOption(player, this));
        return true;
    }

    public override bool ShouldDisableRemainingRestSiteOptions(Player player)
    {
        if (player == Owner && _preserveRestSiteChoices)
        {
            _preserveRestSiteChoices = false;
            return false;
        }
        return true;
    }

    public bool CanExecuteRightClick(ModRightClickExecutionContext context) =>
        Owner != null && CombatManager.Instance.DebugOnlyGetState() != null && Level >= TemporaryCost
        && Owner.PlayerCombatState?.AllCards.Any(HasAnyLegalEnchantment) == true;

    public Task OnRightClick(ModRightClickExecutionContext context)
    {
        if (Owner != null && Entry.IsLocalPlayer(Owner)) RelicRightClickSync.Request(Owner, this);
        return Task.CompletedTask;
    }

    public async Task ApplyRightClickAsync(PlayerChoiceContext context)
    {
        var combat = Owner?.PlayerCombatState;
        if (Owner == null || combat == null || Level < TemporaryCost) return;
        var candidates = combat.AllCards.Where(HasAnyLegalEnchantment).ToList();
        if (candidates.Count == 0) return;
        var selected = (await CardSelectCmd.FromSimpleGrid(context, candidates, Owner,
            new CardSelectorPrefs(CardSelectorPrefs.EnchantSelectionPrompt, 1) { Cancelable = true })).FirstOrDefault();
        if (selected == null || !EnchantForLevels(selected, TemporaryCost)) return;
        int index = selected.DeckVersion == null ? -1 : Owner.Deck.Cards.ToList().IndexOf(selected.DeckVersion);
        if (index >= 0)
        {
            var indexes = JsonSerializer.Deserialize<HashSet<int>>(TemporaryDeckIndexes) ?? [];
            indexes.Add(index);
            TemporaryDeckIndexes = JsonSerializer.Serialize(indexes);
        }
        RefreshUi();
        // Native card preview shows the card with its newly applied enchantment.
        CardCmd.Preview(selected, 1.5f);
    }

    public override async Task AfterCombatEnd(CombatRoom room)
    {
        try
        {
            var indexes = JsonSerializer.Deserialize<HashSet<int>>(TemporaryDeckIndexes) ?? [];
            var combat = Owner?.PlayerCombatState;
            if (combat != null)
                foreach (var card in combat.AllCards.ToArray())
                    if (card.Enchantment != null && card.DeckVersion != null && Owner!.Deck.Cards.ToList().IndexOf(card.DeckVersion) is var idx && indexes.Contains(idx))
                        CardCmd.ClearEnchantment(card);
        }
        catch (Exception e) { Entry.Logger.Warn($"[EnchantingTable] 临时附魔清理失败，将随战斗卡牌销毁：{e.Message}"); }
        TemporaryDeckIndexes = "[]";
        _combatOriginalMaxHp.Clear();
        await base.AfterCombatEnd(room);
    }

    public bool HasAnyLegalEnchantment(CardModel card) => card != null && card.Enchantment == null && IsInPlayerDeck(card)
        && GetLegalEnchantments(card).Count != 0;

    private bool IsInPlayerDeck(CardModel card)
    {
        if (card.Owner != Owner || card.Type is CardType.Status or CardType.Curse or CardType.Quest) return false;
        if (card.Rarity == CardRarity.Event) return false;
        if (card.Pile?.Type != PileType.Deck && card.DeckVersion == null) return false;
        if (card.Pile?.Type == PileType.Deck && card.Keywords.Contains(CardKeyword.Unplayable)) return false;
        return true;
    }

    private static readonly HashSet<string> ExcludedEnchantments = new(StringComparer.Ordinal)
    {
        "DeprecatedEnchantment", "MockFreeEnchantment", "RoyallyApproved", "Imbued", "Clone", "SoulsPower", "TezcatarasEmber",
        "SerializationKeys"
    };
    private static readonly HashSet<Type> CompatibilityWarnings = new();
    internal static List<EnchantmentModel> GetLegalEnchantments(CardModel card)
    {
        var result = new List<EnchantmentModel>();
        var seen = new HashSet<Type>();
        foreach (var canonical in ModelDb.DebugEnchantments)
        {
            var type = canonical.GetType();
            if (ExcludedEnchantments.Contains(type.Name) || !seen.Add(type)) continue;
            try
            {
                var enchantment = canonical.ToMutable();
                if (EnchantingTableCompatibility.CanApply(enchantment, card)) result.Add(enchantment);
            }
            catch (Exception error)
            {
                if (CompatibilityWarnings.Add(type))
                    Entry.Logger.Warn($"[EnchantingTable] Cannot check {type.Name} on {card.Id.Entry}; skipping: {error.Message}");
            }
        }
        return result;
    }

    internal static void RepairInvalidEnchantments(Player player)
    {
        foreach (var card in player.Deck.Cards.ToArray()) EnchantingTableCompatibility.Repair(card);
        if (player.PlayerCombatState is { } combat)
            foreach (var card in combat.AllCards.ToArray()) EnchantingTableCompatibility.Repair(card);
    }
    public override Task AfterCardEnteredCombat(CardModel card)
    {
        if (card.Owner == Owner) EnchantingTableCompatibility.Repair(card);
        return Task.CompletedTask;
    }
    public override Task BeforeCardPlayed(CardPlay play)
    {
        if (play.Card.Owner == Owner) EnchantingTableCompatibility.Repair(play.Card);
        return Task.CompletedTask;
    }

    public bool ApplyRandomEnchantment(CardModel card)
    {
        if (card.Enchantment != null || !IsInPlayerDeck(card)) return false;
        var candidates = GetLegalEnchantments(card);
        if (candidates.Count == 0) return false;
        var rng = Owner?.PlayerRng.Rewards;
        if (rng == null) return false;
        var enchantment = candidates[rng.NextInt(candidates.Count)];
        try
        {
            if (!EnchantingTableCompatibility.CanApply(enchantment, card)) return false;
            return ReferenceEquals(CardCmd.Enchant(enchantment, card, 1m), enchantment);
        }
        catch (Exception e)
        {
            if (ReferenceEquals(card.Enchantment, enchantment)) CardCmd.ClearEnchantment(card);
            Entry.Logger.Warn($"[EnchantingTable] 对 {card.Id.Entry} 施加 {enchantment.GetType().Name} 失败：{e.Message}");
            return false;
        }
    }

    public bool EnchantForLevels(CardModel card, int levels)
    {
        if (!HasAnyLegalEnchantment(card)) return false;
        int previousExperience = SavedTotalExperience;
        if (!SpendLevels(levels)) return false;
        if (ApplyRandomEnchantment(card)) return true;

        // Keep the table's XP intact if a third-party enchantment fails during application.
        SavedTotalExperience = previousExperience;
        RefreshUi();
        return false;
    }

    public bool SpendLevels(int levels)
    {
        if (levels < 1 || Level < levels) return false;
        int xp = SavedTotalExperience;
        for (int i = 0; i < levels; i++)
        {
            int level = ExperienceLevelFromTotal(xp);
            xp -= TotalExperienceForLevel(level) - TotalExperienceForLevel(level - 1);
        }
        SavedTotalExperience = Math.Max(0, xp);
        return true;
    }

    internal void PreserveRestSiteChoices() => _preserveRestSiteChoices = true;

    public static int TotalExperienceForLevel(int level)
    {
        level = Math.Max(0, level);
        double total = level <= 16 ? (double)level * level + 6d * level
            : level <= 31 ? 2.5d * level * level - 40.5d * level + 360d
            : 4.5d * level * level - 162.5d * level + 2220d;
        return (int)Math.Floor(total);
    }
    public static int ExperienceLevelFromTotal(int xp)
    {
        int low = 0, high = 10000;
        while (low < high)
        {
            int mid = low + (high - low + 1) / 2;
            if (TotalExperienceForLevel(mid) <= xp) low = mid; else high = mid - 1;
        }
        return low;
    }

    public void RefreshUi()
    {
        if (!IsMutable) return;
        try
        {
            DynamicVars["Level"].BaseValue = Level;
            DynamicVars["Progress"].BaseValue = ExperienceIntoLevel;
            DynamicVars["NextLevel"].BaseValue = ExperienceForNextLevel;
            (LiveRelic ?? this).RelicExtraIconAmountLabelsInvalidated?.Invoke();
        }
        catch { }
    }
    private EnchantingTable? LiveRelic => Owner?.Relics?.OfType<EnchantingTable>().FirstOrDefault();
    public IReadOnlyList<ExtraIconAmountLabelSpec> GetRelicExtraIconAmountLabelSpecs() =>
        !IsMutable || !(RunManager.Instance?.IsInProgress ?? false) ? Array.Empty<ExtraIconAmountLabelSpec>() :
        [ExtraIconAmountLabelSpec.RichText(ExtraIconAmountLabelCorner.BottomRight, $"[font_size=16][color=#8de05b]{Level}[/color][/font_size]")];
}
