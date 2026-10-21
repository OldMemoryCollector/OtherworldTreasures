using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib;
using STS2RitsuLib.Data;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Utils.Persistence;

namespace OtherworldTreasures.Scripts.Potions;
public sealed partial class Scp294Config
{
    public bool CrazyMode { get; set; }
    public bool EverywhereMode { get; set; }
    public bool CrazyNeowRelics { get; set; }
    public bool CrazyMonsterPotions { get; set; }
    public bool CrazyPotionIntents { get; set; }
    public bool RainbowValues { get; set; } = true;
    public bool ShowChoiceDetails { get; set; } = true;
}

internal static partial class Scp294Settings
{
    private const string Key = "scp294_options";
    private static ModDataStore? _store;
    private static readonly Scp294Config Defaults = new();
    internal static Scp294Config Data => _store?.IsGlobalInitialized == true ? _store.Get<Scp294Config>(Key) : Defaults;
    internal static bool CrazyMode => Data.CrazyMode;
    internal static bool EverywhereMode => Data.EverywhereMode;
    internal static bool RainbowValues => Data.RainbowValues;
    internal static bool ShowChoiceDetails => Data.ShowChoiceDetails;

    internal static void Initialize()
    {
        _store = RitsuLibFramework.GetDataStore(Entry.ModId);
        using (RitsuLibFramework.BeginModDataRegistration(Entry.ModId))
            _store.Register(Key, "scp294_options.json", SaveScope.Global, static () => new Scp294Config());
        RitsuLibFramework.RegisterModSettings(Entry.ModId, page =>
        {
            page.WithTitle(Text("随机药水", "Random potions", "ランダムポーション"))
                .WithModDisplayName(Text("异次元乱入", "Otherworld Treasures", "異次元乱入"))
                .WithDescription(Text("药效设置影响新药水；数值配色可随时切换。", "Effect settings apply to new brews. Number colors can be changed at any time.", "効果設定は新しい薬に適用。数値の配色はいつでも変更できます。"))
                .AddSection("modes", section =>
                {
                    section.WithTitle(Text("药水模式", "Potion modes", "薬のモード"));
                    section.AddToggle("crazy", Text("疯狂模式", "Crazy mode", "クレイジーモード"),
                        ModSettingsBindings.Global<Scp294Config, bool>(Entry.ModId, Key, d => d.CrazyMode, (d, v) => d.CrazyMode = v),
                        Text("可变强度在0～原版对应数值的10倍之间等概率随机，包含两端；无原版对应的自定义项按常规上限计算；灾厄统一为1～10层。", "Variable potency is uniformly random from zero to ten times vanilla, inclusive. Custom effects without a vanilla counterpart use their normal maximum. Doom is always 1–10.", "可変効果は原作の0～10倍の整数から等確率で抽選し、両端を含みます。対応する原作効果がなければ通常上限を基準にします。破滅は常に1～10。"));
                    section.AddToggle("everywhere", Text("常规模式", "Everywhere mode", "常態モード"),
                        ModSettingsBindings.Global<Scp294Config, bool>(Entry.ModId, Key, d => d.EverywhereMode, (d, v) => d.EverywhereMode = v),
                        Text("禁用咖啡机事件，局内新获得的所有药水变为随机调合药水；可与疯狂模式同时开启。", "Disables the coffee-machine event and randomizes every new potion acquired during a run. Can combine with Crazy mode.", "コーヒーマシンのイベントを無効化し、ラン中の新しい薬をすべてランダム化。クレイジーモードと併用できます。"));
                    section.AddToggle("rainbow_values", Text("彩虹数值", "Rainbow numbers", "虹色の数値"),
                        ModSettingsBindings.Global<Scp294Config, bool>(Entry.ModId, Key, d => d.RainbowValues, (d, v) => d.RainbowValues = v),
                        Text("关闭后，可变数值改用原版蓝色。", "Turn off to show variable values in vanilla blue.", "オフにすると可変数値を原作の青色で表示します。"));
                    section.AddToggle("details", Text("事件选项详情", "Event choice details", "イベント選択肢の詳細"),
                        ModSettingsBindings.Global<Scp294Config, bool>(Entry.ModId, Key, d => d.ShowChoiceDetails, (d, v) => d.ShowChoiceDetails = v),
                        Text("显示口味、感觉的简要说明；浓度可预览产物。", "Show brief taste and feeling details; concentration choices preview their brew.", "味と感情の短い説明を表示。濃度では完成品を確認できます。"));
                })
                .AddSection("crazy_extras", section =>
                {
                    section.WithTitle(Text("常规模式附属选项", "Everywhere mode extras", "常態モード追加設定"))
                        .WithEnabledWhen(() => EverywhereMode);
                    section.AddToggle("neow_relics", Text("本地酿造厂", "Local Brewery", "地元の醸造所"),
                        ModSettingsBindings.Global<Scp294Config, bool>(Entry.ModId, Key, d => d.CrazyNeowRelics, (d, v) => d.CrazyNeowRelics = v),
                        Text("涅奥遗物被替换为白兽雕像、药水腰带、药瓶皮套。", "New runs offer one Neow option granting White Beast Statue, Potion Belt and Phial Holster together.", "新しいランのネオーに、ホワイトビーストの像・ポーションベルト・小瓶ホルスターをすべて獲得する選択肢を1つ表示。"));
                    section.AddToggle("monster_potions", Text("狂乱鸡尾酒", "Frenzied Cocktail", "狂乱のカクテル"),
                        ModSettingsBindings.Global<Scp294Config, bool>(Entry.ModId, Key, d => d.CrazyMonsterPotions, (d, v) => d.CrazyMonsterPotions = v),
                        Text("战斗开始时，怪物会使用一瓶随机药水。", "At the start of combat, monsters use a random potion.", "戦闘開始時、モンスターがランダムなポーションを1本使用する。"));
                    section.AddToggle("potion_intents", Text("为什么会变成这样呢？（测试中，未完善）", "Why did it turn out like this? (Testing, incomplete)", "どうしてこうなった？（テスト中・未完成）"),
                        ModSettingsBindings.Global<Scp294Config, bool>(Entry.ModId, Key, d => d.CrazyPotionIntents, (d, v) => d.CrazyPotionIntents = v),
                        Text("随机替代原意图，或附加一个随机药水意图。", "Randomly replace the original intents or add a random potion intent.", "元の行動予告をランダムに置き換えるか、ランダムなポーションの予告を追加する。"));
                });
            RegisterStatusPurge(page);
        }, "scp294");
    }

