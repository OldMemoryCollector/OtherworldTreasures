using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using OtherworldTreasures.Scripts.Poker;
namespace OtherworldTreasures.Scripts.UI;

internal static class PokerVfx
{
    internal static async Task Fly(Creature owner,Creature target,PokerFace face)
    {
        if(NCombatRoom.Instance is not{} room)return;
        var from=room.GetCreatureNode(owner);var to=room.GetCreatureNode(target);if(from==null||to==null)return;
        var flight=new PokerProjectile(face,from.VfxSpawnPosition,to.VfxSpawnPosition);room.CombatVfxContainer.AddChild(flight);await flight.Completion;
    }
}
internal partial class PokerProjectile(PokerFace face,Vector2 from,Vector2 to):Node2D
{
    private readonly TaskCompletionSource _done=new();
    private float _age;
    internal Task Completion=>_done.Task;
    public override void _Ready()
    {var sprite=new Sprite2D{Texture=GD.Load<Texture2D>(face.ImagePath),Scale=Vector2.One*.14f};AddChild(sprite);GlobalPosition=from;ZIndex=0;}
    public override void _Process(double delta)
    {
        _age+=(float)delta;float t=Math.Clamp(_age/.13f,0,1);
        GlobalPosition=from.Lerp(to,t)+new Vector2(0,-Mathf.Sin(Mathf.Pi*t)*22);Rotation=t*Mathf.Tau;
        if(t>=1){_done.TrySetResult();QueueFree();}
    }
    public override void _ExitTree()=>_done.TrySetResult();
}
