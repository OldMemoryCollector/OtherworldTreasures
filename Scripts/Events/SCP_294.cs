using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Runs;
using OtherworldTreasures.Scripts.Potions;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Events;

[RegisterSharedEvent]
public sealed partial class Scp294 : OtherworldEvent
{
    private const int UseCost = 50;
    private bool _paid;
    private bool _starting;
    private Taste? _taste;
    private Feeling? _feeling;
    private PotionModel? _pendingBrew;
    private bool _finished;
    private Dictionary<Concentration, SCP294Potion> _concentrationBrews = new();
    private Dictionary<string, PotionModel> _brewPreviews = new();
    private IReadOnlyList<EventOption>? _concentrationOptions;

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _concentrationBrews = new();
        _brewPreviews = new();
        _concentrationOptions = null;
        ResetNamedBrew();
    }

    // Generated full-screen coffee-machine illustration with dark space for event text.
    public override EventAssetProfile AssetProfile => new(
        InitialPortraitPath: "res://OtherworldTreasures/images/events/scp294_coffee_machine.png");

    protected override bool AncientExempt => true;

    public override bool IsAllowed(IRunState runState) => !Scp294Settings.EverywhereMode &&
        Scp294EncounterRules.IsAllowed(runState, Id);

    protected override Task BeforeEventStarted(bool isPreFinished)
    {
        // Preserve the original first-visit stream, including unfinished saves from earlier versions.
        if (Scp294EncounterRules.PriorVisits(Owner.RunState, Id) == 0)
            return base.BeforeEventStarted(isPreFinished);
        // Later visits vary by act and rebuild deterministically after a load.
        Rng.LoadFromSerializable(new MegaCrit.Sts2.Core.Random.Rng(Owner, Id,
            (ulong)Owner.RunState.CurrentActIndex * 294UL).ToSerializable());
        return base.BeforeEventStarted(isPreFinished);
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        NamedOption("RANDOM_PAY",Scp294InputText.Text("随机调制 · 50金币","Random brew · 50 Gold","ランダム調合・50ゴールド"),Scp294InputText.Text("选择口味、感觉与浓度，调制一杯随机药水。","Choose a taste, feeling and concentration.","味・気分・濃度を選んで調合。"),Owner.Gold >= UseCost ? StartBrew : null),
        NamedOption("CUSTOM_PAY",Scp294InputText.Text("自由输入 · 50金币","Name a drink · 50 Gold","自由入力・50ゴールド"),Scp294InputText.Text("根据输入的内容进行识别，调制一杯饮品。","Brew a drink based on the content you enter.","入力された内容から飲み物を調合。"),Owner.Gold >= UseCost ? StartNamedBrew : null),
        Leave()
    ];

    private async Task StartBrew()
    {
        if (_paid || _starting || _finished) return;
        if (Owner.Gold < UseCost)
        {
            SetEventState(PageDescription("INITIAL"), GenerateInitialOptions());
            return;
        }

        _starting = true;
        try
        {
            await PlayerCmd.LoseGold(UseCost, Owner, GoldLossType.Spent);
            _paid = true;
            await ShowRandomOptions("TASTE", Enum.GetValues<Taste>().Select(taste =>
                ((int)taste, TasteName(taste), (Func<Task>)(() => ChooseTaste(taste)))));
        }
        finally { _starting = false; }
    }

    private Task ChooseTaste(Taste taste)
    {
        if (!_paid || _finished) return Task.CompletedTask;
        _taste = taste;
        return ShowRandomOptions("FEELING", Enum.GetValues<Feeling>().Select(feeling =>
            ((int)feeling, FeelingName(feeling), (Func<Task>)(() => ChooseFeeling(feeling)))));
    }

    private Task ChooseFeeling(Feeling feeling)
    {
        if (!_paid || _finished) return Task.CompletedTask;
        _feeling = feeling;
        return ShowConcentrationOptions();
    }

    private Task ShowConcentrationOptions()
    {
        if (_taste == null || _feeling == null) return Task.CompletedTask;
        if (_concentrationOptions == null)
        {
            var choices = Enum.GetValues<Concentration>().OrderBy(_ => Rng.NextInt(int.MaxValue)).Take(3).ToArray();
            _concentrationOptions = choices.Select(concentration =>
            {
                var brew = SCP294Potion.Brew(this, _taste.Value, _feeling.Value, concentration);
                brew.Owner = Owner;
                _concentrationBrews.Add(concentration, brew);
                string key = ModOptionKey("CONCENTRATION", $"CHOICE_{(int)concentration}");
                _brewPreviews.Add(key, brew);
                var title = new LocString(LocTable, Id.Entry + ".pages.CHOICE_OPTION.title");
                title.Add("Option", $"{ConcentrationName(concentration)} · {SCP294Potion.BrewNameOf(brew)}");
                var description = new LocString(LocTable, Id.Entry + ".pages.CHOICE_OPTION.description");
                description.Add("Details", brew.PreviewSummary);
                return new EventOption(this, () => ChooseConcentration(concentration), title, description,
                    key, new IHoverTip[] { BrewPreviewHoverTip(brew, key) });
            }).ToArray();
        }
        SetEventState(PageDescription("CONCENTRATION"), _concentrationOptions);
        return Task.CompletedTask;
    }

    internal bool TryGetBrewPreview(EventOption option, out PotionModel brew) =>
        _brewPreviews.TryGetValue(option.TextKey, out brew!);

    internal static HoverTip BrewPreviewHoverTip(PotionModel brew, string key) => new(
        SCP294Potion.MakeTitle(brew), SCP294Potion.MakeDescription(brew), brew.Image) { Id = key + ".brew" };

    private async Task ChooseConcentration(Concentration concentration)
    {
        if (!_paid || _finished || _taste == null || _feeling == null) return;
        if (!_concentrationBrews.TryGetValue(concentration, out var preview)) return;
        _pendingBrew ??= preview;
        await ClaimBrew();
    }

    private async Task ClaimBrew()
    {
        if (_finished || _claiming || _pendingBrew is not { } brew) return;
        _claiming = true;
        try
        {
        var result = await MegaCrit.Sts2.Core.Commands.PotionCmd.TryToProcure(brew, Owner);
        if (!result.success)
        {
            if (result.failureReason != PotionProcureFailureReason.TooFull)
            {
                SetEventState(PageDescription("UNAVAILABLE"), new[] { Leave() });
                return;
            }

            var fullDescription = PageDescription("FULL");
            fullDescription.Add("Potion", SCP294Potion.BrewNameOf(brew));
            SetEventState(fullDescription, new[] { new EventOption(this, ClaimBrew, InitialOptionKey("CLAIM")) }
                .Concat(NamedRedoOptions()).Append(Leave()));
            return;
        }

        _finished = true;
        _pendingBrew = null;
        var description = PageDescription("RESULT");
        description.Add("Potion", SCP294Potion.BrewNameOf(brew));
        SetEventFinished(description);
        }
        finally { _claiming = false; }
    }

    private Task ShowRandomOptions(string page, IEnumerable<(int Id, string Name, Func<Task> Select)> all)
    {
        var choices = all.OrderBy(_ => Rng.NextInt(int.MaxValue)).Take(3).ToArray();
        var options = choices.Select(choice =>
        {
            var title = new LocString(LocTable, Id.Entry + ".pages.CHOICE_OPTION.title");
            title.Add("Option", choice.Name);
            var details = new LocString(LocTable, Id.Entry + ".pages.CHOICE_OPTION.description");
            details.Add("Option", choice.Name);
            details.Add("Details", Scp294Settings.ShowChoiceDetails
                ? SCP294Potion.ChoiceDetails(page, choice.Id, _taste, _feeling) : choice.Name);
            string key = ModOptionKey(page, $"CHOICE_{choice.Id}");
            return new EventOption(this, choice.Select, title, details, key, Array.Empty<IHoverTip>());
        }).ToArray();
        SetEventState(PageDescription(page), options);
        return Task.CompletedTask;
    }

    private static string TasteName(Taste taste) => LocText($"taste.{taste}");
    private static string FeelingName(Feeling feeling) => LocText($"feeling.{feeling}");
    private static string ConcentrationName(Concentration concentration) => LocText($"concentration.{concentration}");

    private static string LocText(string suffix)
    {
        // 事件 Id 是 Scp294 → "SCP294"（和 SCP914 同一套命名），本地化键也必须带同一个前缀。
        string key = $"OTHERWORLD_TREASURES_EVENT_SCP294.{suffix}";
        return new LocString("events", key).GetFormattedText();
    }
}

internal enum Taste
{
    Sweet, Bitter, Sour, Spicy, Salty, Cool, Numbing, Astringent, Savory, Strange
}

internal enum Feeling
{
    Romantic, Sad, Angry, Elegant, Wonderful, Strange, Mad, Serene,
    Terrifying, Excited, Tired, Lonely, Despairing, Hopeful, Jealous,
    Nostalgic, Dreamlike, Eerie, Sacred, Absurd, Gentle, Cruel, Light, Heavy
}

internal enum Concentration
{
    Thin, Mild, Balanced, Strong, ExtraStrong, Overwhelming
}
