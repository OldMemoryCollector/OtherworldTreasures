using Godot;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace OtherworldTreasures.Scripts.Konoe;

public partial class KonoeSpellCardUi : Node
{
    private CardModel? _lastModel;
    private KonoeSpellKind? _lastSpell;
    private bool _lastUpgrade;
    public override void _Process(double delta)
    {
        if (GetParent() is not NCard view || view.Model is not { } card) return;
        KonoeSpellKind spell;
        if (card is KonoeBlackStone blackStone) spell = blackStone.CurrentSpell;
        else if (card is KonoeAbyssalFormula abyss) spell = abyss.CurrentSpell;
        else return;
        if (_lastModel == card && _lastSpell == spell && _lastUpgrade == card.IsUpgraded) return;
        _lastModel = card; _lastSpell = spell; _lastUpgrade = card.IsUpgraded;
        if (card is KonoeBlackStone stone) stone.RefreshForm();
        else if (card is KonoeAbyssalFormula formula) formula.RefreshForm();
        view.UpdateVisuals(view.DisplayingPile, CardPreviewMode.Normal);
    }
}
