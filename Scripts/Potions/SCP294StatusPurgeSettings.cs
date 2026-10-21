using STS2RitsuLib.Settings;
namespace OtherworldTreasures.Scripts.Potions;

public sealed partial class Scp294Config
{
    public bool StatusPurgeEnabled { get; set; }
    public string StatusPurgeInterval { get; set; } = "1";
    public StatusPurgeTarget StatusPurgeTarget { get; set; } = StatusPurgeTarget.Everyone;
    public StatusPurgeSelection StatusPurgeSelection { get; set; } = StatusPurgeSelection.All;
    public StatusPurgeLayers StatusPurgeLayers { get; set; } = StatusPurgeLayers.One;
    public StatusPurgePolarity StatusPurgePolarity { get; set; } = StatusPurgePolarity.Negative;
}

internal static partial class Scp294Settings
{
    private static void RegisterStatusPurge(ModSettingsPageBuilder page) => page.AddSection("status_purge", section =>
    {
        section.WithTitle(Text("周期状态清除", "Periodic status removal", "定期状態解除"))
            .WithDescription(Text("每X个完整回合结束时清除；单项为每个目标随机一种，一半向上取整。开局生效。", "Remove statuses after every X complete rounds. Single picks one per creature; half rounds up. Applies to new runs.", "Xラウンド終了ごとに解除。単項目は各対象から1種類をランダム選択、半分は切り上げ。新しいランに適用。"))
            .WithEnabledWhen(() => EverywhereMode);
        section.AddToggle("enabled", Text("启用状态清除", "Enable status removal", "状態解除を有効化"),
            ModSettingsBindings.Global<Scp294Config, bool>(Entry.ModId, Key, d => d.StatusPurgeEnabled, (d,v) => d.StatusPurgeEnabled=v));
        section.AddString("interval", Text("每X回合", "Every X rounds", "Xラウンドごと"),
            ModSettingsBindings.Global<Scp294Config,string>(Entry.ModId,Key,d=>d.StatusPurgeInterval,(d,v)=>d.StatusPurgeInterval=v),
            placeholder: Text("1", "1", "1"), maxLength: 10,
            description: Text("X须为正整数。", "X must be a positive integer.", "Xは正の整数。"), valueValidationVisual: Scp294StatusPurge.ValidInterval);
        section.AddEnumChoice("target", Text("作用对象", "Targets", "対象"),
            ModSettingsBindings.Global<Scp294Config,StatusPurgeTarget>(Entry.ModId,Key,d=>d.StatusPurgeTarget,(d,v)=>d.StatusPurgeTarget=v),
            value=>value switch { StatusPurgeTarget.Enemies=>Text("敌方","Enemies","敵側"),StatusPurgeTarget.Allies=>Text("我方","Allies","味方"),_=>Text("全体","Everyone","全体") }, presentation:ModSettingsChoicePresentation.Dropdown);
        section.AddEnumChoice("selection", Text("清除种类", "Status selection", "種類"),
            ModSettingsBindings.Global<Scp294Config,StatusPurgeSelection>(Entry.ModId,Key,d=>d.StatusPurgeSelection,(d,v)=>d.StatusPurgeSelection=v),
            value=>value==StatusPurgeSelection.Single?Text("单项","Single","単項目"):Text("全部","All","全種類"), presentation:ModSettingsChoicePresentation.Dropdown);
        section.AddEnumChoice("layers", Text("移除层数", "Layers removed", "解除するスタック数"),
            ModSettingsBindings.Global<Scp294Config,StatusPurgeLayers>(Entry.ModId,Key,d=>d.StatusPurgeLayers,(d,v)=>d.StatusPurgeLayers=v),
            value=>value switch {StatusPurgeLayers.One=>Text("1","1","1"),StatusPurgeLayers.Two=>Text("2","2","2"),StatusPurgeLayers.Three=>Text("3","3","3"),StatusPurgeLayers.Half=>Text("一半","Half","半分"),_=>Text("All","All","All")}, presentation:ModSettingsChoicePresentation.Dropdown);
        section.AddEnumChoice("polarity", Text("状态类型", "Status type", "状態の種類"),
            ModSettingsBindings.Global<Scp294Config,StatusPurgePolarity>(Entry.ModId,Key,d=>d.StatusPurgePolarity,(d,v)=>d.StatusPurgePolarity=v),
            value=>value==StatusPurgePolarity.Positive?Text("正面","Positive","強化"):Text("负面","Negative","弱体"), presentation:ModSettingsChoicePresentation.Dropdown);
    });
}
