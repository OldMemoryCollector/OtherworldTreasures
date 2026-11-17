using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;

namespace OtherworldTreasures.Scripts.Konoe;

// One current character palette. Keep using it until the owner changes it.
internal static class KonoeTheme
{
    internal static readonly Color Primary = new("F89897");
    internal static Color Outline => Primary.Darkened(0.6f);
}

// Complete character pool: all five authored rarities belong to Konoe.
public sealed class KonoeCardPool : TypeListCardPoolModel
{
    public override string Title => "konoe";
    public override string EnergyColorName => "ironclad";
    public override string CardFrameMaterialPath => "card_frame_red";
    public override Color DeckEntryCardColor => KonoeTheme.Primary;
    public override Color EnergyOutlineColor => KonoeTheme.Outline;
    public override bool IsColorless => false;
    private Material? _frameMaterial;
    public override Material? PoolFrameMaterial => _frameMaterial ??= MaterialUtils.CreateReplaceHueShaderMaterial(
        KonoeTheme.Primary.R, KonoeTheme.Primary.G, KonoeTheme.Primary.B);
}
