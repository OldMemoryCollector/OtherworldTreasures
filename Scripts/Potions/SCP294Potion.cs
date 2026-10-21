using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Combat;
using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models.PotionPools;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using OtherworldTreasures.Scripts.Events;
using OtherworldTreasures.Scripts.Powers;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Scaffolding.Content.Patches;

namespace OtherworldTreasures.Scripts.Potions;

[RegisterPotion(typeof(EventPotionPool))]
public sealed partial class SCP294Potion : ModPotionTemplate
{
    private const string SavePrefix = "BREW_";
    private static readonly ConditionalWeakTable<PotionModel, BrewRecipe> Recipes = new();
    private static readonly ConcurrentDictionary<string, Texture2D> MixedIconCache = new();
    private static readonly Color[] SoftTintPalette =
    [
        new("df4969"), new("e87945"), new("dfb343"), new("a7c454"),
        new("49ae83"), new("45b6c4"), new("587bdc"), new("9664ce"),
        new("bb68aa"), new("a7ccd2"), new("c65d42"), new("6e9cb0")
    ];
    // Only use original potion art whose silhouette reads as a vessel or vial.
    private static readonly string[] NativeBottleEntries =
    [
        "attack_potion", "ashwater", "block_potion", "blood_potion", "bone_brew",
        "bottled_potential", "clarity", "colorless_potion", "cure_all", "dexterity_potion",
        "distilled_chaos", "explosive_ampoule", "fire_potion", "focus_potion", "foul_potion",
        "fruit_juice", "fysh_oil", "gigantification_potion", "glowwater_potion", "heart_of_iron",
        "kings_courage", "liquid_memories", "mazaleths_gift", "orobic_acid", "poison_potion",
        "potion_of_capacity", "potion_of_doom", "powdered_demise", "power_potion", "radiant_tincture",
        "regen_potion", "shackling_potion", "snecko_oil", "speed_potion", "stable_serum",
        "strength_potion", "swift_potion", "touch_of_insanity", "vulnerable_potion", "weak_potion"
    ];
    // The color comes from the actual liquid area of a second vanilla potion icon.
    private static readonly string[] NativeLiquidEntries =
    [
        "attack_potion", "ashwater", "block_potion", "blood_potion", "bottled_potential",
        "clarity", "cure_all", "dexterity_potion", "explosive_ampoule", "fire_potion",
        "focus_potion", "fruit_juice", "fysh_oil", "gigantification_potion", "glowwater_potion",
        "heart_of_iron", "kings_courage", "liquid_memories", "mazaleths_gift", "orobic_acid",
        "poison_potion", "potion_of_capacity", "potion_of_doom", "powdered_demise", "power_potion",
        "radiant_tincture", "regen_potion", "shackling_potion", "snecko_oil", "speed_potion",
        "stable_serum", "strength_potion", "swift_potion", "touch_of_insanity", "vulnerable_potion",
        "weak_potion"
    ];
    private static readonly BrewPreset[] SpecialPresets =
    [
        new("perfect", 1, "radiant_tincture", [
            new(BrewEffect.Heal, 20, BrewRecipient.Self),
            new(BrewEffect.Regen, 5, BrewRecipient.Self),
            new(BrewEffect.Absorption, 20, BrewRecipient.Self),
            new(BrewEffect.Strength, 5, BrewRecipient.Self),
            new(BrewEffect.Dexterity, 5, BrewRecipient.Self),
            new(BrewEffect.Block, 30, BrewRecipient.Self),
            new(BrewEffect.Buffer, 2, BrewRecipient.Self),
            new(BrewEffect.Artifact, 2, BrewRecipient.Self),
            new(BrewEffect.Intangible, 2, BrewRecipient.Self),
            new(BrewEffect.Thorns, 5, BrewRecipient.Self),
            new(BrewEffect.Plating, 5, BrewRecipient.Self),
            new(BrewEffect.Focus, 3, BrewRecipient.Self),
            new(BrewEffect.GainEnergy, 2, BrewRecipient.Self),
            new(BrewEffect.DrawCards, 3, BrewRecipient.Self),
            new(BrewEffect.NextTurnEnergy, 1, BrewRecipient.Self)
        ]),
        new("weakening", 4, "powdered_demise", [
            new(BrewEffect.Damage, 20, BrewRecipient.SelectedEnemy),
            new(BrewEffect.Weak, 3, BrewRecipient.SelectedEnemy),
            new(BrewEffect.Vulnerable, 3, BrewRecipient.SelectedEnemy),
            new(BrewEffect.Frail, 3, BrewRecipient.SelectedEnemy),
            new(BrewEffect.StrengthDown, 3, BrewRecipient.SelectedEnemy),
            new(BrewEffect.DexterityDown, 3, BrewRecipient.SelectedEnemy),
            new(BrewEffect.Poison, 12, BrewRecipient.SelectedEnemy),
            new(BrewEffect.Doom, 3, BrewRecipient.SelectedEnemy)
        ])
    ];

    public override PotionRarity Rarity => PotionRarity.Rare;
    public override PotionUsage Usage => IsFairyOnly(this) ? PotionUsage.Automatic
        : CanUseOutsideCombat(this) ? PotionUsage.AnyTime : PotionUsage.CombatOnly;
    public override bool PassesCustomUsabilityCheck => CombatManager.Instance.IsInProgress || CanUseOutsideCombat(this);
    public override TargetType TargetType => !CombatManager.Instance.IsInProgress && HasEffect(this, BrewEffect.FoulSplash)
        ? TargetType.TargetedNoCreature : RecipeFor(this).Ingredients.Any(i => i.Recipient is BrewRecipient.SelectedEnemy or BrewRecipient.SelfAndEnemy)
        ? TargetType.AnyEnemy
        : RecipeFor(this).Ingredients.Any(i => i.Recipient is BrewRecipient.AllEnemies or BrewRecipient.BothSides)
            ? TargetType.AllEnemies
            : TargetType.Self;
    public override bool CanBeGeneratedInCombat => false;
    internal static void InitializeVisuals()
    {
        // Use RitsuLib's providers so every consumer of PotionModel.Image (including
        // the throw animation) receives the per-brew texture instead of a static asset.
        const string key = Entry.ModId + ".SCP294";
        ExternalAssetOverrideRegistry.RegisterPotionImageTextureProvider(key,
            potion => potion is SCP294Potion ? MixedIcon(potion, large: false) : null);
        ExternalAssetOverrideRegistry.RegisterPotionImagePathProvider(key,
            potion => potion is SCP294Potion ? MixedIcon(potion, large: false)?.ResourcePath : null);
        ExternalAssetOverrideRegistry.RegisterPotionOutlineTextureProvider(key,
            potion => potion is SCP294Potion ? NativeIcon(potion)?.Outline : null);
        ExternalAssetOverrideRegistry.RegisterPotionOutlinePathProvider(key,
            potion => potion is SCP294Potion ? NativeIcon(potion)?.OutlinePath : null);
    }

    internal string BrewName => RecipeFor(this).Name;

    internal string PreviewSummary
    {
        get
        {
            var ingredients = RecipeFor(this).Ingredients;
            string names = string.Join(LocManager.Instance.Language == "zhs" ? "、" : ", ",
                ingredients.Select(item => EffectName(item.Effect)).Distinct().Take(3).Select(name => $"[gold]{name}[/gold]"));
            string text = LocManager.Instance.Language switch
            {
                "eng" => $"{names}. Hover for details.",
                "jpn" => $"{names}。ホバーで詳細。",
                _ => $"{names}；悬停查看药效。"
            };
            return text;
        }
    }

    internal static SCP294Potion Brew(Scp294 eventModel, Taste taste, Feeling feeling, Concentration concentration) =>
        BrewWithRng(eventModel.Rng, taste, feeling, concentration);

    internal static SCP294Potion BrewForPlayer(Player player) => BrewRandomWithRng(player.RunState.Rng.CombatPotionGeneration);

    internal static SCP294Potion BrewRandomWithRng(Rng rng) => BrewWithRng(rng,
        rng.NextItem(Enum.GetValues<Taste>()), rng.NextItem(Enum.GetValues<Feeling>()), rng.NextItem(Enum.GetValues<Concentration>()));

    internal static SCP294Potion BrewWithRng(Rng rng, Taste taste, Feeling feeling, Concentration concentration)
    {
        var special = RollSpecialPreset(rng);
        if (special is not null)
        {
            var specialRecipe = new BrewRecipe(LocText("special." + special.NameKey),
                special.Ingredients.Select(i => Scp294Settings.CrazyMode || i.Effect == BrewEffect.Doom
                    ? i with { Amount = RollAmount(rng, i.Effect, (int)concentration, i.Recipient) } : i).ToArray(), special.IconEntry,
                LiquidEntries: ChooseLiquidEntries(rng, special.IconEntry, special.Ingredients.Count),
                VisualStyle: RollVisualStyle(rng), VisualSeed: rng.NextInt(int.MaxValue));
            var specialPotion = (SCP294Potion)ModelDb.Potion<SCP294Potion>().ToMutable();
            Recipes.Add(specialPotion, specialRecipe);
            return specialPotion;
        }

        int density = (int)concentration;
        // Legacy HealExtraTurn stays readable in old saves, but new brews roll its parts independently.
        var possible = Enum.GetValues<BrewEffect>().Where(effect => effect != BrewEffect.HealExtraTurn).ToList();
        var guaranteed = TasteEffects(taste).Where(possible.Contains).ToList();
        if (guaranteed.Count == 0) guaranteed = possible;

        int count = RollEffectCount(rng, taste, feeling);

        var ingredients = new List<BrewIngredient>(count);
        var signature = PolarityOf(feeling);
        var first = rng.NextItem(guaranteed);
        ingredients.Add(MakeIngredient(rng, first, density, feeling));
        while (ingredients.Count < count)
        {
            var effect = PickWeighted(rng, possible, signature);
            ingredients.Add(MakeIngredient(rng, effect, density, feeling));
        }

        var name = MakeName(rng, ingredients);
        var bottleEntry = ChooseNativeEntry(rng, NativeBottleEntries);
        var recipe = new BrewRecipe(name, ingredients, bottleEntry,
            LiquidEntries: ChooseLiquidEntries(rng, bottleEntry, ingredients.Count),
            VisualStyle: RollVisualStyle(rng), VisualSeed: rng.NextInt(int.MaxValue));
        var potion = (SCP294Potion)ModelDb.Potion<SCP294Potion>().ToMutable();
        Recipes.Add(potion, recipe);
        return potion;
    }

    private static BrewVisualStyle RollVisualStyle(Rng rng) => rng.NextInt(100) switch
    {
        < 40 => BrewVisualStyle.Waves,
        < 65 => BrewVisualStyle.Gradient,
        < 90 => BrewVisualStyle.Marble,
        _ => BrewVisualStyle.SoftTint
    };

    private static BrewPreset? RollSpecialPreset(Rng rng)
    {
        int roll = rng.NextInt(100);
        foreach (var preset in SpecialPresets)
        {
            if (roll < preset.ChancePercent) return preset;
            roll -= preset.ChancePercent;
        }
        return null;
    }

    private static string ChooseNativeEntry(Rng rng, IReadOnlyList<string> entries)
    {
        var available = entries.Where(entry => ModelDb.AllPotions.Any(model =>
            model.Id.Entry.Equals(entry, StringComparison.OrdinalIgnoreCase))).ToArray();
        return available.Length == 0
            ? "strength_potion"
            : available[rng.NextInt(available.Length)];
    }

