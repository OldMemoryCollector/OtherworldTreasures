using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Runs;
using OtherworldTreasures.Scripts.Relics;
namespace OtherworldTreasures.Scripts.UI;

public partial class PokerPileHud:Control
{
    private TextureButton _button=null!;
    private Label _count=null!;
    public override void _Ready()
    {
        MouseFilter=MouseFilterEnum.Ignore;
        _button=new TextureButton{Name="DeathPokerPile",TextureNormal=GD.Load<Texture2D>(DeathPoker.PileImage),IgnoreTextureSize=true,StretchMode=TextureButton.StretchModeEnum.KeepAspectCentered,Size=new Vector2(110,110),TooltipText="死亡扑克"};AddChild(_button);
        _count=new Label{Position=new Vector2(68,75),Size=new Vector2(40,35),HorizontalAlignment=HorizontalAlignment.Center,MouseFilter=MouseFilterEnum.Ignore};
        _count.AddThemeFontSizeOverride("font_size",26);_count.AddThemeColorOverride("font_color",Colors.White);_count.AddThemeColorOverride("font_outline_color",Colors.Black);_count.AddThemeConstantOverride("outline_size",6);_button.AddChild(_count);
        _button.Pressed+=()=>{if(Current() is{} relic&&NOverlayStack.Instance is{ScreenCount:0})PokerSelectScreen.Open(relic.Cards,true);};
    }
    private static DeathPoker? Current()=>LocalContext.GetMe(RunManager.Instance.DebugOnlyGetState())?.Relics.OfType<DeathPoker>().FirstOrDefault();
    public override void _Process(double delta)
    {
        if(GetParent() is not NCombatUi ui)return;
        var relic=Current();Visible=relic!=null&&CombatManager.Instance.IsInProgress;
        if(!Visible)return;
        _count.Text=relic!.Cards.Count.ToString();
        var draw=ui.DrawPile;var icon=draw.GetNodeOrNull<Control>("Icon");
        // Anchor to the actual draw-pile icon, inheriting the native combat UI's animation and visibility.
        var rect=(icon??draw).GetGlobalRect();
        _button.GlobalPosition=new Vector2(rect.Position.X+(rect.Size.X-110)/2,rect.Position.Y-124);
        _button.Disabled=NOverlayStack.Instance is not{ScreenCount:0};
    }
}
