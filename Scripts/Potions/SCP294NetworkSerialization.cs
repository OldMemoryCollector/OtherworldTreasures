using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace OtherworldTreasures.Scripts.Potions;

// BREW_ IDs contain a recipe rather than an entry in the fixed model-ID table.
// Put the registered potion ID on the wire and append its recipe to that object.
// Ordinary potions/rewards retain the native packet layout; JSON saves keep BREW_.
internal static class Scp294NetworkRecipes
{
    internal static ModelId WireId => ModelDb.Potion<SCP294Potion>().Id;
    internal static bool IsBrew(ModelId? id) => id?.Category == WireId.Category
        && id.Entry.StartsWith("BREW_", StringComparison.Ordinal);
    private static bool IsMarker(ModelId? id) => id == WireId;

    internal static void WritePayload(PacketWriter writer, ModelId? original, ModelId? current)
    {
        if (original != null || IsMarker(current)) writer.WriteString(original?.Entry ?? "");
    }

    internal static ModelId ReadPayload(PacketReader reader, ModelId id)
    {
        if (!IsMarker(id)) return id;
        string entry = reader.ReadString();
        if (entry.Length == 0) return id;
        if (entry.Length > 32768 || !entry.StartsWith("BREW_", StringComparison.Ordinal))
            throw new InvalidDataException("Invalid random-potion recipe in network packet.");
        return new ModelId(id.Category, entry);
    }
}

[HarmonyPatch(typeof(SerializablePotion), nameof(SerializablePotion.Serialize))]
internal static class Scp294PotionPacketWrite
{
    private static void Prefix(SerializablePotion __instance, out ModelId? __state)
    {
        __state = Scp294NetworkRecipes.IsBrew(__instance.Id) ? __instance.Id : null;
        if (__state != null) __instance.Id = Scp294NetworkRecipes.WireId;
    }

    private static void Postfix(SerializablePotion __instance, PacketWriter writer, ModelId? __state)
    {
        if (__state != null) __instance.Id = __state;
        Scp294NetworkRecipes.WritePayload(writer, __state, __instance.Id);
    }

    private static void Finalizer(SerializablePotion __instance, ModelId? __state)
    {
        if (__state != null) __instance.Id = __state;
    }
}

[HarmonyPatch(typeof(SerializablePotion), nameof(SerializablePotion.Deserialize))]
internal static class Scp294PotionPacketRead
{
    private static void Postfix(SerializablePotion __instance, PacketReader reader)
    {
        if (__instance.Id != null)
            __instance.Id = Scp294NetworkRecipes.ReadPayload(reader, __instance.Id);
    }
}

[HarmonyPatch(typeof(SerializableReward), nameof(SerializableReward.Serialize))]
internal static class Scp294RewardPacketWrite
{
    private static void Prefix(SerializableReward __instance, out ModelId? __state)
    {
        __state = Scp294NetworkRecipes.IsBrew(__instance.PredeterminedModelId)
            ? __instance.PredeterminedModelId : null;
        if (__state != null) __instance.PredeterminedModelId = Scp294NetworkRecipes.WireId;
    }

    private static void Postfix(SerializableReward __instance, PacketWriter writer, ModelId? __state)
    {
        if (__state != null) __instance.PredeterminedModelId = __state;
        Scp294NetworkRecipes.WritePayload(writer, __state, __instance.PredeterminedModelId);
    }

    private static void Finalizer(SerializableReward __instance, ModelId? __state)
    {
        if (__state != null) __instance.PredeterminedModelId = __state;
    }
}

[HarmonyPatch(typeof(SerializableReward), nameof(SerializableReward.Deserialize))]
internal static class Scp294RewardPacketRead
{
    private static void Postfix(SerializableReward __instance, PacketReader reader) =>
        __instance.PredeterminedModelId = Scp294NetworkRecipes.ReadPayload(reader, __instance.PredeterminedModelId);
}