    private static string[] ChooseLiquidEntries(Rng rng, string bottleEntry, int effectCount)
    {
        var available = NativeLiquidEntries.Where(entry =>
            !entry.Equals(bottleEntry, StringComparison.OrdinalIgnoreCase) &&
            ModelDb.AllPotions.Any(model => model.Id.Entry.Equals(entry, StringComparison.OrdinalIgnoreCase))).ToArray();
        if (available.Length == 0)
            available = NativeLiquidEntries.Where(entry =>
                ModelDb.AllPotions.Any(model => model.Id.Entry.Equals(entry, StringComparison.OrdinalIgnoreCase))).ToArray();

        // The three user choices determine the recipe's effect count; concentration only
        // changes each effect's strength. Use that same count for the visual mixture,
        // with at least two colors so even a one-effect brew still looks blended.
        int mixtureCount = Math.Clamp(effectCount, 2, 6);
        var candidates = available.ToList();
        var selected = new List<string>(Math.Min(mixtureCount, candidates.Count));
        while (selected.Count < mixtureCount && candidates.Count > 0)
        {
            int index = rng.NextInt(candidates.Count);
            string entry = candidates[index];
            candidates.RemoveAt(index);
            selected.Add(entry);
        }
        return selected.Count > 0 ? selected.ToArray() : [ChooseNativeEntry(rng, NativeLiquidEntries)];
    }

    // Taste and feeling shape how many effects are combined. Concentration only changes
    // effect magnitude in RollAmount and never changes the number of ingredients.
    private static int RollEffectCount(Rng rng, Taste taste, Feeling feeling)
    {
        int tasteComplexity = taste switch
        {
            Taste.Sweet or Taste.Salty or Taste.Savory => 0,
            Taste.Strange => 2,
            _ => 1
        };
        int targetComplexity = RecipientPreferenceOf(feeling) switch
        {
            BrewRecipient.AllAllies or BrewRecipient.AllEnemies or BrewRecipient.SelfAndEnemy => 1,
            BrewRecipient.BothSides => 2,
            _ => 0
        };
        return Math.Clamp(1 + tasteComplexity + targetComplexity + rng.NextInt(2), 1, 6);
    }

    private static BrewIngredient MakeIngredient(Rng rng, BrewEffect effect, int density, Feeling feeling)
    {
        var recipient = RecipientFor(rng, effect, feeling);
        return new BrewIngredient(effect, RollAmount(rng, effect, density, recipient), recipient, RollTiming(rng, effect),
            OrbEntry: effect == BrewEffect.ChannelSameOrbs ? rng.NextItem(OrbEntries) : null,
            SecondaryAmount: effect == BrewEffect.FoulSplash ? RollFoulSplashMultiplier(rng, density)
                : effect == BrewEffect.Doom && recipient is BrewRecipient.BothSides or BrewRecipient.SelfAndEnemy
                    ? RollAmount(rng, effect, density, BrewRecipient.Self) : null);
    }

    private static BrewTiming RollTiming(Rng rng, BrewEffect effect)
    {
        if (effect is not (BrewEffect.Strength or BrewEffect.StrengthDown or BrewEffect.Dexterity or
            BrewEffect.DexterityDown or BrewEffect.Absorption or BrewEffect.Buffer or
            BrewEffect.Artifact or BrewEffect.Intangible)) return BrewTiming.Immediate;
        return rng.NextInt(4) switch
        {
            0 => BrewTiming.ThisTurn,
            1 => BrewTiming.NextTurn,
            _ => BrewTiming.Immediate
        };
    }

    private static BrewRecipe RecipeFor(PotionModel potion) =>
        Recipes.TryGetValue(potion, out var recipe)
            ? recipe
            : new BrewRecipe(LocText("defaultName"),
                [new BrewIngredient(BrewEffect.Strength, 1, BrewRecipient.Self)], "strength_potion");

