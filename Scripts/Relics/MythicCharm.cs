using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.RelicPools;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Relics;

// 神话护身符（来自《泰拉瑞亚》Charm of Myths）：
// 战斗开始时获得 1 层缓冲 + 1 层人工制品；每回合开始恢复 1 点生命值，
// 当前生命值低于上限一半时改为恢复 2 点。
[RegisterRelic(typeof(SharedRelicPool))]
public class MythicCharm : ModRelicTemplate
{
    private const decimal InitialBuffer = 1m;
    private const decimal InitialArtifact = 1m;
    private const int HealPerTurn = 1;
    private const int LowHealthHealPerTurn = 2;

    // 由先古之民给予，使用 Ancient 稀有度
    public override RelicRarity Rarity => RelicRarity.Ancient;

    // 遗物图片资源（IconPath=小图标85x85，IconOutlinePath=轮廓85x85，BigIconPath=大图标256x256）
    public override RelicAssetProfile AssetProfile => new(
        IconPath: "res://OtherworldTreasures/images/relics/Mythic_Charm.png",
        IconOutlinePath: "res://OtherworldTreasures/images/relics/Mythic_Charm.png",
        BigIconPath: "res://OtherworldTreasures/images/relics/Mythic_Charm.png"
    );

    // 战斗开始：1 层缓冲 + 1 层人工制品
    public override async Task BeforeCombatStart()
    {
        var creature = Owner?.Creature;
        if (creature != null)
        {
            Flash();
            var ctx = new ThrowingPlayerChoiceContext();
            await PowerCmd.Apply<BufferPower>(ctx, creature, InitialBuffer, creature, null);
            await PowerCmd.Apply<ArtifactPower>(ctx, creature, InitialArtifact, creature, null);
        }
        await base.BeforeCombatStart();
    }

    // 每回合开始恢复生命：生命值低于上限一半时恢复 2 点，否则恢复 1 点（满血时不触发治疗特效）
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == Owner && player.Creature is { } creature && creature.CurrentHp < creature.MaxHp)
        {
            bool lowHealth = creature.CurrentHp * 2 < creature.MaxHp;
            await CreatureCmd.Heal(creature, lowHealth ? LowHealthHealPerTurn : HealPerTurn);
        }
        await base.AfterPlayerTurnStart(choiceContext, player);
    }
}
