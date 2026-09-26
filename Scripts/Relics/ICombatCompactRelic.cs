namespace OtherworldTreasures.Scripts.Relics;

// 战斗中"界面精简"遗物标记：战斗进行中（CombatManager.IsInProgress）隐藏额外悬浮提示
// （AdditionalHoverTips，如篝火注火、地图穿越等战斗中用不到的机制说明）。
// 是否同时把主描述换成 relics.json 的 {Id.Entry}.combatDescription 由 UseCompactDescriptionInCombat 决定：
//   - true（默认）：主描述也换成战斗简描述，没配 combatDescription 时仍用 .description；
//   - false：主描述保持完整 .description（遗物本身介绍已足够简洁时使用，如惊奇套牌）。
// 战斗外（地图、篝火、商店、图鉴、奖励三选一等）完整描述与全部提示保持不变。
// 顶栏悬停框与点击后的遗物详情大窗行为一致，因为改的是 RelicModel 取文本/提示的源头。
public interface ICombatCompactRelic
{
    bool UseCompactDescriptionInCombat => true;
}
