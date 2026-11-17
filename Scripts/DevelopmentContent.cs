using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using STS2RitsuLib.Scaffolding.Godot.NodeAttachments;

[assembly: AssemblyMetadata("Otherworld.BuildFlavor", OtherworldTreasures.Scripts.DevelopmentContent.Flavor)]
[assembly: AssemblyMetadata("Otherworld.DevelopmentGroup", OtherworldTreasures.Scripts.DevelopmentContent.Group)]
[assembly: AssemblyMetadata("Otherworld.GameApi", OtherworldTreasures.Scripts.DevelopmentContent.GameApi)]

namespace OtherworldTreasures.Scripts;

// Compile-time only: a published DLL cannot be opened by a local config or launch argument.
internal static class DevelopmentContent
{
#if OW_DEVELOPMENT
    internal const bool Enabled = true;
    internal const string Flavor = "Development";
#else
    internal const bool Enabled = false;
    internal const string Flavor = "Release";
#endif
#if OW_DEV_ALL
    internal const string Group = "All";
#elif OW_DEV_NINE
    internal const string Group = "Nine";
#elif OW_DEV_SPIDER
    internal const string Group = "Spider";
#elif OW_DEV_GINTAMA
    internal const string Group = "Gintama";
#elif OW_DEV_EXTRAS
    internal const string Group = "Extras";
#else
    internal const string Group = "None";
#endif
#if STS2_113
    internal const string GameApi = "0.113.0";
#else
    internal const string GameApi = "0.111.0";
#endif
    internal const bool Nine = Enabled && (Group == "All" || Group == "Nine");
    internal const bool Spider = Enabled && (Group == "All" || Group == "Spider");
    internal const bool Gintama = Enabled && (Group == "All" || Group == "Gintama");
    internal const bool Extras = Enabled && (Group == "All" || Group == "Extras");

    internal static void Initialize()
    {
        Entry.Logger.Info($"[BuildFlavor] {Flavor} group={Group} api={GameApi}");
#if OW_DEVELOPMENT
        var attachments = ModNodeAttachmentRegistry.For(Entry.ModId);
        attachments.RegisterReadyChild<NMainMenu, DevelopmentBanner>("development_main_menu", static _ => new DevelopmentBanner(),
            new NodeAttachmentOptions { Name = "OtherworldDevelopmentBanner", DuplicatePolicy = NodeAttachmentDuplicatePolicy.ReuseExistingByName });
        attachments.RegisterReadyChild<NRun, DevelopmentBanner>("development_run", static _ => new DevelopmentBanner(),
            new NodeAttachmentOptions { Name = "OtherworldDevelopmentBanner", DuplicatePolicy = NodeAttachmentDuplicatePolicy.ReuseExistingByName });
#endif
    }
}

#if OW_DEVELOPMENT
internal sealed partial class DevelopmentBanner : CanvasLayer
{
    public override void _Ready()
    {
        Layer = 20;
        string group = DevelopmentContent.Group switch { "All" => "全部", "Nine" => "奈茵", "Spider" => "蜘蛛", "Gintama" => "银魂", _ => "其他开发内容" };
        var label = new Label { Name = "Label", Text = "本机开发版 · " + group,
            Position = new Vector2(640, 18), MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", 24);
        label.AddThemeColorOverride("font_color", new Color("ffd16b"));
        label.AddThemeColorOverride("font_shadow_color", Colors.Black);
        label.AddThemeConstantOverride("shadow_offset_x", 2);
        label.AddThemeConstantOverride("shadow_offset_y", 2);
        AddChild(label);
    }
}
#endif
