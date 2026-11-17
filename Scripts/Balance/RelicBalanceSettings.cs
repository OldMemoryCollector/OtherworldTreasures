using OtherworldTreasures.Scripts.Balance;
using STS2RitsuLib.Settings;

namespace OtherworldTreasures.Scripts.Potions;

public sealed partial class Scp294Config
{
    public RelicBalanceOptions RelicBalance { get; set; } = new();
}

internal static partial class Scp294Settings
{
    private static void RegisterRelicBalance(ModSettingsPageBuilder page)
    {
        page.AddSection("relic_balance", section =>
        {
            section.WithTitle(ModSettingsText.Dynamic(() => "遗物平衡"))
                .WithDescription(ModSettingsText.Dynamic(() => "分别设置，新冒险生效。"));
            section.AddToggle("kuras_dice", ModSettingsText.Dynamic(() => "库拉的骰子"),
                ModSettingsBindings.Global<Scp294Config, bool>(Entry.ModId, Key,
                    d => d.RelicBalance?.KurasDice == true,
                    (d, value) => { d.RelicBalance ??= new(); d.RelicBalance.KurasDice = value; }));
            section.AddToggle("what_if_phone_booth", ModSettingsText.Dynamic(() => "如果电话亭"),
                ModSettingsBindings.Global<Scp294Config, bool>(Entry.ModId, Key,
                    d => d.RelicBalance?.WhatIfPhoneBooth == true,
                    (d, value) => { d.RelicBalance ??= new(); d.RelicBalance.WhatIfPhoneBooth = value; }));
            section.AddToggle("shrink_ray", ModSettingsText.Dynamic(() => "缩小灯"),
                ModSettingsBindings.Global<Scp294Config, bool>(Entry.ModId, Key,
                    d => d.RelicBalance?.ShrinkRay == true,
                    (d, value) => { d.RelicBalance ??= new(); d.RelicBalance.ShrinkRay = value; }));
            section.AddToggle("yorozuya_scooter", ModSettingsText.Dynamic(() => "万事屋的电动车"),
                ModSettingsBindings.Global<Scp294Config, bool>(Entry.ModId, Key,
                    d => d.RelicBalance?.YorozuyaScooter == true,
                    (d, value) => { d.RelicBalance ??= new(); d.RelicBalance.YorozuyaScooter = value; }));
        });
    }
}
