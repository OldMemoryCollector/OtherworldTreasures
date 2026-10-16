using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace OtherworldTreasures.Scripts.Alien;

public sealed class GhostfreakDisperseIntent : EscapeIntent
{
    protected override string IntentPrefix => "OTHERWORLD_TREASURES_SOUL_DISPERSE";
    private const string Icon = "res://OtherworldTreasures/images/powers/ghostfreak/disperse.svg";
    protected override string SpritePath => string.Empty;
    public override IEnumerable<string> AssetPaths => [Icon];
    public override Texture2D? GetTexture(IEnumerable<Creature> targets, Creature owner) => GD.Load<Texture2D>(Icon);
    public override string GetAnimation(IEnumerable<Creature> targets, Creature owner) => "escape";
}