    private static string MakeName(Rng rng, IReadOnlyList<BrewIngredient> ingredients)
    {
        if (LocManager.Instance.Language is "eng" or "jpn")
        {
            string[] words = ingredients.Select(item => EffectName(item.Effect)).ToArray();
            for (int i = words.Length - 1; i > 0; --i)
            {
                int j = rng.NextInt(i + 1);
                (words[i], words[j]) = (words[j], words[i]);
            }
            string stem = string.Concat(words.Select(word => word[..1])).ToUpperInvariant();
            return stem + (LocManager.Instance.Language == "jpn" ? "調合薬" : " Brew");
        }

        string[] chars = ingredients.Select(item => EffectGlyph(item.Effect)
            [rng.NextInt(EffectGlyph(item.Effect).Length)].ToString()).ToArray();
        for (int i = chars.Length - 1; i > 0; --i)
        {
            int j = rng.NextInt(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
        return string.Concat(chars) + "药水";
    }

    private static BrewEffect PickWeighted(Rng rng, IReadOnlyList<BrewEffect> options, BrewPolarity polarity)
    {
        var weighted = options.Select(effect => (Effect: effect,
            Weight: polarity == BrewPolarity.Random || PolarityOf(effect) == polarity ? 2 : 1)).ToArray();
        int total = weighted.Sum(item => item.Weight);
        int roll = rng.NextInt(total);
        foreach (var item in weighted)
        {
            if (roll < item.Weight) return item.Effect;
            roll -= item.Weight;
        }
        return weighted[^1].Effect;
    }

    private static bool IsPlayerOnlyEffect(BrewEffect effect) => effect is BrewEffect.DrawCards or BrewEffect.GainEnergy or BrewEffect.NextTurnDraw or
            BrewEffect.NextTurnEnergy or BrewEffect.Clarity or BrewEffect.Radiance or BrewEffect.AttackChoice or
            BrewEffect.SkillChoice or BrewEffect.PowerChoice or BrewEffect.ColorlessChoice or BrewEffect.RetrieveDiscard or
            BrewEffect.GambleDiscardDraw or BrewEffect.UpgradeHand or BrewEffect.ExhaustHand or BrewEffect.MaxHp or
            BrewEffect.ShuffleHandIntoDraw or BrewEffect.DrawPileChoice or BrewEffect.FreeHandCard or
            BrewEffect.ThreeFreeCards or BrewEffect.AutoPlayTop or BrewEffect.RetainHand or
            BrewEffect.DuplicateNextCard or BrewEffect.TripleNextAttack or BrewEffect.RandomizeHandCosts or
            BrewEffect.ExtraTurn or BrewEffect.GainStars or BrewEffect.AddOrbSlots or
            BrewEffect.ChannelDarkOrbs or BrewEffect.ShivPlus or BrewEffect.SoulCards or BrewEffect.UpgradedColorless or
            BrewEffect.ShuffleAndDrawSame or BrewEffect.ReplayStrikes or BrewEffect.FairyRevive or
            BrewEffect.FillPotionSlots or BrewEffect.SneckoDraw or BrewEffect.FoulSplash or
            BrewEffect.HealExtraTurn or BrewEffect.AmbergrisHeal or BrewEffect.ExhaustHandDraw or BrewEffect.ChannelSameOrbs or BrewEffect.ChannelRandomOrbs;

    private static BrewRecipient RecipientFor(Rng rng, BrewEffect effect, Feeling feeling)
    {
        // Card selection, draw and energy are player-side actions in the source potions.
        if (IsPlayerOnlyEffect(effect))
            return BrewRecipient.Self;

        var preference = RecipientPreferenceOf(feeling);
        BrewRecipient[] eligible =
        [BrewRecipient.Self, BrewRecipient.SelectedEnemy, BrewRecipient.AllEnemies,
            BrewRecipient.AllAllies, BrewRecipient.BothSides, BrewRecipient.SelfAndEnemy];
        int roll = rng.NextInt(eligible.Length + 3);
        return roll < 3 ? preference : eligible[roll - 3];
    }

    private static BrewRecipient RecipientPreferenceOf(Feeling feeling) => feeling switch
    {
        Feeling.Angry or Feeling.Terrifying or Feeling.Jealous or Feeling.Eerie or Feeling.Cruel => BrewRecipient.SelectedEnemy,
        Feeling.Wonderful or Feeling.Serene or Feeling.Despairing or Feeling.Hopeful or Feeling.Light => BrewRecipient.AllAllies,
        Feeling.Mad or Feeling.Dreamlike or Feeling.Sacred or Feeling.Absurd or Feeling.Heavy => BrewRecipient.BothSides,
        Feeling.Romantic or Feeling.Gentle or Feeling.Strange => BrewRecipient.SelfAndEnemy,
        _ => BrewRecipient.Self
    };

    private static BrewPolarity PolarityOf(Feeling feeling) => feeling switch
    {
        Feeling.Sad or Feeling.Angry or Feeling.Terrifying or Feeling.Tired or Feeling.Despairing or
        Feeling.Jealous or Feeling.Cruel or Feeling.Heavy => BrewPolarity.Negative,
        Feeling.Strange or Feeling.Mad or Feeling.Lonely or Feeling.Nostalgic or Feeling.Dreamlike or
        Feeling.Eerie or Feeling.Absurd => BrewPolarity.Random,
        _ => BrewPolarity.Positive
    };

    private static IReadOnlyList<BrewEffect> TasteEffects(Taste taste) => taste switch
    {
        Taste.Sweet => [BrewEffect.Heal, BrewEffect.HealPercent, BrewEffect.Regen, BrewEffect.Absorption, BrewEffect.FairyRevive, BrewEffect.AmbergrisHeal],
        Taste.Bitter => [BrewEffect.Weak, BrewEffect.Vulnerable],
        Taste.Sour => [BrewEffect.Poison, BrewEffect.Damage, BrewEffect.Weak],
        Taste.Spicy => [BrewEffect.Damage, BrewEffect.Strength, BrewEffect.Vulnerable, BrewEffect.Thorns],
        Taste.Salty => [BrewEffect.Block, BrewEffect.Dexterity, BrewEffect.Strength, BrewEffect.Buffer, BrewEffect.Plating],
        Taste.Cool => [BrewEffect.DrawCards, BrewEffect.GainEnergy, BrewEffect.NextTurnDraw,
            BrewEffect.NextTurnEnergy, BrewEffect.Clarity, BrewEffect.Radiance, BrewEffect.AttackChoice, BrewEffect.SkillChoice,
            BrewEffect.PowerChoice, BrewEffect.ColorlessChoice, BrewEffect.RetrieveDiscard, BrewEffect.GambleDiscardDraw,
            BrewEffect.ShuffleAndDrawSame, BrewEffect.SneckoDraw, BrewEffect.ExhaustHandDraw, BrewEffect.FillPotionSlots,
            BrewEffect.ChannelSameOrbs, BrewEffect.ChannelRandomOrbs],
        Taste.Numbing => [BrewEffect.Weak, BrewEffect.Frail, BrewEffect.Poison],
        Taste.Astringent => [BrewEffect.Weak, BrewEffect.Vulnerable, BrewEffect.Damage],
        Taste.Savory => [BrewEffect.Heal, BrewEffect.Strength, BrewEffect.Block, BrewEffect.Artifact],
        _ => Enum.GetValues<BrewEffect>()
    };

    private static BrewPolarity PolarityOf(BrewEffect effect) => effect switch
    {
        BrewEffect.Heal or BrewEffect.HealPercent or BrewEffect.Regen or BrewEffect.Absorption or BrewEffect.Buffer or
        BrewEffect.Artifact or BrewEffect.Intangible or BrewEffect.Thorns or BrewEffect.Plating or
        BrewEffect.Ritual or BrewEffect.Focus or BrewEffect.MaxHp or BrewEffect.TripleBlock or
        BrewEffect.NextTurnBlock or BrewEffect.NextTurnDraw or BrewEffect.NextTurnEnergy or
        BrewEffect.Clarity or BrewEffect.Radiance or
        BrewEffect.ShuffleHandIntoDraw or BrewEffect.DrawPileChoice or BrewEffect.FreeHandCard or
        BrewEffect.ThreeFreeCards or BrewEffect.AutoPlayTop or BrewEffect.RetainHand or
        BrewEffect.DuplicateNextCard or BrewEffect.TripleNextAttack or
        BrewEffect.ExtraTurn or BrewEffect.GainStars or BrewEffect.AddOrbSlots or
        BrewEffect.ChannelDarkOrbs or BrewEffect.ShivPlus or BrewEffect.SoulCards or BrewEffect.UpgradedColorless or
        BrewEffect.ReplayStrikes or BrewEffect.ShuffleAndDrawSame or
        BrewEffect.UpgradeHand or BrewEffect.ColorlessChoice or BrewEffect.Strength or BrewEffect.Dexterity or
        BrewEffect.Block or BrewEffect.GainEnergy or BrewEffect.DrawCards or BrewEffect.AttackChoice or
        BrewEffect.SkillChoice or BrewEffect.PowerChoice or BrewEffect.RetrieveDiscard or BrewEffect.GambleDiscardDraw or BrewEffect.FairyRevive or BrewEffect.FillPotionSlots or
        BrewEffect.SneckoDraw or BrewEffect.HealExtraTurn or BrewEffect.AmbergrisHeal or BrewEffect.ExhaustHandDraw or
        BrewEffect.ChannelSameOrbs or BrewEffect.ChannelRandomOrbs => BrewPolarity.Positive,
        _ => BrewPolarity.Negative
    };

    protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
    {
        var recipe = RecipeFor(this);
        if (_reviving)
        {
            int percent = recipe.Ingredients.Where(i => i.Effect == BrewEffect.FairyRevive).Sum(i => Math.Max(0, i.Amount));
            await CreatureCmd.Heal(Owner.Creature, Math.Max(1m, Owner.Creature.MaxHp * percent / 100m));
            return;
        }
        if (!CombatManager.Instance.IsInProgress)
        {
            await UseOutsideCombat(choiceContext, recipe, target);
            return;
        }
        foreach (var ingredient in recipe.Ingredients)
        {
            switch (ingredient.Recipient)
            {
                case BrewRecipient.SelectedEnemy when target is { IsAlive: true }:
                    await Apply(choiceContext, ingredient, target);
                    break;
                case BrewRecipient.SelfAndEnemy:
                    await Apply(choiceContext, ingredient, Owner.Creature);
                    if (target is { IsAlive: true }) await Apply(choiceContext, ingredient, target);
                    break;
                case BrewRecipient.AllEnemies:
                    foreach (var enemy in Owner.Creature.CombatState?.Enemies.Where(c => c.IsAlive).ToArray() ?? [])
                        await Apply(choiceContext, ingredient, enemy);
                    break;
                case BrewRecipient.AllAllies:
                    foreach (var player in Owner.Creature.CombatState?.Players.ToArray() ?? [])
                        if (player.Creature.IsAlive) await Apply(choiceContext, ingredient, player.Creature);
                    break;
                case BrewRecipient.BothSides:
                    foreach (var player in Owner.Creature.CombatState?.Players.ToArray() ?? [])
                        if (player.Creature.IsAlive) await Apply(choiceContext, ingredient, player.Creature);
                    foreach (var enemy in Owner.Creature.CombatState?.Enemies.Where(c => c.IsAlive).ToArray() ?? [])
                        await Apply(choiceContext, ingredient, enemy);
                    break;
                default:
                    await Apply(choiceContext, ingredient, Owner.Creature);
                    break;
            }
        }
    }

    private static LocString BrewSelectionPrompt(BrewEffect effect) =>
        new("potions", $"OTHERWORLD_TREASURES_POTION_SCP294.selection.{effect}");

    private async Task Apply(PlayerChoiceContext context, BrewIngredient ingredient, Creature creature)
    {
        int amount = ingredient.Amount;
        if (ingredient.Effect == BrewEffect.Doom &&
            ingredient.Recipient is BrewRecipient.BothSides or BrewRecipient.SelfAndEnemy && creature.Side == Owner.Creature.Side)
            amount = ingredient.SecondaryAmount ?? Math.Min(6, ingredient.Amount);
        if (await ApplyExtraEffect(context, ingredient, creature)) return;
        switch (ingredient.Effect)
        {
            case BrewEffect.Heal:
                await CreatureCmd.Heal(creature, amount);
                break;
            case BrewEffect.HealPercent:
            case BrewEffect.AmbergrisHeal:
                await CreatureCmd.Heal(creature, (decimal)creature.MaxHp * amount / 100m);
                break;
            case BrewEffect.Damage:
                await CreatureCmd.Damage(context, creature, amount,
                    ValueProp.Unpowered, Owner.Creature, null, null);
                break;
            case BrewEffect.Regen:
                await PowerCmd.Apply<RegenPower>(context, creature, amount, Owner.Creature, null);
                break;
            case BrewEffect.Strength:
                await ApplyTimedStrength(context, creature, amount, ingredient.Timing);
                break;
            case BrewEffect.StrengthDown:
                await ApplyTimedStrength(context, creature, -amount, ingredient.Timing);
                break;
            case BrewEffect.Dexterity:
                await ApplyTimedDexterity(context, creature, amount, ingredient.Timing);
                break;
            case BrewEffect.DexterityDown:
                await ApplyTimedDexterity(context, creature, -amount, ingredient.Timing);
                break;
            case BrewEffect.Absorption:
                await ApplyTimedCharge<TemporaryHealthPower, SCP294ThisTurnAbsorptionPower, SCP294NextTurnAbsorptionPower>(context, creature, amount, ingredient.Timing);
                break;
            case BrewEffect.Buffer:
                await ApplyTimedCharge<BufferPower, SCP294ThisTurnBufferPower, SCP294NextTurnBufferPower>(context, creature, amount, ingredient.Timing);
                break;
            case BrewEffect.Artifact:
                await ApplyTimedCharge<ArtifactPower, SCP294ThisTurnArtifactPower, SCP294NextTurnArtifactPower>(context, creature, amount, ingredient.Timing);
                break;
            case BrewEffect.Intangible:
                await ApplyTimedCharge<IntangiblePower, SCP294ThisTurnIntangiblePower, SCP294NextTurnIntangiblePower>(context, creature, amount, ingredient.Timing);
                break;
            case BrewEffect.Block:
                await CreatureCmd.GainBlock(creature, amount, ValueProp.Unpowered, null);
                break;
            case BrewEffect.NextTurnBlock:
                await PowerCmd.Apply<BlockNextTurnPower>(context, creature, amount, Owner.Creature, null);
                break;
            case BrewEffect.TripleBlock:
                await CreatureCmd.GainBlock(creature, creature.Block * 2, ValueProp.Unpowered, null);
                break;
            case BrewEffect.MaxHp:
                await CreatureCmd.GainMaxHp(creature, amount);
                break;
            case BrewEffect.Thorns:
                await PowerCmd.Apply<ThornsPower>(context, creature, amount, Owner.Creature, null);
                break;
            case BrewEffect.Plating:
                await PowerCmd.Apply<PlatingPower>(context, creature, amount, Owner.Creature, null);
                break;
            case BrewEffect.Ritual:
                await PowerCmd.Apply<RitualPower>(context, creature, amount, Owner.Creature, null);
                break;
            case BrewEffect.Focus:
                await PowerCmd.Apply<FocusPower>(context, creature, amount, Owner.Creature, null);
                break;
            case BrewEffect.Doom:
                await PowerCmd.Apply<DoomPower>(context, creature, amount, Owner.Creature, null);
                break;
            case BrewEffect.Shrink:
                await PowerCmd.Apply<ShrinkPower>(context, creature, amount, Owner.Creature, null);
                break;
            case BrewEffect.Demise:
                await PowerCmd.Apply<DemisePower>(context, creature, amount, Owner.Creature, null);
                break;
            case BrewEffect.DuplicateNextCard:
                await PowerCmd.Apply<DuplicationPower>(context, creature, amount, Owner.Creature, null);
                break;
            case BrewEffect.TripleNextAttack:
                await PowerCmd.Apply<GigantificationPower>(context, creature, amount, Owner.Creature, null);
                break;
            case BrewEffect.ExtraTurn:
                await PowerCmd.Apply<AmbergrisPower>(context, creature, amount, Owner.Creature, null);
                break;
            case BrewEffect.Weak:
                await PowerCmd.Apply<WeakPower>(context, creature, amount, Owner.Creature, null);
                break;
            case BrewEffect.Vulnerable:
                await PowerCmd.Apply<VulnerablePower>(context, creature, amount, Owner.Creature, null);
                break;
            case BrewEffect.Frail:
                await PowerCmd.Apply<FrailPower>(context, creature, amount, Owner.Creature, null);
                break;
            case BrewEffect.Poison:
                await PowerCmd.Apply<PoisonPower>(context, creature, amount, Owner.Creature, null);
                break;
            case BrewEffect.DrawCards when creature.Player is { } player:
                await CardPileCmd.Draw(context, amount, player);
                break;
            case BrewEffect.NextTurnDraw:
                await PowerCmd.Apply<DrawCardsNextTurnPower>(context, creature, amount, Owner.Creature, null);
                break;
            case BrewEffect.Clarity:
                await PowerCmd.Apply<ClarityPower>(context, creature, amount, Owner.Creature, null);
                break;
            case BrewEffect.GainEnergy when creature.Player is { } player:
                await PlayerCmd.GainEnergy(amount, player);
                break;
            case BrewEffect.GainStars when creature.Player is { } starPlayer:
                await PlayerCmd.GainStars(amount, starPlayer);
                break;
            case BrewEffect.AddOrbSlots when creature.Player is { } orbPlayer:
                await OrbCmd.AddSlots(orbPlayer, amount);
                break;
            case BrewEffect.ChannelDarkOrbs when creature.Player is { } darkPlayer:
                if (darkPlayer.PlayerCombatState is not { } darkState) break;
                int orbCount = Math.Max(1, darkState.OrbQueue.Capacity);
                for (int index = 0; index < orbCount; index++) await OrbCmd.Channel<DarkOrb>(context, darkPlayer);
                break;
            case BrewEffect.NextTurnEnergy:
                await PowerCmd.Apply<EnergyNextTurnPower>(context, creature, amount, Owner.Creature, null);
                break;
            case BrewEffect.Radiance:
                await PowerCmd.Apply<RadiancePower>(context, creature, amount, Owner.Creature, null);
                break;
            case BrewEffect.ShuffleHandIntoDraw when creature.Player is { } shufflePlayer:
                await CardPileCmd.Add(PileType.Hand.GetPile(shufflePlayer).Cards.ToArray(), PileType.Draw);
                await CardPileCmd.Shuffle(context, shufflePlayer);
                if (amount > 0) await CardPileCmd.Draw(context, amount, shufflePlayer);
                break;
            case BrewEffect.ShuffleAndDrawSame when creature.Player is { } redrawPlayer:
                int handCount = PileType.Hand.GetPile(redrawPlayer).Cards.Count;
                await CardCmd.Discard(context, PileType.Hand.GetPile(redrawPlayer).Cards.ToArray());
                if (handCount > 0) await CardPileCmd.Draw(context, handCount, redrawPlayer);
                break;
            case BrewEffect.ReplayStrikes when creature.Player is { } replayPlayer:
                foreach (var card in replayPlayer.PlayerCombatState?.AllCards ?? [])
                    if (card.Tags.Contains(CardTag.Strike)) card.BaseReplayCount += amount;
                break;
            case BrewEffect.AutoPlayTop when creature.Player is { } autoPlayer:
                await CardPileCmd.AutoPlayFromDrawPile(context, autoPlayer, amount, CardPilePosition.Top, forceExhaust: false);
                break;
            case BrewEffect.DrawPileChoice when creature.Player is { } drawPilePlayer:
                var selectedDraw = (await CardSelectCmd.FromCombatPile(context, PileType.Draw.GetPile(drawPilePlayer), drawPilePlayer,
                    new CardSelectorPrefs(BrewSelectionPrompt(BrewEffect.DrawPileChoice), 1))).FirstOrDefault();
                if (selectedDraw != null) await CardPileCmd.Add(selectedDraw, PileType.Hand);
                break;
            case BrewEffect.FreeHandCard when creature.Player is { } freeCardPlayer:
                (await CardSelectCmd.FromHand(context, freeCardPlayer,
                    new CardSelectorPrefs(BrewSelectionPrompt(BrewEffect.FreeHandCard), 1),
                    card => card.CostsEnergyOrStars(includeGlobalModifiers: false) ||
                        card.CostsEnergyOrStars(includeGlobalModifiers: true), this))
                    .FirstOrDefault()?.SetToFreeThisCombat();
                break;
            case BrewEffect.RetainHand:
                await PowerCmd.Apply<RetainHandPower>(context, creature, amount, Owner.Creature, null);
                break;
            case BrewEffect.RandomizeHandCosts when creature.Player is { } randomCostPlayer:
                foreach (var card in PileType.Hand.GetPile(randomCostPlayer).Cards.ToArray())
                    if (!card.EnergyCost.CostsX && card.EnergyCost.GetWithModifiers(CostModifiers.None) >= 0)
                        card.EnergyCost.SetThisTurnOrUntilPlayed(Owner.RunState.Rng.CombatEnergyCosts.NextInt(4));
                break;
            case BrewEffect.ThreeFreeCards when creature.Player is { } threePlayer:
                var generated = new List<CardModel>();
                foreach (var type in new[] { CardType.Attack, CardType.Skill, CardType.Power })
                    generated.AddRange(CardFactory.GetDistinctForCombat(threePlayer,
                        threePlayer.Character.CardPool.GetUnlockedCards(threePlayer.UnlockState,
                            threePlayer.RunState.CardMultiplayerConstraint).Where(card => card.Type == type),
                        1, threePlayer.RunState.Rng.CombatCardGeneration));
                foreach (var card in generated) card.SetToFreeThisTurn();
                await CardPileCmd.AddGeneratedCardsToCombat(generated, PileType.Hand, Owner);
                break;
            case BrewEffect.ShivPlus when creature.Player is { } shivPlayer:
                if (creature.CombatState is not { } shivCombat) break;
                foreach (var card in await Shiv.CreateInHand(shivPlayer, amount, shivCombat, Owner))
                    CardCmd.Upgrade(card);
                break;
            case BrewEffect.SoulCards when creature.Player is { } soulPlayer:
                if (creature.CombatState is not { } soulCombat) break;
                await Soul.CreateInHand(soulPlayer, amount, soulCombat, Owner);
                break;
            case BrewEffect.UpgradedColorless when creature.Player is { } colorlessOwner:
                foreach (var card in CardFactory.GetDistinctForCombat(colorlessOwner,
                    ModelDb.CardPool<ColorlessCardPool>().GetUnlockedCards(colorlessOwner.UnlockState,
                        colorlessOwner.RunState.CardMultiplayerConstraint), amount,
                    colorlessOwner.RunState.Rng.CombatCardGeneration))
                {
                    CardCmd.Upgrade(card);
                    await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, Owner);
                }
                break;
            case BrewEffect.AttackChoice when creature.Player is { } attackPlayer:
                await AddChosenGeneratedCard(context, attackPlayer, CardType.Attack);
                break;
            case BrewEffect.SkillChoice when creature.Player is { } skillPlayer:
                await AddChosenGeneratedCard(context, skillPlayer, CardType.Skill);
                break;
            case BrewEffect.PowerChoice when creature.Player is { } powerPlayer:
                await AddChosenGeneratedCard(context, powerPlayer, CardType.Power);
                break;
            case BrewEffect.ColorlessChoice when creature.Player is { } colorlessPlayer:
                await AddChosenColorlessCard(context, colorlessPlayer);
                break;
            case BrewEffect.UpgradeHand when creature.Player is { } upgradePlayer:
                foreach (var card in PileType.Hand.GetPile(upgradePlayer).Cards.ToArray())
                    if (card.IsUpgradable) CardCmd.Upgrade(card);
                break;
            case BrewEffect.ExhaustHand when creature.Player is { } exhaustPlayer:
                foreach (var card in await CardSelectCmd.FromHand(context, exhaustPlayer,
                    new CardSelectorPrefs(BrewSelectionPrompt(BrewEffect.ExhaustHand), 0, 999999999), null, this))
                    await CardCmd.Exhaust(context, card);
                break;
            case BrewEffect.RetrieveDiscard when creature.Player is { } discardPlayer:
                var recovered = (await CardSelectCmd.FromCombatPile(context, PileType.Discard.GetPile(discardPlayer), discardPlayer,
                    new CardSelectorPrefs(BrewSelectionPrompt(BrewEffect.RetrieveDiscard), 1))).FirstOrDefault();
                if (recovered != null)
                {
                    recovered.SetToFreeThisTurn();
                    await CardPileCmd.Add(recovered, PileType.Hand);
                }
                break;
            case BrewEffect.GambleDiscardDraw when creature.Player is { } gamblePlayer:
                var selected = (await CardSelectCmd.FromHandForDiscard(context, gamblePlayer,
                    new CardSelectorPrefs(BrewSelectionPrompt(BrewEffect.GambleDiscardDraw), 0, 999999999), null, this)).ToList();
                await CardCmd.DiscardAndDraw(context, selected, selected.Count);
                break;
        }
    }

    private async Task ApplyTimedStrength(PlayerChoiceContext context, Creature creature, int signedAmount, BrewTiming timing)
    {
        switch (timing)
        {
            case BrewTiming.NextTurn:
                await PowerCmd.Apply<SCP294NextTurnStrengthPower>(context, creature, signedAmount, Owner.Creature, null);
                break;
            case BrewTiming.ThisTurn when signedAmount > 0:
                await PowerCmd.Apply<SCP294ThisTurnStrengthPower>(context, creature, signedAmount, Owner.Creature, null);
                break;
            case BrewTiming.ThisTurn:
                await PowerCmd.Apply<SCP294ThisTurnStrengthDownPower>(context, creature, -signedAmount, Owner.Creature, null);
                break;
            default:
                await PowerCmd.Apply<StrengthPower>(context, creature, signedAmount, Owner.Creature, null);
                break;
        }
    }

    private async Task ApplyTimedDexterity(PlayerChoiceContext context, Creature creature, int signedAmount, BrewTiming timing)
    {
        switch (timing)
        {
            case BrewTiming.NextTurn:
                await PowerCmd.Apply<SCP294NextTurnDexterityPower>(context, creature, signedAmount, Owner.Creature, null);
                break;
            case BrewTiming.ThisTurn when signedAmount > 0:
                await PowerCmd.Apply<SCP294ThisTurnDexterityPower>(context, creature, signedAmount, Owner.Creature, null);
                break;
            case BrewTiming.ThisTurn:
                await PowerCmd.Apply<SCP294ThisTurnDexterityDownPower>(context, creature, -signedAmount, Owner.Creature, null);
                break;
            default:
                await PowerCmd.Apply<DexterityPower>(context, creature, signedAmount, Owner.Creature, null);
                break;
        }
    }

    private async Task ApplyTimedCharge<TNative, TThisTurn, TNextTurn>(PlayerChoiceContext context,
        Creature creature, int amount, BrewTiming timing)
        where TNative : PowerModel
        where TThisTurn : PowerModel
        where TNextTurn : PowerModel
    {
        if (timing == BrewTiming.NextTurn)
        {
            await PowerCmd.Apply<TNextTurn>(context, creature, amount, Owner.Creature, null);
            return;
        }

        int before = creature.GetPower<TNative>()?.Amount ?? 0;
        await PowerCmd.Apply<TNative>(context, creature, amount, Owner.Creature, null);
        int granted = Math.Max(0, (creature.GetPower<TNative>()?.Amount ?? 0) - before);
        if (timing == BrewTiming.ThisTurn && granted > 0)
            await PowerCmd.Apply<TThisTurn>(context, creature, granted, Owner.Creature, null);
    }

    private async Task AddChosenGeneratedCard(PlayerChoiceContext context, Player player, CardType type)
    {
        List<CardModel> cards = CardFactory.GetDistinctForCombat(player,
            player.Character.CardPool.GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint)
                .Where(card => card.Type == type), 3, player.RunState.Rng.CombatCardGeneration).ToList();
        CardModel? chosen = await CardSelectCmd.FromChooseACardScreen(context, cards, player, canSkip: true);
        if (chosen == null) return;
        chosen.SetToFreeThisTurn();
        await CardPileCmd.AddGeneratedCardToCombat(chosen, PileType.Hand, Owner);
    }

