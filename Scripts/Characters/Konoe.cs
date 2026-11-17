using Godot;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Cards;
using OtherworldTreasures.Scripts.Konoe;
using MegaCrit.Sts2.Core.Models.PotionPools;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Nodes.Combat;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Characters;
using STS2RitsuLib.Scaffolding.Godot;
using STS2RitsuLib.Scaffolding.Visuals.StateMachine;

namespace OtherworldTreasures.Scripts.Characters;

[RegisterCharacter]
public sealed class Konoe : ModCharacterTemplate<KonoeCardPool, IroncladRelicPool, IroncladPotionPool>
{
    public override CharacterGender Gender => CharacterGender.Feminine;
    public override int StartingHp => 68;
    public override int StartingGold => 99;
    public override Color NameColor => KonoeTheme.Primary;
    public override Color EnergyLabelOutlineColor => KonoeTheme.Outline;
    public override Color MapDrawingColor => KonoeTheme.Primary;
    public override bool RequiresEpochAndTimeline => false;
    // Keep registration for existing saves, but do not offer the unfinished character in releases.
    public override bool HideFromVanillaCharacterSelect => !DevelopmentContent.Nine;
    public override bool AllowInVanillaRandomCharacterSelect => false;
    public override bool HideInCardLibraryCompendium => !DevelopmentContent.Nine;

    // Four Konoe strikes, four Konoe defends, and the two existing element starters.
    protected override IEnumerable<CardModel> LocalStartingDeck => [
        ModelDb.Card<KonoeStrike>(), ModelDb.Card<KonoeStrike>(), ModelDb.Card<KonoeStrike>(), ModelDb.Card<KonoeStrike>(),
        ModelDb.Card<KonoeDefend>(), ModelDb.Card<KonoeDefend>(), ModelDb.Card<KonoeDefend>(), ModelDb.Card<KonoeDefend>(),
        ModelDb.Card<KonoeRandomElement>(), ModelDb.Card<KonoeBlackStone>()
    ];
    protected override IEnumerable<RelicModel> LocalStartingRelics => [ModelDb.Relic<KonoeSagesBook>()];

    private static CharacterAssetProfile? _assetProfile;

    public override CharacterAssetProfile AssetProfile => _assetProfile ??= CharacterAssetProfiles.Merge(
        CharacterAssetProfiles.Ironclad(),
        new(
            Scenes: new(VisualsPath: "res://OtherworldTreasures/scenes/characters/nine/visuals.tscn"),
            Ui: new(
                IconTexturePath: "res://OtherworldTreasures/images/characters/nine/portrait.png",
                IconPath: "res://OtherworldTreasures/scenes/characters/nine/icon.tscn",
                CharacterSelectBgPath: "res://OtherworldTreasures/scenes/characters/nine/select_background.tscn",
                CharacterSelectIconPath: "res://OtherworldTreasures/images/characters/nine/select.png",
                CharacterSelectLockedIconPath: "res://OtherworldTreasures/images/characters/nine/select.png",
                MapMarkerPath: "res://OtherworldTreasures/images/characters/nine/portrait.png"),
            VisualCues: KonoeAnimations.Create()));

    public override float AttackAnimDelay => KonoeAttackVfx.EmissionDelay(KonoeAttackVfx.WindPath);
    public override float CastAnimDelay => 0.195f;

    protected override NCreatureVisuals? TryCreateCreatureVisuals()
    {
        KonoeAttackVfx.Preload();
        return RitsuGodotNodeFactories.CreateFromScenePath<NCreatureVisuals>(AssetProfile.Scenes!.VisualsPath!);
    }

    protected override ModAnimStateMachine? SetupCustomCombatAnimationStateMachine(Node visualsRoot, CharacterModel character)
    {
        var builder = ModAnimStateMachineBuilder.Create()
            .AddState("idle", loop: true).AsInitial().Done()
            .AddState("attack").WithNext("idle").Done()
            .AddState("water_attack").WithNext("idle").Done()
            .AddState("wind_attack").WithNext("idle").Done()
            .AddState("fire_attack").WithNext("idle").Done()
            .AddState("black_stone_attack").WithNext("idle").Done()
            .AddState("cast").WithNext("idle").Done()
            .AddState("hit").WithNext("idle").Done()
            .AddState("dead").Done()
            .AddState("relaxed").WithNext("idle").Done()
            .AddState("heavy").WithNext("idle").Done()
            .AddState("block").WithNext("idle").Done();
        builder.AddAnyState("Idle", "idle");
        builder.AddAnyState("Attack", "attack");
        builder.AddAnyState("WaterAttack", "water_attack");
        builder.AddAnyState("WindAttack", "wind_attack");
        builder.AddAnyState("FireAttack", "fire_attack");
        builder.AddAnyState("BlackStoneAttack", "black_stone_attack");
        builder.AddAnyState("Cast", "cast");
        builder.AddAnyState("PowerUp", "cast");
        builder.AddAnyState("Hit", "hit");
        builder.AddAnyState("Dead", "dead");
        builder.AddAnyState("Relaxed", "relaxed");
        builder.AddAnyState("heavyAttack", "heavy");
        builder.AddAnyState("Block", "block");
        return builder.BuildForVisualsRoot(visualsRoot, character);
    }

    public override List<string> GetArchitectAttackVfx() => ModelDb.Character<Ironclad>().GetArchitectAttackVfx();
}
