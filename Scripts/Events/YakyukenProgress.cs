using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib;
using STS2RitsuLib.RunData;

namespace OtherworldTreasures.Scripts.Events;

// 野球拳的出拳进度：存在局内数据里，跨战斗、跨卡牌克隆都读同一份。
// 等级不在这里，等级就是这张牌自己的升级等级（LV1～LV9 是同一张牌不断升级）。
public sealed class YakyukenSave
{
    public Dictionary<string, int> Punches { get; set; } = new();
}

internal static class YakyukenProgress
{
    internal const int BasePunchRequirement = 6;
    internal const int PunchRequirementStep = 2;

    private static PlayerRunSavedData<YakyukenSave> _slot = null!;

    internal static void Initialize()
    {
        using (RitsuLibFramework.BeginModDataRegistration(Entry.ModId))
            _slot = RitsuLibFramework.GetRunSavedDataStore(Entry.ModId).RegisterPerPlayer(
                "yakyuken_v1", () => new YakyukenSave(),
                options: new RunSavedDataOptions { WritePolicy = RunSavedDataWritePolicy.WhenSet });
    }

    // 这一级要打多少拳才升到下一级
    internal static int Needed(int level) => BasePunchRequirement + (level - 1) * PunchRequirementStep;

    private static bool InRun(Player? player) => player?.RunState is MegaCrit.Sts2.Core.Runs.RunState;

    internal static int Punches(Player? player, CardModel card) =>
        InRun(player) ? _slot.Get(player!).Punches.GetValueOrDefault(card.Id.Entry, 0) : 0;

    // 出一拳；打满这一级所需的拳数就返回 true，并把计数清零。
    internal static bool AddPunch(Player player, CardModel card, int needed)
    {
        int punches = Punches(player, card) + 1;
        bool full = punches >= needed;
        if (full)
        {
            punches = 0;
        }

        _slot.Modify(player, data => data.Punches[card.Id.Entry] = punches);
        return full;
    }

    internal static void Reset(Player player, CardModel card) =>
        _slot.Modify(player, data => data.Punches[card.Id.Entry] = 0);
}