    private async Task AddChosenColorlessCard(PlayerChoiceContext context, Player player)
    {
        var cards = CardFactory.GetDistinctForCombat(player,
            ModelDb.CardPool<ColorlessCardPool>().GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint),
            3, player.RunState.Rng.CombatCardGeneration).ToList();
        var chosen = await CardSelectCmd.FromChooseACardScreen(context, cards, player, canSkip: true);
        if (chosen == null) return;
        chosen.SetToFreeThisTurn();
        await CardPileCmd.AddGeneratedCardToCombat(chosen, PileType.Hand, Owner);
    }

    internal static BrewRecipe RecipeForId(ModelId? id)
    {
        if (id == null || !id.Entry.StartsWith(SavePrefix, StringComparison.Ordinal))
            throw new ArgumentException("Not a serialized SCP-294 brew ID", nameof(id));
        string payload = id.Entry[SavePrefix.Length..].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight((payload.Length + 3) / 4 * 4, '=');
        string json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
        return JsonSerializer.Deserialize<BrewRecipe>(json) ?? throw new JsonException("Empty SCP-294 recipe");
    }

    internal static ModelId EncodeId(PotionModel potion, BrewRecipe recipe)
    {
        string json = JsonSerializer.Serialize(recipe);
        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return new ModelId(potion.Id.Category, SavePrefix + encoded);
    }

    internal static bool TryRecipe(PotionModel potion, out BrewRecipe recipe) => Recipes.TryGetValue(potion, out recipe!);
    internal static void AttachRecipe(PotionModel potion, BrewRecipe recipe)
    {
        Recipes.Remove(potion);
        Recipes.Add(potion, recipe);
    }

    internal static PotionModel? NativeIcon(PotionModel potion)
    {
        string entry = RecipeFor(potion).IconEntry;
        return ModelDb.AllPotions.FirstOrDefault(model => model.Id.Entry.Equals(entry, StringComparison.OrdinalIgnoreCase));
    }

    internal static Texture2D? MixedIcon(PotionModel potion, bool large)
    {
        BrewRecipe recipe = RecipeFor(potion);
        PotionModel? bottle = NativeIcon(potion);
        string[] liquidEntries = VisualLiquidEntries(recipe);
        PotionModel[] liquids = liquidEntries.Select(entry => ModelDb.AllPotions.FirstOrDefault(model =>
                model.Id.Entry.Equals(entry, StringComparison.OrdinalIgnoreCase)))
            .Where(model => model is not null).Cast<PotionModel>().ToArray();
        if (bottle is null || liquids.Length == 0) return null;

        int visualSeed = recipe.VisualSeed ?? BitConverter.ToInt32(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(recipe))), 0);
        string key = $"{recipe.IconEntry}|{string.Join('+', liquids.Select(liquid => liquid.Id.Entry))}|{recipe.VisualStyle}|{visualSeed}|{(large ? "large" : "small")}";
        return MixedIconCache.GetOrAdd(key, _ =>
        {
            // LargeImagePath is a res:// resource inside the game's PCK, so Image.LoadFromFile
            // cannot read it. Load via the model's ResourceLoader-backed LargeImage instead.
            Godot.Image? vesselImage = ReadPotionLargeImage(bottle);
            if (vesselImage is null) return bottle.LargeImage;
            Godot.Image[] liquidImages = liquids.Select(ReadPotionLargeImage)
                .OfType<Godot.Image>().ToArray();
            Texture2D mixed = RecolorLiquid(vesselImage, liquidImages, bottle.LargeImage, recipe.VisualStyle, visualSeed);
            if (mixed is ImageTexture)
            {
                // Path-based consumers must resolve the same generated texture as Image.
                // TakeOverPath registers this in Godot's resource cache without writing files.
                string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
                mixed.TakeOverPath($"res://OtherworldTreasures/generated/scp294/{digest}.tres");
            }
            return mixed;
        });
    }

    private static string[] VisualLiquidEntries(BrewRecipe recipe)
    {
        string[] entries = (recipe.LiquidEntries ?? [])
            .Where(entry => !string.IsNullOrWhiteSpace(entry))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (entries.Length >= 2) return entries;

        // Old saved brews only stored a bottle or one liquid. Derive a stable palette
        // from their saved recipe so they gain a blend without rerolling after loading.
        string[] available = NativeLiquidEntries.Where(entry =>
            !entry.Equals(recipe.IconEntry, StringComparison.OrdinalIgnoreCase) &&
            ModelDb.AllPotions.Any(model => model.Id.Entry.Equals(entry, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        if (available.Length == 0) return entries.Length > 0 ? entries : [recipe.LiquidEntry ?? recipe.IconEntry];
        byte[] seed = SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(recipe)));
        return available.OrderBy(entry => Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(Convert.ToHexString(seed) + entry))))
            .Take(Math.Clamp(recipe.Ingredients.Count, 2, 6)).ToArray();
    }

    private static Godot.Image? ReadPotionLargeImage(PotionModel potion)
    {
        Texture2D? texture = potion.LargeImage;
        if (texture is null) return null;
        Godot.Image? image = texture.GetImage();
        // Some renderers return shared image data; recolor a copy, never the native source.
        return image is not null && !image.IsEmpty() ? (Godot.Image)image.Duplicate() : null;
    }

    private static Texture2D RecolorLiquid(Godot.Image? vesselImage, IReadOnlyList<Godot.Image> liquidImages, Texture2D fallback,
        BrewVisualStyle style, int visualSeed)
    {
        if (vesselImage is null || vesselImage.IsEmpty() || liquidImages.Count == 0 || liquidImages.Any(image => image is null || image.IsEmpty())) return fallback;
        vesselImage.Convert(Godot.Image.Format.Rgba8);
        var liquidColors = new List<(float Hue, float Saturation)>(liquidImages.Count);
        foreach (Godot.Image liquidImage in liquidImages)
        {
            liquidImage.Convert(Godot.Image.Format.Rgba8);
            if (TryFindLiquidRegion(liquidImage, out _, out float hue, out float saturation))
                liquidColors.Add((hue, saturation));
        }
        if (liquidColors.Count == 0) return fallback;

        int width = vesselImage.GetWidth();
        int height = vesselImage.GetHeight();
        TryFindLiquidRegion(vesselImage, out _, out float bodyHue, out _);
        var tintMask = new bool[width * height];
        int top = height, bottom = -1;
        float variation = (visualSeed & 0xffff) / 65536f;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            Color pixel = vesselImage.GetPixel(x, y);
            RgbToHsv(pixel, out _, out float saturation, out float value);
            if (pixel.A < 0.5f || saturation < 0.08f || value < 0.16f) continue;
            // Cover every painted region, including bright glass, disconnected folds,
            // and small colored details. Transparent pixels, neutral highlights and ink stay intact.
            tintMask[y * width + x] = true;
            top = Math.Min(top, y);
            bottom = Math.Max(bottom, y);
        }
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            if (!tintMask[y * width + x]) continue;
            float nx = (x + 0.5f) / width, ny = (y + 0.5f) / height;
            Color pixel = vesselImage.GetPixel(x, y);
            RgbToHsv(pixel, out float pixelHue, out float saturation, out float value);
            if (style == BrewVisualStyle.SoftTint)
            {
                Color target = SoftTintPalette[(int)((uint)visualSeed % (uint)SoftTintPalette.Length)];
                float delta = ((pixelHue - bodyHue + 1.5f) % 1f) - 0.5f;
                float softHue = (target.H + Math.Clamp(delta, -0.12f, 0.12f) * 0.38f + 1f) % 1f;
                float softSaturation = Math.Clamp(saturation * 0.65f + target.S * 0.35f, 0.22f, 0.85f);
                float softValue = Math.Clamp(value * 0.86f + target.V * 0.14f, 0f, 1f);
                vesselImage.SetPixel(x, y, Color.FromHsv(softHue, softSaturation, softValue, pixel.A));
                continue;
            }
            // Divide the vessel fill into softly blended, wavy bands. Every band borrows
            // a different vanilla potion's liquid hue while preserving vessel shading,
            // highlights, transparency, and pixel texture.
            float vertical = Math.Clamp((y - top + 0.5f) / Math.Max(1, bottom - top + 1), 0f, 0.9999f);
            float offset = variation * MathF.Tau;
            float wave = 0.13f * MathF.Sin(nx * MathF.Tau * 2f + ny * 11f + offset);
            float phase = style switch
            {
                BrewVisualStyle.Gradient => Math.Clamp(vertical * 0.78f +
                    (variation < 0.5f ? nx : 1f - nx) * 0.22f, 0f, 0.9999f) * (liquidColors.Count - 1),
                BrewVisualStyle.Marble => Math.Clamp(vertical +
                    0.19f * MathF.Sin(nx * 9f + vertical * 6f + offset) +
                    0.10f * MathF.Sin(vertical * 17f - nx * 5f + offset), 0f, 0.9999f) * liquidColors.Count,
                _ => vertical * liquidColors.Count + wave
            };
            int band = Math.Clamp((int)MathF.Floor(phase), 0, liquidColors.Count - 1);
            float blend = phase - MathF.Floor(phase);
            int nextBand = (band + 1) % liquidColors.Count;
            // Narrow transition zones keep the separate ingredients visible without
            // turning the mixture into a flat set of hard stripes.
            float transition = style == BrewVisualStyle.Gradient ? blend
                : style == BrewVisualStyle.Marble ? Math.Clamp((blend - 0.40f) / 0.60f, 0f, 1f)
                : Math.Clamp((blend - 0.78f) / 0.22f, 0f, 1f);
            transition = transition * transition * (3f - 2f * transition);
            (float Hue, float Saturation) colorA = liquidColors[band];
            (float Hue, float Saturation) colorB = liquidColors[nextBand];
            float hueDelta = ((colorB.Hue - colorA.Hue + 1.5f) % 1f) - 0.5f;
            float hue = (colorA.Hue + hueDelta * transition + 1f) % 1f;
            float sourceSaturation = colorA.Saturation + (colorB.Saturation - colorA.Saturation) * transition;
            // Preserve the original value and alpha so the wider tint retains shading.
            float mixedSaturation = Math.Clamp(saturation * 0.55f + sourceSaturation * 0.45f, 0.18f, 0.96f);
            vesselImage.SetPixel(x, y, Color.FromHsv(hue, mixedSaturation, value, pixel.A));
        }
        return ImageTexture.CreateFromImage(vesselImage);
    }

    private static bool TryFindLiquidRegion(Godot.Image image, out int[] pixels, out float hue, out float saturation)
    {
        int width = image.GetWidth(), height = image.GetHeight();
        int count = width * height;
        var eligible = new bool[count];
        var visited = new bool[count];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float nx = (x + 0.5f) / width, ny = (y + 0.5f) / height;
            if (nx < 0.14f || nx > 0.86f || ny < 0.20f || ny > 0.94f) continue;
            Color c = image.GetPixel(x, y);
            RgbToHsv(c, out _, out float s, out float v);
            eligible[y * width + x] = c.A >= 0.5f && s >= 0.28f && v >= 0.18f;
        }

        var queue = new Queue<int>();
        List<int>? best = null;
        float bestScore = 0;
        for (int i = 0; i < count; i++)
        {
            if (!eligible[i] || visited[i]) continue;
            var component = new List<int>();
            visited[i] = true;
            queue.Enqueue(i);
            long sumX = 0, sumY = 0;
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                int x = current % width, y = current / width;
                component.Add(current); sumX += x; sumY += y;
                for (int oy = -1; oy <= 1; oy++)
                for (int ox = -1; ox <= 1; ox++)
                {
                    if (ox == 0 && oy == 0) continue;
                    int xx = x + ox, yy = y + oy;
                    if (xx < 0 || xx >= width || yy < 0 || yy >= height) continue;
                    int next = yy * width + xx;
                    if (eligible[next] && !visited[next]) { visited[next] = true; queue.Enqueue(next); }
                }
            }

            if (component.Count < 3) continue;
            float cx = (float)sumX / component.Count / width;
            float cy = (float)sumY / component.Count / height;
            float centrality = 1f - Math.Abs(cx - 0.5f) * 0.7f;
            float score = component.Count * centrality * (0.7f + cy * 0.3f);
            if (score > bestScore) { bestScore = score; best = component; }
        }

        if (best is null)
        {
            pixels = Array.Empty<int>(); hue = saturation = 0;
            return false;
        }

        float hx = 0, hy = 0, satSum = 0, weightSum = 0;
        foreach (int index in best)
        {
            Color c = image.GetPixel(index % width, index / width);
            RgbToHsv(c, out float h, out float s, out _);
            float weight = Math.Max(0.05f, s);
            hx += MathF.Cos(h * MathF.Tau) * weight;
            hy += MathF.Sin(h * MathF.Tau) * weight;
            satSum += s * weight; weightSum += weight;
        }
        hue = (MathF.Atan2(hy, hx) / MathF.Tau + 1f) % 1f;
        saturation = satSum / Math.Max(0.001f, weightSum);
        pixels = best.ToArray();
        return true;
    }

    private static void RgbToHsv(Color c, out float h, out float s, out float v)
    {
        float max = Math.Max(c.R, Math.Max(c.G, c.B));
        float min = Math.Min(c.R, Math.Min(c.G, c.B));
        float delta = max - min;
        v = max;
        s = max <= 0 ? 0 : delta / max;
        if (delta <= 0) { h = 0; return; }
        if (max == c.R) h = ((c.G - c.B) / delta) % 6f;
        else if (max == c.G) h = (c.B - c.R) / delta + 2f;
        else h = (c.R - c.G) / delta + 4f;
        h = (h / 6f + 1f) % 1f;
    }

    internal static LocString MakeTitle(PotionModel potion)
    {
        var title = new LocString("potions", "OTHERWORLD_TREASURES_POTION_SCP294.title");
        title.Add("Name", RecipeFor(potion).Name);
        return title;
    }

    internal static LocString MakeDescription(PotionModel potion)
    {
        var description = new LocString("potions", "OTHERWORLD_TREASURES_POTION_SCP294.description");
        var recipe = RecipeFor(potion);
        string effects=string.Join("\n",recipe.Ingredients.Select(Describe));
        if(recipe.NativePotionEntry!=null)
        {
            var original=new LocString("potions",recipe.NativePotionEntry+".description");potion.DynamicVars.AddTo(original);
            string prefix=MegaCrit.Sts2.Core.Helpers.EnergyIconHelper.GetPrefix(potion);original.Add("energyPrefix",prefix);
            foreach(var variable in original.Variables.Values)if(variable is MegaCrit.Sts2.Core.Localization.DynamicVars.EnergyVar energy)energy.ColorPrefix=prefix;
            effects=original.GetFormattedText()+"\n"+effects;
        }
        description.Add("Effects", effects);
        return description;
    }

    private static string Describe(BrewIngredient ingredient)
    {
        string language = LocManager.Instance.Language;
        string number = VariableNumber(ingredient.Amount);
        string? extra = DescribeExtra(ingredient, language);
        if (extra != null) return FormatPotionText(extra, language);
        string prefix = language switch
        {
            "eng" => ingredient.Recipient switch { BrewRecipient.SelectedEnemy => "Target: ", BrewRecipient.AllEnemies => "All enemies: ", BrewRecipient.AllAllies => "All allies: ", BrewRecipient.BothSides => "Both sides: ", BrewRecipient.SelfAndEnemy => "You and target: ", _ => "You: " },
            "jpn" => ingredient.Recipient switch { BrewRecipient.SelectedEnemy => "敵：", BrewRecipient.AllEnemies => "敵全体：", BrewRecipient.AllAllies => "味方全体：", BrewRecipient.BothSides => "敵味方全体：", BrewRecipient.SelfAndEnemy => "自分と敵1体：", _ => "自分：" },
            _ => ingredient.Recipient switch { BrewRecipient.SelectedEnemy => "敌方目标：", BrewRecipient.AllEnemies => "敌方全体：", BrewRecipient.AllAllies => "己方全体：", BrewRecipient.BothSides => "敌我双方：", BrewRecipient.SelfAndEnemy => "自身与敌方目标：", _ => "自身：" }
        };
        string verb = ingredient.Timing != BrewTiming.Immediate
            ? TimedDescription(ingredient.Effect, number, ingredient.Timing, language)
            : language switch
        {
            "eng" => EnglishDescription(ingredient.Effect, number),
            "jpn" => JapaneseDescription(ingredient.Effect, number),
            _ => ChineseDescription(ingredient.Effect, number)
        };
        return FormatPotionText(prefix + verb, language);
    }

    private static string TimedDescription(BrewEffect effect, string n, BrewTiming timing, string language)
    {
        bool loses = effect is BrewEffect.StrengthDown or BrewEffect.DexterityDown;
        bool defensive = effect is BrewEffect.Absorption or BrewEffect.Buffer or BrewEffect.Artifact or BrewEffect.Intangible;
        string name = language switch
        {
            "eng" => effect switch { BrewEffect.Strength or BrewEffect.StrengthDown => "Strength", BrewEffect.Dexterity or BrewEffect.DexterityDown => "Dexterity", BrewEffect.Absorption => "Absorption", BrewEffect.Buffer => "Buffer", BrewEffect.Artifact => "Artifact", _ => "Intangible" },
            "jpn" => effect switch { BrewEffect.Strength or BrewEffect.StrengthDown => "筋力", BrewEffect.Dexterity or BrewEffect.DexterityDown => "敏捷", BrewEffect.Absorption => "ダメージ吸収", BrewEffect.Buffer => "バッファー", BrewEffect.Artifact => "アーティファクト", _ => "霊体" },
            _ => effect switch { BrewEffect.Strength or BrewEffect.StrengthDown => "力量", BrewEffect.Dexterity or BrewEffect.DexterityDown => "敏捷", BrewEffect.Absorption => "伤害吸收", BrewEffect.Buffer => "缓冲", BrewEffect.Artifact => "人工制品", _ => "无实体" }
        };
        return language switch
        {
            "eng" => timing == BrewTiming.NextTurn
                ? $"At the start of next turn, {(loses ? "lose" : "gain")} {n} {name}."
                : defensive ? $"Gain {n} {name} for this round. Unused charges expire after the opposing side's turn."
                : $"{(loses ? "Lose" : "Gain")} {n} {name} this turn; the change ends at turn end.",
            "jpn" => timing == BrewTiming.NextTurn
                ? $"次のターン開始時に{n}{name}を{(loses ? "失う" : "得る")}。"
                : defensive ? $"このラウンド中、{n}{name}を得る。相手側のターン終了時に残りは消える。"
                : $"このターン、{n}{name}を{(loses ? "失う" : "得る")}。ターン終了時に元に戻る。",
            _ => timing == BrewTiming.NextTurn
                ? $"下回合开始时{(loses ? "失去" : "获得")}{n}{(defensive && effect != BrewEffect.Absorption ? "层" : "点")}{name}。"
                : defensive ? $"本轮获得{n}{(effect == BrewEffect.Absorption ? "点" : "层")}{name}；对方回合结束时剩余效果消失。"
                : $"本回合{(loses ? "失去" : "获得")}{n}点{name}；回合结束时恢复。"
        };
    }

    private static string ChineseDescription(BrewEffect effect, string n) => effect switch
    {
        BrewEffect.Heal => $"恢复{n}点生命。",
        BrewEffect.HealPercent or BrewEffect.AmbergrisHeal => $"恢复最大生命值的{n}%。",
        BrewEffect.Damage => $"造成{n}点伤害。",
        BrewEffect.Regen => $"获得{n}层再生。",
        BrewEffect.Absorption => $"获得{n}点伤害吸收。",
        BrewEffect.Buffer => $"获得{n}层缓冲。",
        BrewEffect.Artifact => $"获得{n}层人工制品。",
        BrewEffect.Intangible => $"获得{n}层无实体。",
        BrewEffect.Thorns => $"获得{n}点荆棘。",
        BrewEffect.Plating => $"获得{n}层覆甲。",
        BrewEffect.Ritual => $"获得{n}层仪式。",
        BrewEffect.Focus => $"获得{n}点集中。",
        BrewEffect.Doom => $"施加{n}层灾厄。",
        BrewEffect.MaxHp => $"获得{n}点最大生命值。",
        BrewEffect.TripleBlock => "将当前格挡变为3倍。",
        BrewEffect.NextTurnBlock => $"下回合开始时获得{n}点格挡。",
        BrewEffect.NextTurnDraw => $"下回合开始时额外抽{n}张牌。",
        BrewEffect.NextTurnEnergy => $"下回合开始时获得{n}点能量。",
        BrewEffect.Clarity => $"在接下来的{n}个回合开始时，额外抽1张牌。",
        BrewEffect.Radiance => $"在接下来的{n}个回合开始时，额外获得1点能量。",
        BrewEffect.ShuffleHandIntoDraw => $"将手牌中的所有牌洗入抽牌堆，然后抽{n}张牌。",
        BrewEffect.ShuffleAndDrawSame => "弃置所有手牌，然后抽取等量的牌。",
        BrewEffect.ReplayStrikes => $"本场战斗中，打击牌额外重放{n}次。",
        BrewEffect.AutoPlayTop => $"打出抽牌堆顶部的{n}张牌。",
        BrewEffect.DrawPileChoice => "选择抽牌堆中的1张牌加入手牌。",
        BrewEffect.FreeHandCard => "选择手牌中的1张牌，使其在本场战斗中可以免费打出。",
        BrewEffect.RetainHand => $"保留手牌{n}回合。",
        BrewEffect.ThreeFreeCards => "将1张随机攻击牌、1张随机技能牌和1张随机能力牌加入手牌。这些牌在本回合可以免费打出。",
        BrewEffect.Shrink => $"攻击造成的伤害降低30%，持续{n}回合。",
        BrewEffect.Demise => $"自身每回合结束时失去{n}点生命。",
        BrewEffect.DuplicateNextCard => $"本回合接下来打出的{n}张牌各额外打出1次。",
        BrewEffect.TripleNextAttack => $"接下来打出的{n}张攻击牌造成3倍伤害。",
        BrewEffect.RandomizeHandCosts => "本回合随机化手牌中所有可消耗能量牌的耗能。",
        BrewEffect.ExtraTurn => $"本场战斗中，额外进行{n}个回合。",
        BrewEffect.GainStars => $"获得{n}颗星星。",
        BrewEffect.AddOrbSlots => $"获得{n}个充能球栏位。",
        BrewEffect.ChannelDarkOrbs => "生成与你的充能球栏位数量相同的黑暗充能球；没有栏位时生成1个。",
        BrewEffect.ShivPlus => $"将{n}张小刀+加入手牌。",
        BrewEffect.SoulCards => $"将{n}张灵魂加入手牌。",
        BrewEffect.UpgradedColorless => $"将{n}张升级的无色牌加入手牌。",
        BrewEffect.UpgradeHand => "在本场战斗中升级手牌中的所有牌。",
        BrewEffect.ExhaustHand => "消耗手牌中的任意张牌。",
        BrewEffect.Strength => $"获得{n}点力量。",
        BrewEffect.StrengthDown => $"失去{n}点力量。",
        BrewEffect.Dexterity => $"获得{n}点敏捷。",
        BrewEffect.DexterityDown => $"失去{n}点敏捷。",
        BrewEffect.Block => $"获得{n}点格挡。",
        BrewEffect.Weak => $"施加{n}层虚弱。",
        BrewEffect.Vulnerable => $"施加{n}层易伤。",
        BrewEffect.Frail => $"施加{n}层脆弱。",
        BrewEffect.Poison => $"施加{n}层中毒。",
        BrewEffect.DrawCards => $"抽{n}张牌。",
        BrewEffect.GainEnergy => $"获得{n}点能量。",
        BrewEffect.AttackChoice => "从3张随机攻击牌中选择1张加入手牌。这张牌在本回合可以免费打出。",
        BrewEffect.SkillChoice => "从3张随机技能牌中选择1张加入手牌。这张牌在本回合可以免费打出。",
        BrewEffect.PowerChoice => "从3张随机能力牌中选择1张加入手牌。这张牌在本回合可以免费打出。",
        BrewEffect.ColorlessChoice => "从3张随机无色牌中选择1张加入手牌。这张牌在本回合可以免费打出。",
        BrewEffect.RetrieveDiscard => "将弃牌堆中的1张牌放入手牌。这张牌在本回合可以免费打出。",
        BrewEffect.GambleDiscardDraw => "弃置任意张手牌，然后抽取等量的牌。",
        _ => "效果异常。"
    };

    private static string EnglishDescription(BrewEffect effect, string n) => effect switch
    {
        BrewEffect.Heal => $"Heal {n} HP.",
        BrewEffect.HealPercent or BrewEffect.AmbergrisHeal => $"Heal {n}% of Max HP.",
        BrewEffect.Damage => $"Deal {n} damage.",
        BrewEffect.Regen => $"Gain {n} Regen.",
        BrewEffect.Absorption => $"Gain {n} Absorption.",
        BrewEffect.Buffer => $"Gain {n} Buffer.",
        BrewEffect.Artifact => $"Gain {n} Artifact.",
        BrewEffect.Intangible => $"Gain {n} Intangible.",
        BrewEffect.Thorns => $"Gain {n} Thorns.",
        BrewEffect.Plating => $"Gain {n} Plating.",
        BrewEffect.Ritual => $"Gain {n} Ritual.",
        BrewEffect.Focus => $"Gain {n} Focus.",
        BrewEffect.Doom => $"Apply {n} Doom.",
        BrewEffect.MaxHp => $"Gain {n} Max HP.",
        BrewEffect.TripleBlock => "Triple current Block.",
        BrewEffect.NextTurnBlock => $"Gain {n} Block at the start of next turn.",
        BrewEffect.NextTurnDraw => $"Draw {n} extra cards at the start of next turn.",
        BrewEffect.NextTurnEnergy => $"Gain {n} Energy at the start of next turn.",
        BrewEffect.Clarity => $"Draw 1 extra card at the start of each of the next {n} turns.",
        BrewEffect.Radiance => $"Gain 1 extra Energy at the start of each of the next {n} turns.",
        BrewEffect.ShuffleHandIntoDraw => $"Shuffle your hand into your draw pile, then draw {n} cards.",
        BrewEffect.ShuffleAndDrawSame => "Discard your hand, then draw the same number of cards.",
        BrewEffect.ReplayStrikes => $"Strike cards replay {n} additional time(s) this combat.",
        BrewEffect.AutoPlayTop => $"Play the top {n} cards of your draw pile.",
        BrewEffect.DrawPileChoice => "Choose a card from your draw pile and put it into your hand.",
        BrewEffect.FreeHandCard => "Choose a card in your hand. It is free to play for this combat.",
        BrewEffect.RetainHand => $"Retain your hand for {n} turns.",
        BrewEffect.ThreeFreeCards => "Add a random Attack, Skill and Power to your hand. They are free to play this turn.",
        BrewEffect.Shrink => $"Attacks deal 30% less damage for {n} turns.",
        BrewEffect.Demise => $"Lose {n} HP at the end of each of your turns.",
        BrewEffect.DuplicateNextCard => $"Your next {n} cards this turn play an additional time.",
        BrewEffect.TripleNextAttack => $"Your next {n} Attacks deal triple damage.",
        BrewEffect.RandomizeHandCosts => "Randomize the Energy costs of eligible cards in your hand this turn.",
        BrewEffect.ExtraTurn => $"Take {n} extra turns in this combat.",
        BrewEffect.GainStars => $"Gain {n} Stars.",
        BrewEffect.AddOrbSlots => $"Gain {n} Orb slots.",
        BrewEffect.ChannelDarkOrbs => "Channel Dark Orbs equal to your Orb slots, or 1 if you have none.",
        BrewEffect.ShivPlus => $"Add {n} upgraded Shivs to your hand.",
        BrewEffect.SoulCards => $"Add {n} Souls to your hand.",
        BrewEffect.UpgradedColorless => $"Add {n} upgraded Colorless cards to your hand.",
        BrewEffect.UpgradeHand => "Upgrade all cards in your hand for this combat.",
        BrewEffect.ExhaustHand => "Exhaust any number of cards in your hand.",
        BrewEffect.Strength => $"Gain {n} Strength.",
        BrewEffect.StrengthDown => $"Lose {n} Strength.",
        BrewEffect.Dexterity => $"Gain {n} Dexterity.",
        BrewEffect.DexterityDown => $"Lose {n} Dexterity.",
        BrewEffect.Block => $"Gain {n} Block.",
        BrewEffect.Weak => $"Gain {n} Weak.",
        BrewEffect.Vulnerable => $"Gain {n} Vulnerable.",
        BrewEffect.Frail => $"Gain {n} Frail.",
        BrewEffect.Poison => $"Gain {n} Poison.",
        BrewEffect.DrawCards => $"Draw {n} cards.",
        BrewEffect.GainEnergy => $"Gain {n} Energy.",
        BrewEffect.AttackChoice => "Choose 1 of 3 random Attack cards. Add it to your hand; it costs 0 this turn.",
        BrewEffect.SkillChoice => "Choose 1 of 3 random Skill cards. Add it to your hand; it costs 0 this turn.",
        BrewEffect.PowerChoice => "Choose 1 of 3 random Power cards. Add it to your hand; it costs 0 this turn.",
        BrewEffect.ColorlessChoice => "Choose 1 of 3 random Colorless cards. Add it to your hand; it costs 0 this turn.",
        BrewEffect.RetrieveDiscard => "Put a card from your discard pile into your hand. It costs 0 this turn.",
        BrewEffect.GambleDiscardDraw => "Discard any number of cards, then draw that many cards.",
        _ => "Unknown effect."
    };

    private static string JapaneseDescription(BrewEffect effect, string n) => effect switch
    {
        BrewEffect.Heal => $"HPを{n}回復する。",
        BrewEffect.HealPercent or BrewEffect.AmbergrisHeal => $"最大HPの{n}%を回復する。",
        BrewEffect.Damage => $"{n}ダメージを与える。",
        BrewEffect.Regen => $"再生を{n}得る。",
        BrewEffect.Absorption => $"ダメージ吸収を{n}得る。",
        BrewEffect.Buffer => $"バッファーを{n}得る。",
        BrewEffect.Artifact => $"アーティファクトを{n}得る。",
        BrewEffect.Intangible => $"霊体を{n}得る。",
        BrewEffect.Thorns => $"トゲを{n}得る。",
        BrewEffect.Plating => $"プレートを{n}得る。",
        BrewEffect.Ritual => $"儀式を{n}得る。",
        BrewEffect.Focus => $"集中力を{n}得る。",
        BrewEffect.Doom => $"破滅を{n}付与する。",
        BrewEffect.MaxHp => $"最大HPを{n}得る。",
        BrewEffect.TripleBlock => "現在のブロックを3倍にする。",
        BrewEffect.NextTurnBlock => $"次のターン開始時にブロックを{n}得る。",
        BrewEffect.NextTurnDraw => $"次のターン開始時にカードを追加で{n}枚引く。",
        BrewEffect.NextTurnEnergy => $"次のターン開始時にエナジーを{n}得る。",
        BrewEffect.Clarity => $"次の{n}ターンの開始時にカードを追加で1枚引く。",
        BrewEffect.Radiance => $"次の{n}ターンの開始時にエナジーを追加で1得る。",
        BrewEffect.ShuffleHandIntoDraw => $"手札をすべて山札に戻してシャッフルし、カードを{n}枚引く。",
        BrewEffect.ShuffleAndDrawSame => "手札をすべて捨て、同じ枚数のカードを引く。",
        BrewEffect.ReplayStrikes => $"この戦闘中、ストライクを持つカードは追加で{n}回再生される。",
        BrewEffect.AutoPlayTop => $"山札の上から{n}枚のカードをプレイする。",
        BrewEffect.DrawPileChoice => "山札からカードを1枚選び、手札に加える。",
        BrewEffect.FreeHandCard => "手札のカードを1枚選ぶ。この戦闘中、そのカードを無料でプレイできる。",
        BrewEffect.RetainHand => $"手札を{n}ターン保持する。",
        BrewEffect.ThreeFreeCards => "ランダムなアタック、スキル、パワーカードを1枚ずつ手札に加える。このターン中、それらは無料でプレイできる。",
        BrewEffect.Shrink => $"攻撃ダメージが{n}ターンの間30%減少する。",
        BrewEffect.Demise => $"自身の各ターン終了時にHPを{n}失う。",
        BrewEffect.DuplicateNextCard => $"このターン、次の{n}枚のカードを追加で1回プレイする。",
        BrewEffect.TripleNextAttack => $"次の{n}枚のアタックのダメージが3倍になる。",
        BrewEffect.RandomizeHandCosts => "このターン、手札の対象カードのエナジーコストをランダム化する。",
        BrewEffect.ExtraTurn => $"この戦闘中、追加のターンを{n}回得る。",
        BrewEffect.GainStars => $"スターを{n}得る。",
        BrewEffect.AddOrbSlots => $"オーブスロットを{n}得る。",
        BrewEffect.ChannelDarkOrbs => "オーブスロットの数だけダークオーブを生成する。スロットがない場合は1個生成する。",
        BrewEffect.ShivPlus => $"アップグレード済みのナイフを{n}枚手札に加える。",
        BrewEffect.SoulCards => $"ソウルを{n}枚手札に加える。",
        BrewEffect.UpgradedColorless => $"アップグレード済みの無色カードを{n}枚手札に加える。",
        BrewEffect.UpgradeHand => "この戦闘中、手札のカードをすべてアップグレードする。",
        BrewEffect.ExhaustHand => "手札のカードを好きな枚数廃棄する。",
        BrewEffect.Strength => $"筋力を{n}得る。",
        BrewEffect.StrengthDown => $"筋力を{n}失う。",
        BrewEffect.Dexterity => $"敏捷を{n}得る。",
        BrewEffect.DexterityDown => $"敏捷を{n}失う。",
        BrewEffect.Block => $"ブロックを{n}得る。",
        BrewEffect.Weak => $"脱力を{n}得る。",
        BrewEffect.Vulnerable => $"弱体を{n}得る。",
        BrewEffect.Frail => $"脆弱を{n}得る。",
        BrewEffect.Poison => $"毒を{n}得る。",
        BrewEffect.DrawCards => $"カードを{n}枚引く。",
        BrewEffect.GainEnergy => $"エナジーを{n}得る。",
        BrewEffect.AttackChoice => "ランダムなアタックカード3枚から1枚を選び、手札に加える。このターン中、そのカードは0コストで使える。",
        BrewEffect.SkillChoice => "ランダムなスキルカード3枚から1枚を選び、手札に加える。このターン中、そのカードは0コストで使える。",
        BrewEffect.PowerChoice => "ランダムなパワーカード3枚から1枚を選び、手札に加える。このターン中、そのカードは0コストで使える。",
        BrewEffect.ColorlessChoice => "ランダムな無色カード3枚から1枚を選び、手札に加える。このターン中、そのカードは0コストで使える。",
        BrewEffect.RetrieveDiscard => "捨て札からカードを1枚手札に加える。このターン中、そのカードは0コストで使える。",
        BrewEffect.GambleDiscardDraw => "手札を好きな枚数捨て、その枚数分カードを引く。",
        _ => "不明な効果。"
    };

    private static string EffectName(BrewEffect effect) => effect switch
    {
        BrewEffect.Heal => LocText("effect.heal"), BrewEffect.Damage => LocText("effect.damage"),
        BrewEffect.HealPercent or BrewEffect.AmbergrisHeal => LocText("effect.healPercent"),
        BrewEffect.Regen => LocText("effect.regen"),
        BrewEffect.Absorption => LocText("effect.absorption"), BrewEffect.Buffer => LocText("effect.buffer"),
        BrewEffect.Artifact => LocText("effect.artifact"), BrewEffect.Intangible => LocText("effect.intangible"),
        BrewEffect.Thorns => LocText("effect.thorns"), BrewEffect.Plating => LocText("effect.plating"),
        BrewEffect.Ritual => LocText("effect.ritual"), BrewEffect.Focus => LocText("effect.focus"),
        BrewEffect.Doom => LocText("effect.doom"), BrewEffect.MaxHp => LocText("effect.maxHp"),
        BrewEffect.TripleBlock => LocText("effect.tripleBlock"), BrewEffect.UpgradeHand => LocText("effect.upgradeHand"),
        BrewEffect.ExhaustHand => LocText("effect.exhaustHand"), BrewEffect.ColorlessChoice => LocText("effect.colorlessChoice"),
        BrewEffect.NextTurnBlock => LocText("effect.nextTurnBlock"), BrewEffect.NextTurnDraw => LocText("effect.nextTurnDraw"),
        BrewEffect.NextTurnEnergy => LocText("effect.nextTurnEnergy"), BrewEffect.Clarity => LocText("effect.clarity"),
        BrewEffect.Radiance => LocText("effect.radiance"),
        BrewEffect.ShuffleHandIntoDraw => LocText("effect.shuffleHand"),
        BrewEffect.ShuffleAndDrawSame => LocText("effect.shuffleAndDrawSame"),
        BrewEffect.ReplayStrikes => LocText("effect.replayStrikes"),
        BrewEffect.AutoPlayTop => LocText("effect.autoPlay"),
        BrewEffect.DrawPileChoice => LocText("effect.drawPileChoice"),
        BrewEffect.FreeHandCard => LocText("effect.freeHandCard"),
        BrewEffect.RetainHand => LocText("effect.retainHand"),
        BrewEffect.ThreeFreeCards => LocText("effect.threeFreeCards"),
        BrewEffect.Shrink => LocText("effect.shrink"), BrewEffect.Demise => LocText("effect.demise"),
        BrewEffect.DuplicateNextCard => LocText("effect.duplicate"),
        BrewEffect.TripleNextAttack => LocText("effect.gigantification"),
        BrewEffect.RandomizeHandCosts => LocText("effect.randomCosts"),
        BrewEffect.ExtraTurn => LocText("effect.extraTurn"), BrewEffect.GainStars => LocText("effect.stars"),
        BrewEffect.AddOrbSlots => LocText("effect.orbSlots"), BrewEffect.ChannelDarkOrbs => LocText("effect.darkOrbs"),
        BrewEffect.ShivPlus => LocText("effect.shivPlus"), BrewEffect.SoulCards => LocText("effect.souls"),
        BrewEffect.UpgradedColorless => LocText("effect.upgradedColorless"),
        BrewEffect.Strength => LocText("effect.strength"), BrewEffect.StrengthDown => LocText("effect.strengthDown"),
        BrewEffect.Dexterity => LocText("effect.dexterity"), BrewEffect.DexterityDown => LocText("effect.dexterityDown"),
        BrewEffect.Block => LocText("effect.block"), BrewEffect.Weak => LocText("effect.weak"),
        BrewEffect.Vulnerable => LocText("effect.vulnerable"), BrewEffect.Frail => LocText("effect.frail"),
        BrewEffect.Poison => LocText("effect.poison"), BrewEffect.DrawCards => LocText("effect.draw"),
        BrewEffect.GainEnergy => LocText("effect.energy"),
        BrewEffect.AttackChoice => LocText("effect.attackChoice"), BrewEffect.SkillChoice => LocText("effect.skillChoice"),
        BrewEffect.PowerChoice => LocText("effect.powerChoice"), BrewEffect.RetrieveDiscard => LocText("effect.retrieveDiscard"),
        BrewEffect.GambleDiscardDraw => LocText("effect.gambleDiscardDraw"), _ => ExtraEffectName(effect)
    };

    private static string EffectGlyph(BrewEffect effect) => effect switch
    {
        BrewEffect.Heal => "治疗", BrewEffect.HealPercent or BrewEffect.AmbergrisHeal => "回血", BrewEffect.Damage => "伤害",
        BrewEffect.Regen => "再生", BrewEffect.Absorption => "伤害吸收", BrewEffect.Buffer => "缓冲",
        BrewEffect.Artifact => "人工制品", BrewEffect.Intangible => "无实体",
        BrewEffect.Thorns => "荆棘", BrewEffect.Plating => "覆甲", BrewEffect.Ritual => "仪式",
        BrewEffect.Focus => "集中", BrewEffect.Doom => "灾厄", BrewEffect.MaxHp => "生命",
        BrewEffect.TripleBlock => "壁垒", BrewEffect.UpgradeHand => "升级", BrewEffect.ExhaustHand => "消耗",
        BrewEffect.ColorlessChoice => "无色",
        BrewEffect.NextTurnBlock => "预备", BrewEffect.NextTurnDraw => "预知",
        BrewEffect.NextTurnEnergy => "蓄能", BrewEffect.Clarity => "清明", BrewEffect.Radiance => "辉光",
        BrewEffect.ShuffleHandIntoDraw => "洗牌", BrewEffect.ShuffleAndDrawSame => "重抽",
        BrewEffect.ReplayStrikes => "连击", BrewEffect.AutoPlayTop => "混沌",
        BrewEffect.DrawPileChoice => "预知", BrewEffect.FreeHandCard => "疯狂",
        BrewEffect.RetainHand => "保留", BrewEffect.ThreeFreeCards => "三才",
        BrewEffect.Shrink => "缩小", BrewEffect.Demise => "亡灭", BrewEffect.DuplicateNextCard => "复制",
        BrewEffect.TripleNextAttack => "巨大", BrewEffect.RandomizeHandCosts => "混乱",
        BrewEffect.ExtraTurn => "时间", BrewEffect.GainStars => "星辰", BrewEffect.AddOrbSlots => "槽位",
        BrewEffect.ChannelDarkOrbs => "黑暗", BrewEffect.ShivPlus => "小刀", BrewEffect.SoulCards => "灵魂",
        BrewEffect.UpgradedColorless => "彩卡",
        BrewEffect.Strength => "力量", BrewEffect.StrengthDown => "削弱",
        BrewEffect.Dexterity => "敏捷", BrewEffect.DexterityDown => "迟钝", BrewEffect.Block => "格挡",
        BrewEffect.Weak => "虚弱", BrewEffect.Vulnerable => "易伤", BrewEffect.Frail => "脆弱",
        BrewEffect.Poison => "中毒", BrewEffect.DrawCards => "抽牌", BrewEffect.GainEnergy => "能量",
        BrewEffect.AttackChoice => "攻击", BrewEffect.SkillChoice => "技能", BrewEffect.PowerChoice => "能力",
        BrewEffect.RetrieveDiscard => "回收", BrewEffect.GambleDiscardDraw => "替换", _ => ExtraEffectGlyph(effect)
    };

    private static string LocText(string suffix) =>
        new LocString("potions", "OTHERWORLD_TREASURES_POTION_SCP294." + suffix).GetFormattedText();
}

