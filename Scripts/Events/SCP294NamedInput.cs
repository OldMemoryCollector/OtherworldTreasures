using System.Text;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;
using OtherworldTreasures.Scripts.Potions;
using OtherworldTreasures.Scripts.UI;
namespace OtherworldTreasures.Scripts.Events;
public sealed partial class Scp294
{
    private NamedDrinkResult? _namedDrink;
    private bool _claiming;
    private void ResetNamedBrew(){_namedDrink=null;_claiming=false;}
    private static string Text(string zh,string en,string jp)=>Scp294InputText.Text(zh,en,jp);
    private static LocString Literal(string text){var loc=new LocString("events","OTHERWORLD_TREASURES_EVENT_SCP294.pages.CHOICE_OPTION.description");loc.Add("Details",text);return loc;}
    private EventOption NamedOption(string key,string name,string detail,Func<Task>? callback,IEnumerable<IHoverTip>? tips=null)
    {
        var title=new LocString("events","OTHERWORLD_TREASURES_EVENT_SCP294.pages.CHOICE_OPTION.title");title.Add("Option",SCP294Potion.FormatPotionText(name,LocManager.Instance.Language));
        return new EventOption(this,callback,title,Literal(SCP294Potion.FormatPotionText(detail,LocManager.Instance.Language)),ModOptionKey("NAMED",key),tips?.ToArray()??Array.Empty<IHoverTip>());
    }
    private Task StartNamedBrew()=>RequestNamedDrink();
    private async Task RequestNamedDrink()
    {
        if(_starting||_finished||IsFinished||_paid||Owner.Gold<UseCost)return;
        _starting=true;
        try
        {
            SetEventState(Literal(Text("在键盘上输入你想要的饮品。","Type the drink you would like.","欲しい飲み物を入力してください。")),
                new[]{NamedOption("INPUT_WAIT",Text("正在输入…","Entering drink…","入力中…"),"",null)});
            var sync=RunManager.Instance.PlayerChoiceSynchronizer;
            uint choiceId=sync.ReserveChoiceId(Owner);
            string? input;
            if(RunManager.Instance.NetService.Type!=NetGameType.Replay&&(LocalContext.IsMe(Owner)||RunManager.Instance.NetService.Type==NetGameType.Singleplayer))
            {
                if(Node is not Godot.Control parent)throw new InvalidOperationException("SCP-294 input requires the event layout.");
                input=await Scp294DrinkInput.Open(parent,_namedDrink?.Request.Name??"").Completion;
                sync.SyncLocalChoice(Owner,choiceId,PlayerChoiceResult.FromIndexes(EncodeDrinkInput(input)));
            }
            else input=DecodeDrinkInput((await sync.WaitForRemoteChoice(Owner,choiceId)).AsIndexes());
            if(_finished||IsFinished)return;
            if(string.IsNullOrWhiteSpace(input))
            {
                SetEventState(PageDescription("INITIAL"),GenerateInitialOptions());
                return;
            }
            if(Owner.Gold<UseCost){SetEventState(PageDescription("INITIAL"),GenerateInitialOptions());return;}
            await PlayerCmd.LoseGold(UseCost,Owner,GoldLossType.Spent);_paid=true;
            PrepareNamedDrink(input);
        }
        finally{_starting=false;}
    }
    internal static List<int> EncodeDrinkInput(string? value)=>value==null?new List<int>():Encoding.UTF8.GetBytes(SCP294Potion.NormalizeDrinkName(value)).Select(b=>(int)b).ToList();
    internal static string? DecodeDrinkInput(IReadOnlyList<int> bytes)
    {
        if(bytes.Count==0)return null;
        if(bytes.Count>SCP294Potion.DrinkInputLimit*4||bytes.Any(i=>i is <0 or >255))throw new ArgumentException("Invalid drink text choice");
        return SCP294Potion.NormalizeDrinkName(new UTF8Encoding(false,true).GetString(bytes.Select(i=>(byte)i).ToArray()));
    }
    private void PrepareNamedDrink(string input)
    {
        _namedDrink=SCP294Potion.BrewNamed(Rng,input);_pendingBrew=_namedDrink.Potion;_pendingBrew.Owner=Owner;
        ShowNamedDrink();
    }
    private void ShowNamedDrink()
    {
        if(_namedDrink==null||_pendingBrew==null)return;
        string key=ModOptionKey("NAMED","CLAIM");_brewPreviews[key]=_pendingBrew;
        var choices=new List<EventOption>{NamedOption("CLAIM",Text("领取 · ","Take · ","受け取る・")+SCP294Potion.BrewNameOf(_pendingBrew),SCP294Potion.SummaryOf(_pendingBrew),ClaimBrew,[BrewPreviewHoverTip(_pendingBrew,key)])};
        choices.Add(Leave());
        string text=Text("出口滑出了", "The machine serves ","出口から出てきたのは ")+"[gold]"+SCP294Potion.BrewNameOf(_pendingBrew)+"[/gold]。";
        SetEventState(Literal(text),choices);
    }
}
