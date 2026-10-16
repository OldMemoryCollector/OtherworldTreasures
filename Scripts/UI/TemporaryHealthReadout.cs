using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using OtherworldTreasures.Scripts.Powers;

namespace OtherworldTreasures.Scripts.UI;

// A separate "+N" avoids presenting temporary HP as increased maximum HP.
public partial class TemporaryHealthReadout : Label
{
    private static readonly FieldInfo CreatureField = typeof(NHealthBar).GetField("_creature", BindingFlags.Instance | BindingFlags.NonPublic)!;
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeColorOverride("font_color", new Color("edc447"));
        AddThemeColorOverride("font_outline_color", Colors.Black);
        AddThemeConstantOverride("outline_size", 4);
        AddThemeFontSizeOverride("font_size", 18);
    }
    public override void _Process(double delta)
    {
        if (GetParent() is not NHealthBar bar) return;
        var creature = CreatureField.GetValue(bar) as Creature;
        int amount = creature == null ? 0 : TemporaryHealthPower.Available(creature);
        Visible = creature?.IsAlive == true && amount > 0;
        if (!Visible) return;
        Text = $"+{amount}";
        Position = new Vector2(Math.Max(0, bar.Size.X - Size.X), -24);
    }
}