internal enum BrewRecipient { Self, SelectedEnemy, AllEnemies, AllAllies, BothSides, SelfAndEnemy }
internal enum BrewEffect { Heal, Damage, Regen, Strength, StrengthDown, Dexterity, DexterityDown, Block, Weak, Vulnerable, Frail, Poison, DrawCards, GainEnergy, AttackChoice, SkillChoice, PowerChoice, RetrieveDiscard, GambleDiscardDraw, Absorption, Buffer, Artifact, Intangible, Thorns, Plating, Ritual, Focus, Doom, MaxHp, TripleBlock, UpgradeHand, ExhaustHand, ColorlessChoice, NextTurnBlock, NextTurnDraw, NextTurnEnergy, Clarity, Radiance, ShuffleHandIntoDraw, AutoPlayTop, DrawPileChoice, FreeHandCard, RetainHand, ThreeFreeCards, Shrink, Demise, DuplicateNextCard, TripleNextAttack, RandomizeHandCosts, HealPercent, ExtraTurn, GainStars, AddOrbSlots, ChannelDarkOrbs, ShivPlus, SoulCards, UpgradedColorless, ShuffleAndDrawSame, ReplayStrikes, FairyRevive, FillPotionSlots, SneckoDraw, FoulSplash, HealExtraTurn, ExhaustHandDraw, ChannelSameOrbs, ChannelRandomOrbs, AmbergrisHeal }
internal enum BrewPolarity { Positive, Negative, Random }
internal enum BrewTiming { Immediate, ThisTurn, NextTurn }
internal sealed record BrewIngredient(BrewEffect Effect, int Amount, BrewRecipient Recipient, BrewTiming Timing = BrewTiming.Immediate,
    string? OrbEntry = null, int? SecondaryAmount = null);
