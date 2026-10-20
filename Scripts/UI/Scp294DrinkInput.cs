using System.Text;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using OtherworldTreasures.Scripts.Potions;
namespace OtherworldTreasures.Scripts.UI;

// Text entry changes only local UI. Confirmed text is synchronized by the event's native player choice.
public partial class Scp294DrinkInput : Control
{
    private readonly TaskCompletionSource<string?> _completion=new();
    private NMegaLineEdit? _input;
    private Button? _confirm;
    private MegaRichTextLabel? _recognition;
    private string _initial="";
    private double _exampleTime;
    internal Task<string?> Completion=>_completion.Task;
    internal static Scp294DrinkInput Open(Control parent,string initial,bool freeRedo)
    {
        var dialog=new Scp294DrinkInput{Name="Scp294DrinkInput",_initial=initial};parent.AddChild(dialog);dialog.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);dialog.Build(freeRedo);return dialog;
    }
    private void Build(bool freeRedo)
    {
        MouseFilter=MouseFilterEnum.Stop;ZIndex=100;
        var shade=new ColorRect{Color=new Color(0,0,0,.72f),MouseFilter=MouseFilterEnum.Stop};AddChild(shade);shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var panel=new Panel{Size=new Vector2(940,420),AnchorLeft=.5f,AnchorRight=.5f,AnchorTop=.5f,AnchorBottom=.5f,OffsetLeft=-470,OffsetRight=470,OffsetTop=-210,OffsetBottom=210};
        panel.AddThemeStyleboxOverride("panel",new StyleBoxFlat{BgColor=new Color("192f3b"),BorderColor=new Color("6e97a0"),BorderWidthLeft=2,BorderWidthTop=2,BorderWidthRight=2,BorderWidthBottom=2,CornerRadiusTopLeft=15,CornerRadiusTopRight=15,CornerRadiusBottomLeft=15,CornerRadiusBottomRight=15});AddChild(panel);
        Label Label(string text,int font,Vector2 position,Color color){var label=new Label{Text=text,Position=position};label.AddThemeFontSizeOverride("font_size",font);label.AddThemeColorOverride("font_color",color);panel.AddChild(label);return label;}
        Label(Scp294InputText.Text("SCP-294 · 你想喝什么？","SCP-294 · What would you like?","SCP-294・何を飲みたい？"),38,new Vector2(34,25),new Color("edcc82"));
        Label(freeRedo?Scp294InputText.Text("免费重做 · 确认后替换当前饮品","Free remake · replaces the current drink","無料の作り直し・現在の飲み物を交換"):Scp294InputText.Text("自由输入饮品名 · 确认调制时支付 50 金币","Name your drink · pay 50 Gold when confirming","飲み物名を自由入力・確定時に50ゴールド"),25,new Vector2(36,89),new Color("a8c2ca"));
        _input=new NMegaLineEdit{Name="DrinkName",Position=new Vector2(36,151),Size=new Vector2(868,68),MaxLength=SCP294Potion.DrinkInputLimit,PlaceholderText=RandomDrinkExample()};_input.AddThemeFontSizeOverride("font_size",28);panel.AddChild(_input);
        var tip=PreloadManager.Cache.GetScene("res://scenes/ui/hover_tip.tscn").Instantiate<Control>();_recognition=(MegaRichTextLabel)tip.GetNode<MegaRichTextLabel>("%Description").Duplicate();tip.Free();_recognition.Name="Recognition";_recognition.Position=new Vector2(36,247);_recognition.Size=new Vector2(868,50);_recognition.AddThemeFontSizeOverride("normal_font_size",26);panel.AddChild(_recognition);
        Button Action(string text,float x){var b=new Button{Text=text,Position=new Vector2(x,324),Size=new Vector2(414,58)};b.AddThemeFontSizeOverride("font_size",27);panel.AddChild(b);return b;}
        var cancel=Action(Scp294InputText.Text("取消","Cancel","キャンセル"),36);_confirm=Action(Scp294InputText.Text("确认调制","Brew","調合する"),490);
        cancel.Pressed+=()=>Finish(null);_confirm.Pressed+=()=>Submit();_input.TextChanged+=_=>Refresh();_input.TextSubmitted+=_=>Submit();_input.Text=_initial;Refresh();_input.GrabFocus();_input.Edit();
        _input.FocusNeighborBottom=_confirm.GetPath();_confirm.FocusNeighborTop=_input.GetPath();_confirm.FocusNeighborLeft=cancel.GetPath();cancel.FocusNeighborRight=_confirm.GetPath();
    }
    private void Refresh()
    {
        if(_input==null||_confirm==null||_recognition==null)return;
        var request=SCP294Potion.ParseDrinkName(_input.Text);_confirm.Disabled=request.Name.Length==0;
        _recognition.Text=Scp294InputText.Text("根据输入的内容进行识别。","Recognizes the content you enter.","入力された内容から判定します。");
    }
    public override void _Process(double delta)
    {
        if(_input==null)return;
        if(_input.Text.Length>0){_exampleTime=0;return;}
        _exampleTime+=delta;if(_exampleTime<5)return;_exampleTime%=5;
        _input.PlaceholderText=RandomDrinkExample();
    }
    private void Submit(){if(_input!=null&&SCP294Potion.NormalizeDrinkName(_input.Text) is{Length:>0} text)Finish(text);}
    private void Finish(string? text){if(!_completion.TrySetResult(text))return;QueueFree();}
    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if(@event is InputEventKey{Pressed:true,Echo:false,Keycode:Key.Escape}){Finish(null);GetViewport().SetInputAsHandled();}
    }
    public override void _ExitTree(){_completion.TrySetResult(null);}
}