    private static ModSettingsText Text(string zhs, string eng, string jpn) => ModSettingsText.Dynamic(() =>
        MegaCrit.Sts2.Core.Localization.LocManager.Instance.Language switch { "eng" => eng, "jpn" => jpn, _ => zhs });
    internal static bool CanAccept(Player player, int slotIndex) => slotIndex >= 0
        ? slotIndex < player.MaxPotionCount && player.GetPotionAtSlotIndex(slotIndex) == null : player.HasOpenPotionSlots;
    internal static bool ShouldReplace(PotionModel potion, Player player) => EverywhereMode &&
        potion.IsMutable && potion is not SCP294Potion && !SCP294Potion.IsCatalogBrew(potion) && player.RunState != null;
}

[HarmonyPatch(typeof(PotionCmd), nameof(PotionCmd.TryToProcure), new[] { typeof(PotionModel), typeof(Player), typeof(int) })]
internal static class Scp294ProcureRandomizePatch
{
    private static void Prefix(ref PotionModel potion, Player player, int slotIndex)
    {
        if (Scp294Settings.ShouldReplace(potion, player) && Scp294Settings.CanAccept(player, slotIndex))
            potion = SCP294Potion.BrewForPlayer(player);
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.AddPotionInternal))]
internal static class Scp294DirectPotionRandomizePatch
{
    private static void Prefix(Player __instance, ref PotionModel potion, int slotIndex, bool silent)
    {
        if (silent || !Scp294Settings.ShouldReplace(potion, __instance)) return;
        if (Scp294Settings.CanAccept(__instance, slotIndex))
            potion = SCP294Potion.BrewForPlayer(__instance);
    }
}