internal enum BrewVisualStyle { Waves, Gradient, Marble, SoftTint }

internal sealed record BrewRecipe(string Name, IReadOnlyList<BrewIngredient> Ingredients, string IconEntry,
    string? LiquidEntry = null, IReadOnlyList<string>? LiquidEntries = null,
    BrewVisualStyle VisualStyle = BrewVisualStyle.Waves, int? VisualSeed = null, string? NativePotionEntry = null)
{
    public IReadOnlyList<BrewIngredient> Ingredients { get; init; } = LimitDoom(Ingredients);
    private static IReadOnlyList<BrewIngredient> LimitDoom(IReadOnlyList<BrewIngredient> ingredients)
    {
        bool seen = false;
        var result = new List<BrewIngredient>(ingredients.Count);
        foreach (var ingredient in ingredients)
        {
            if (ingredient.Effect != BrewEffect.Doom) { result.Add(ingredient); continue; }
            if (seen) continue;
            seen = true;
            result.Add(ingredient with
            {
                Amount = Math.Clamp(ingredient.Amount, 1, 10),
                SecondaryAmount = ingredient.SecondaryAmount is { } secondary ? Math.Clamp(secondary, 1, 10) : null
            });
        }
        return result;
    }
}
internal sealed record BrewPreset(string NameKey, int ChancePercent, string IconEntry, IReadOnlyList<BrewIngredient> Ingredients);

