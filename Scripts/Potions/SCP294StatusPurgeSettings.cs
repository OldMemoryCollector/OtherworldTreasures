using STS2RitsuLib.Settings;
namespace OtherworldTreasures.Scripts.Potions;

public sealed partial class Scp294Config
{
    // Legacy single-rule fields are retained for migration.
    public bool StatusPurgeEnabled { get; set; }
    public string StatusPurgeInterval { get; set; } = "1";
    public StatusPurgeTarget StatusPurgeTarget { get; set; } = StatusPurgeTarget.Everyone;
    public StatusPurgeSelection StatusPurgeSelection { get; set; } = StatusPurgeSelection.All;
    public StatusPurgeLayers StatusPurgeLayers { get; set; } = StatusPurgeLayers.One;
    public StatusPurgePolarity StatusPurgePolarity { get; set; } = StatusPurgePolarity.Negative;
}

public sealed class StatusPurgeGroupConfig
{
    public bool Enabled { get; set; }
    public string Interval { get; set; } = "1";
    public StatusPurgeSelection Selection { get; set; } = StatusPurgeSelection.All;
    public StatusPurgeLayers Layers { get; set; } = StatusPurgeLayers.One;
    public StatusPurgePolarity Polarity { get; set; } = StatusPurgePolarity.Negative;
}

public sealed partial class Scp294Config
{
    public bool StatusPurgeGroupsInitialized { get; set; }
    public StatusPurgeGroupConfig StatusPurgeEnemies { get; set; } = new();
    public StatusPurgeGroupConfig StatusPurgeAllies { get; set; } = new();
    public StatusPurgeGroupConfig StatusPurgeEveryone { get; set; } = new();
}

internal static partial class Scp294Settings
{
    private static string PurgeSection(StatusPurgeTarget target) => target switch
    { StatusPurgeTarget.Enemies => "status_purge_enemies", StatusPurgeTarget.Allies => "status_purge_allies", _ => "status_purge_everyone" };
    private static void RegisterStatusPurge(ModSettingsPageBuilder page)
    {
        Scp294StatusPurge.Migrate(Data);
        foreach (var target in Enum.GetValues<StatusPurgeTarget>()) page.AddSection(PurgeSection(target), section =>
        {
            section.WithTitle(target switch
            { StatusPurgeTarget.Enemies => Text("敌方状态清除", "Enemy status removal", "敵側の状態解除"),
              StatusPurgeTarget.Allies => Text("我方状态清除", "Ally status removal", "味方の状態解除"),
              _ => Text("全体状态清除", "Everyone status removal", "全体の状態解除") })
                .WithDescription(target == StatusPurgeTarget.Everyone
                    ? Text("独立检查回合间隔；与敌方、我方规则重叠时分别执行。开局生效。", "Independent interval; overlaps with the enemy and ally rules apply separately. Applies to new runs.", "間隔は独立。敵側・味方の設定と重なる場合はそれぞれ適用。新しいランに適用。")
                    : Text("每X个完整回合结束时清除；单项随机一种，一半向上取整。开局生效。", "Remove after every X complete rounds. Single picks one per creature; half rounds up. Applies to new runs.", "Xラウンド終了ごとに解除。単項目はランダムに1種類、半分は切り上げ。新しいランに適用。"))
                .WithEnabledWhen(() => EverywhereMode);
            section.AddToggle("enabled", Text("启用状态清除", "Enable status removal", "状態解除を有効化"),
                ModSettingsBindings.Global<Scp294Config,bool>(Entry.ModId,Key,d=>Scp294StatusPurge.Options(d,target).Enabled,(d,v)=>Scp294StatusPurge.Options(d,target).Enabled=v));
            section.AddString("interval", Text("每X回合", "Every X rounds", "Xラウンドごと"),
                ModSettingsBindings.Global<Scp294Config,string>(Entry.ModId,Key,d=>Scp294StatusPurge.Options(d,target).Interval,(d,v)=>Scp294StatusPurge.Options(d,target).Interval=v),
                placeholder:Text("1","1","1"),maxLength:10,description:Text("X须为正整数。","X must be a positive integer.","Xは正の整数。"),valueValidationVisual:Scp294StatusPurge.ValidInterval);
            section.AddEnumChoice("selection",Text("清除种类","Status selection","種類"),
                ModSettingsBindings.Global<Scp294Config,StatusPurgeSelection>(Entry.ModId,Key,d=>Scp294StatusPurge.Options(d,target).Selection,(d,v)=>Scp294StatusPurge.Options(d,target).Selection=v),
                v=>v==StatusPurgeSelection.Single?Text("单项","Single","単項目"):Text("全部","All","全種類"),presentation:ModSettingsChoicePresentation.Dropdown);
            section.AddEnumChoice("layers",Text("移除层数","Layers removed","解除するスタック数"),
                ModSettingsBindings.Global<Scp294Config,StatusPurgeLayers>(Entry.ModId,Key,d=>Scp294StatusPurge.Options(d,target).Layers,(d,v)=>Scp294StatusPurge.Options(d,target).Layers=v),
                v=>v switch{StatusPurgeLayers.One=>Text("1","1","1"),StatusPurgeLayers.Two=>Text("2","2","2"),StatusPurgeLayers.Three=>Text("3","3","3"),StatusPurgeLayers.Half=>Text("一半","Half","半分"),_=>Text("All","All","All")},presentation:ModSettingsChoicePresentation.Dropdown);
            section.AddEnumChoice("polarity",Text("状态类型","Status type","状態の種類"),
                ModSettingsBindings.Global<Scp294Config,StatusPurgePolarity>(Entry.ModId,Key,d=>Scp294StatusPurge.Options(d,target).Polarity,(d,v)=>Scp294StatusPurge.Options(d,target).Polarity=v),
                v=>v switch{StatusPurgePolarity.Positive=>Text("正面","Positive","強化"),StatusPurgePolarity.Negative=>Text("负面","Negative","弱体"),_=>Text("全部","All","すべて")},presentation:ModSettingsChoicePresentation.Dropdown);
        });
    }
}
