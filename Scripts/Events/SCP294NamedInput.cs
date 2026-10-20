using System.Text;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Gold;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using OtherworldTreasures.Scripts.Potions;
using OtherworldTreasures.Scripts.UI;
namespace OtherworldTreasures.Scripts.Events;
public sealed partial class Scp294
{
    private NamedDrinkResult? _namedDrink;
    private bool _namedRedoUsed;
    private string _namedIncident="";
    private bool _claiming;
    private void ResetNamedBrew(){_namedDrink=null;_namedRedoUsed=false;_namedIncident="";_claiming=false;}
    private static string Text(string zh,string en,string jp)=>Scp294InputText.Text(zh,en,jp);
    private static LocString Literal(string text){var loc=new LocString("events","OTHERWORLD_TREASURES_EVENT_SCP294.pages.CHOICE_OPTION.description");loc.Add("Details",text);return loc;}
    private EventOption NamedOption(string key,string name,string detail,Func<Task>? callback,IEnumerable<IHoverTip>? tips=null)
    {
        var title=new LocString("events","OTHERWORLD_TREASURES_EVENT_SCP294.pages.CHOICE_OPTION.title");title.Add("Option",SCP294Potion.FormatPotionText(name,LocManager.Instance.Language));
        return new EventOption(this,callback,title,Literal(SCP294Potion.FormatPotionText(detail,LocManager.Instance.Language)),ModOptionKey("NAMED",key),tips?.ToArray()??Array.Empty<IHoverTip>());
    }
    private Task StartNamedBrew()=>RequestNamedDrink(false);
    private Task RedoNamedBrew()=>RequestNamedDrink(true);
    private async Task RequestNamedDrink(bool freeRedo)
    {
        if(_starting||_finished||IsFinished||(!freeRedo&&(_paid||Owner.Gold<UseCost))||(freeRedo&&(_namedRedoUsed||_namedDrink?.Accident!=BrewAccident.FreeRedo)))return;
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
                input=await Scp294DrinkInput.Open(parent,_namedDrink?.Request.Name??"",freeRedo).Completion;
                sync.SyncLocalChoice(Owner,choiceId,PlayerChoiceResult.FromIndexes(EncodeDrinkInput(input)));
            }
            else input=DecodeDrinkInput((await sync.WaitForRemoteChoice(Owner,choiceId)).AsIndexes());
            if(_finished||IsFinished)return;
            if(string.IsNullOrWhiteSpace(input))
            {
                if(freeRedo)ShowNamedDrink();else SetEventState(PageDescription("INITIAL"),GenerateInitialOptions());
                return;
            }
            if(freeRedo)_namedRedoUsed=true;
            else
            {
                if(Owner.Gold<UseCost){SetEventState(PageDescription("INITIAL"),GenerateInitialOptions());return;}
                await PlayerCmd.LoseGold(UseCost,Owner,GoldLossType.Spent);_paid=true;
            }
            await PrepareNamedDrink(input);
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
    private async Task PrepareNamedDrink(string input)
    {
        _namedDrink=SCP294Potion.BrewNamed(Rng,input,!_namedRedoUsed);_pendingBrew=_namedDrink.Potion;_pendingBrew.Owner=Owner;
        await ApplyNamedAccident(_namedDrink);ShowNamedDrink();
    }
    private async Task ApplyNamedAccident(NamedDrinkResult result)
    {
        _namedIncident="";
        switch(result.Accident)
        {
            case BrewAccident.HpLoss:
                int damage=Math.Min(result.Damage,Math.Max(0,(int)Owner.Creature.CurrentHp-1));
                if(damage>0)await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(),Owner.Creature,damage,ValueProp.Unblockable|ValueProp.Unpowered,null,null);
                _namedIncident=Text($"机器喷出了滚烫的液体。你失去了{damage}点生命。",$"The machine splashed scalding liquid. Lose {damage} HP.",$"熱い液体が飛び散り、HPを{damage}失った。");break;
            case BrewAccident.Curse:
                await CardPileCmd.AddCurseToDeck<Writhe>(Owner);
                _namedIncident=Text("杯底压着一张诅咒。你获得了扭动。","A curse rests under the cup. Obtain Writhe.","カップの下に呪い。身もだえを獲得。");break;
            case BrewAccident.FreeRedo:
                _namedIncident=Text("杯底印着一张赠券：可免费重做一次。","A coupon under the cup grants one free remake.","カップの下の券で、一度だけ無料で作り直せる。");break;
        }
    }
    private IEnumerable<EventOption> NamedRedoOptions()
    {
        if(_namedDrink?.Accident==BrewAccident.FreeRedo&&!_namedRedoUsed)
            yield return NamedOption("FREE_REDO",Text("免费重做","Free remake","無料で作り直す"),Text("重新输入饮品名，替换这一杯。仅限一次。","Enter another name and replace this drink, once.","別の名前を入力し、今回の飲み物を一度だけ交換。"),RedoNamedBrew);
    }
    private void ShowNamedDrink()
    {
        if(_namedDrink==null||_pendingBrew==null)return;
        string key=ModOptionKey("NAMED","CLAIM");_brewPreviews[key]=_pendingBrew;
        var choices=new List<EventOption>{NamedOption("CLAIM",Text("领取 · ","Take · ","受け取る・")+SCP294Potion.BrewNameOf(_pendingBrew),SCP294Potion.SummaryOf(_pendingBrew),ClaimBrew,[BrewPreviewHoverTip(_pendingBrew,key)])};
        choices.AddRange(NamedRedoOptions());choices.Add(Leave());
        string text=Text("出口滑出了一杯", "The machine serves ","出口から出てきたのは ")+"[gold]"+SCP294Potion.BrewNameOf(_pendingBrew)+"[/gold]。";
        if(_namedIncident.Length>0)text+="\n"+SCP294Potion.FormatPotionText(_namedIncident,LocManager.Instance.Language);
        SetEventState(Literal(text),choices);
    }
}