[HarmonyPatch(typeof(PotionModel), nameof(PotionModel.ToSerializable))]
internal static class SCP294SerializePatch
{
    private static void Postfix(PotionModel __instance, int slotIndex, ref SerializablePotion __result)
    {
        if (SCP294Potion.TryRecipe(__instance, out var recipe))
            __result.Id = SCP294Potion.EncodeId(__instance, recipe);
    }
}

[HarmonyPatch(typeof(PotionModel), nameof(PotionModel.FromSerializable))]
internal static class SCP294DeserializePatch
{
    private static bool Prefix(SerializablePotion save, ref PotionModel __result)
    {
        if (save.Id == null || !save.Id.Entry.StartsWith("BREW_", StringComparison.Ordinal)) return true;
        var recipe = SCP294Potion.RecipeForId(save.Id);
        var canonical = recipe.NativePotionEntry is { } entry
            ? ModelDb.AllPotions.FirstOrDefault(potion=>potion.Id.Entry==entry)
            : ModelDb.AllPotions.FirstOrDefault(potion=>potion is SCP294Potion);
        if(canonical==null)throw new InvalidOperationException("Missing potion model for saved SCP-294 recipe.");
        if(recipe.NativePotionEntry!=null)SCP294Potion.EnsureCatalogUseHook(canonical);
        __result = canonical.ToMutable();
        SCP294Potion.AttachRecipe(__result, recipe);
        return false;
    }
}

