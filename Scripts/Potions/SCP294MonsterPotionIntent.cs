using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace OtherworldTreasures.Scripts.Potions;

internal sealed class Scp294MonsterPotionIntent(MonsterBrewPlan plan) : AbstractIntent
{
    internal MonsterBrewPlan Plan => plan;
    public override IntentType IntentType => IntentType.Unknown;
    protected override string IntentPrefix => "OTHERWORLD_TREASURES_MONSTER_POTION";
    protected override string SpritePath => "packed/potion/fire_potion.png";
    public override string GetAnimation(IEnumerable<Creature> targets, Creature owner) => "unknown";
    public override Texture2D GetTexture(IEnumerable<Creature> targets, Creature owner) => plan.DisplayPotion.Image;
    protected override LocString GetIntentDescription(IEnumerable<Creature> targets, Creature owner)
    {
        var description = new LocString("intents", IntentPrefix + ".description");
        description.Add("Count", plan.Effects.Length);
        description.Add("Effects", SCP294Potion.MonsterDescription(plan));
        return description;
    }
}

// The native intent animator accepts a fixed animation table. Keep its normal hover/bob behavior,
// replace only our intent's sprite frames and label with the potion image and effect count.
[HarmonyPatch(typeof(NIntent), "UpdateVisuals")]
internal static class Scp294MonsterPotionIntentVisuals
{
    private sealed record ScaleState(Vector2 Scale);
    private static readonly ConditionalWeakTable<NIntent, ScaleState> Scales = new();
    internal static void Restore(NIntent intent)
    {
        if (!Scales.TryGetValue(intent, out var original)) return;
        intent.GetNode<Sprite2D>("%Intent").Scale = original.Scale;
        AccessTools.Field(typeof(NIntent), "_animationName").SetValue(intent, null);
        Scales.Remove(intent);
    }
    private static readonly FieldInfo Intent = AccessTools.Field(typeof(NIntent), "_intent");
    private static readonly FieldInfo Frames = AccessTools.Field(typeof(NIntent), "_animationFrames");
    private static void Postfix(NIntent __instance)
    {
        if (Intent.GetValue(__instance) is not Scp294MonsterPotionIntent potion) return;
        ((List<Texture2D>)Frames.GetValue(__instance)!).Clear();
        var sprite = __instance.GetNode<Sprite2D>("%Intent");
        Scales.GetValue(__instance, _ => new ScaleState(sprite.Scale));
        sprite.Texture = potion.Plan.DisplayPotion.Image;
        sprite.Scale = Vector2.One * (52f / Math.Max(1, sprite.Texture.GetWidth()));
        __instance.GetNode<MegaRichTextLabel>("%Value").Text = potion.Plan.Effects.Length.ToString();
    }
}

public sealed partial class SCP294Potion
{
    internal static string MonsterEffectName(BrewEffect effect) => EffectName(effect);
}

[HarmonyPatch(typeof(NIntent), nameof(NIntent.UpdateIntent))]
internal static class Scp294MonsterPotionIntentReset
{
    private static void Prefix(NIntent __instance) => Scp294MonsterPotionIntentVisuals.Restore(__instance);
}

[HarmonyPatch(typeof(AbstractIntent), nameof(AbstractIntent.GetHoverTip))]
internal static class Scp294MonsterPotionHoverTip
{
    private static bool Prefix(AbstractIntent __instance, ref HoverTip __result)
    {
        if (__instance is not Scp294MonsterPotionIntent potion) return true;
        var display = potion.Plan.DisplayPotion;
        __result = new HoverTip(display.Title, SCP294Potion.MonsterDescription(potion.Plan), display.Image)
        { Id = $"OtherworldMonsterPotion:{potion.Plan.Monster.Creature.CombatId}:{potion.Plan.Move.Id}:{potion.Plan.Round}" };
        return false;
    }
}