[HarmonyPatch(typeof(PotionModel), "Title", MethodType.Getter)]
internal static class SCP294TitlePatch
{
    private static bool Prefix(PotionModel __instance, ref LocString __result)
    {
        if (__instance is not SCP294Potion && !SCP294Potion.IsCatalogBrew(__instance)) return true;
        __result = SCP294Potion.MakeTitle(__instance);
        return false;
    }
}

[HarmonyPatch(typeof(PotionModel), "Description", MethodType.Getter)]
internal static class SCP294DescriptionPatch
{
    private static bool Prefix(PotionModel __instance, ref LocString __result)
    {
        if (__instance is not SCP294Potion && !SCP294Potion.IsCatalogBrew(__instance)) return true;
        __result = SCP294Potion.MakeDescription(__instance);
        return false;
    }
}

[HarmonyPatch(typeof(PotionModel), "LargeImage", MethodType.Getter)]
internal static class SCP294LargeImagePatch
{
    private static bool Prefix(PotionModel __instance, ref Texture2D __result)
    {
        if (__instance is SCP294Potion && SCP294Potion.MixedIcon(__instance, large: true) is { } mixed)
        {
            __result = mixed;
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(PotionModel), "LargeImagePath", MethodType.Getter)]
internal static class SCP294LargeImagePathPatch
{
    private static bool Prefix(PotionModel __instance, ref string __result)
    {
        if (__instance is SCP294Potion && SCP294Potion.MixedIcon(__instance, large: true) is { } mixed)
        {
            __result = mixed.ResourcePath;
            return false;
        }
        return true;
    }
}
