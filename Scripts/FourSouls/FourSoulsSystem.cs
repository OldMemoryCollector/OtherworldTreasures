using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using OtherworldTreasures.Scripts.Powers;
using OtherworldTreasures.Scripts.Relics;

namespace OtherworldTreasures.Scripts.FourSouls;

/// <summary>
/// 四魂之玉的核心规则：
/// - 按「当前层 + 玩家持有碎片数」定档位，为每层地图上的普通怪/精英房间各 roll 一份四魂集合
/// - 进战斗时按「优先分散」把该房间的四魂分给具体怪物
/// - 玩家侧 buff 按各魂数量缩放；怪物侧 buff 按层数缩放
/// - roll 结果不入档：用 (run 种子, 层号, 锁定碎片数) 确定性重算，读档后得到同一份结果
/// </summary>
internal static class FourSoulsSystem
{
    // 一间房到底有没有四魂：四魂怪太多会铺满整张地图，这里给一半的概率
    private const float RoomSoulChance = 0.5f;

    // 档位概率：1魂 / 2魂 / 3魂（下标 = tier - 1）
    private static readonly float[][] SoulCountChances =
    {
        new[] { 0.90f, 0.10f, 0.00f }, // 1 层（无门槛）：1魂 90% / 2魂 10%
        new[] { 0.65f, 0.30f, 0.05f }, // 2 层（碎片≥3）：1魂 65% / 2魂 30% / 3魂 5%
        new[] { 0.40f, 0.50f, 0.10f }, // 3 层（碎片≥9）：1魂 40% / 2魂 50% / 3魂 10%
    };

    // === 遗物读写 ===

    /// <summary>取 run 里玩家持有的四魂之玉碎片（没有则 null）。</summary>
    internal static FourSoulsFragment? FindRelic(IRunState runState)
        => runState.Players.SelectMany(p => p.Relics).OfType<FourSoulsFragment>().FirstOrDefault();

    /// <summary>四魂系统当前是否生效：持有碎片且已开启。</summary>
    internal static bool IsActive(IRunState runState)
        => FindRelic(runState) is { Enabled: true };

    // === 档位 ===

    /// <summary>按层数与进入该层时的碎片数决定概率档（含退化规则）。</summary>
    internal static int TierFor(int actFloor, int fragmentCount)
    {
        if (actFloor >= 3)
        {
            if (fragmentCount >= 9) return 3;
            if (fragmentCount >= 3) return 2;
            return 1;
        }
        if (actFloor == 2)
        {
            return fragmentCount >= 3 ? 2 : 1;
        }
        return 1;
    }

    // === 地图 roll ===

    /// <summary>
    /// 重算当前层每个普通怪/精英房间的四魂集合。
    /// 结果只依赖 (run 种子, 层号, 该层锁定的碎片数)，因此存档读回后结果一致。
    /// 未持有碎片遗物时返回空表。
    /// </summary>
    internal static Dictionary<MapCoord, SoulKind[]> DeriveRoomSouls(IRunState runState)
    {
        var result = new Dictionary<MapCoord, SoulKind[]>();
        var relic = FindRelic(runState);
        if (relic == null || runState.Map == null)
        {
            return result;
        }

        int actIndex = runState.CurrentActIndex;
        int locked = relic.GetActLock(actIndex);

        int tier = TierFor(actIndex + 1, locked);
        var rng = new Rng(runState.Rng.Seed, $"otherworld_four_souls_act_{actIndex}");

        foreach (var point in runState.Map.GetAllMapPoints())
        {
            if (point.PointType != MapPointType.Monster && point.PointType != MapPointType.Elite)
            {
                continue;
            }
            // 先决定这一间房到底有没有四魂：否则每间房都出，四魂怪会铺满整张地图
            if (rng.NextFloat() >= RoomSoulChance)
            {
                continue;
            }
            int n = RollSoulCount(rng, tier);
            if (n <= 0)
            {
                continue;
            }
            result[point.coord] = PickKinds(rng, n);
        }
        return result;
    }

    /// <summary>取当前房间（正在打的那场）的四魂集合；没有则空数组。</summary>
    internal static SoulKind[] GetCurrentRoomSouls(IRunState runState)
    {
        var coord = runState.CurrentMapPoint?.coord;
        if (coord == null || !IsActive(runState))
        {
            return Array.Empty<SoulKind>();
        }
        return DeriveRoomSouls(runState).TryGetValue(coord.Value, out var souls)
            ? souls
            : Array.Empty<SoulKind>();
    }

    /// <summary>抽这一间房的魂数（按档位的概率）。</summary>
    private static int RollSoulCount(Rng rng, int tier)
    {
        var chances = SoulCountChances[Math.Clamp(tier, 1, 3) - 1];
        float roll = rng.NextFloat();
        float acc = 0f;
        for (int i = 0; i < chances.Length; i++)
        {
            acc += chances[i];
            if (roll < acc)
            {
                return i + 1;
            }
        }
        return 1;
    }

    /// <summary>从四种魂里不重复抽 n 种。</summary>
    private static SoulKind[] PickKinds(Rng rng, int n)
    {
        var pool = new List<SoulKind>(Souls.All);
        rng.Shuffle(pool);
        return pool.Take(Math.Clamp(n, 0, pool.Count)).ToArray();
    }

    /// <summary>优先分散：把 n 片魂分给 m 名敌人，尽量每名敌人各拿一片。</summary>
    internal static SoulKind[][] Distribute(SoulKind[] souls, int enemyCount)
    {
        var result = new SoulKind[Math.Max(enemyCount, 0)][];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = Array.Empty<SoulKind>();
        }
        if (result.Length == 0 || souls.Length == 0)
        {
            return result;
        }

        var buckets = new List<SoulKind>[result.Length];
        for (int i = 0; i < result.Length; i++)
        {
            buckets[i] = new List<SoulKind>();
        }
        for (int i = 0; i < souls.Length; i++)
        {
            buckets[i % result.Length].Add(souls[i]);
        }
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = buckets[i].ToArray();
        }
        return result;
    }

    // === 战斗结算 ===

    /// <summary>Buff 数值（怪物侧按层数缩放）。</summary>
    private static int EnemyStrength(int actFloor) => actFloor + 1;
    private static int EnemyPlating(int actFloor) => actFloor * 2 + 4;
    private static int EnemyRegen(int actFloor) => actFloor * 2 + 1;

    /// <summary>战斗开始：给玩家上四魂强化，并给敌人分配并挂上各自房间的四魂。</summary>
    internal static async Task ApplyCombatStartBuffs(
        PlayerChoiceContext ctx, Player player, ICombatState combatState)
    {
        var runState = player.RunState;
        var relic = FindRelic(runState);
        if (relic == null || !relic.Enabled || player.Creature == null)
        {
            return;
        }

        // 玩家侧：只上实际强化（显示交给遗物栏的四个角标，不挂【四魂】能力）
        await ApplyToCreature(ctx, player.Creature, relic.Fragments, actFloor: 0, player: true);

        // 怪物侧：本房间的四魂集合分给具体敌人
        var souls = GetCurrentRoomSouls(runState);
        if (souls.Length == 0)
        {
            return;
        }
        int actFloor = runState.CurrentActIndex + 1;
        var enemies = combatState.Enemies.Where(e => e.IsAlive).ToList();
        var distribution = Distribute(souls, enemies.Count);

        for (int i = 0; i < enemies.Count; i++)
        {
            if (distribution[i].Length == 0)
            {
                continue;
            }
            // 把分到的四魂贴到生物上（能力描述读它），再挂上显示用能力
            FourSoulsPower.Attach(enemies[i], CountOf(distribution[i]));
            await FourSoulsPower.ApplyAndSet(ctx, enemies[i]);
            await ApplyToCreature(ctx, enemies[i], distribution[i], actFloor, player: false);
        }
    }

    /// <summary>把一组魂折成"每种几片"的计数（下标 = SoulKind）。</summary>
    internal static int[] CountOf(SoulKind[] souls)
    {
        var counts = new int[Souls.Count];
        foreach (var soul in souls)
        {
            counts[(int)soul]++;
        }
        return counts;
    }

    /// <summary>把一组魂的 buff 挂到某个生物上。</summary>
    private static async Task ApplyToCreature(
        PlayerChoiceContext ctx, Creature target, int[] counts, int actFloor, bool player)
    {
        for (int i = 0; i < Souls.Count; i++)
        {
            int n = i < counts.Length ? counts[i] : 0;
            if (n <= 0)
            {
                continue;
            }
            await ApplyOneKinds(ctx, target, (SoulKind)i, n, actFloor, player);
        }
    }

    /// <summary>把一组魂（可能同种多片）的 buff 挂到某个生物上。</summary>
    private static async Task ApplyToCreature(
        PlayerChoiceContext ctx, Creature target, SoulKind[] souls, int actFloor, bool player)
    {
        foreach (var group in souls.GroupBy(s => s))
        {
            await ApplyOneKinds(ctx, target, group.Key, group.Count(), actFloor, player);
        }
    }

    private static async Task ApplyOneKinds(
        PlayerChoiceContext ctx, Creature target, SoulKind kind, int count, int actFloor, bool player)
    {
        if (count <= 0)
        {
            return;
        }

        // 玩家侧：奇魂只给永久血上限，战斗内不重复给
        if (player && kind == SoulKind.Wonder)
        {
            return;
        }

        switch (kind)
        {
            case SoulKind.Wasteland:
                await PowerCmd.Apply<StrengthPower>(
                    ctx, target, player ? count : count * EnemyStrength(Math.Max(actFloor, 1)), target, null);
                break;
            case SoulKind.Harmony:
                await PowerCmd.Apply<PlatingPower>(
                    ctx, target, player ? count * 2 : count * EnemyPlating(Math.Max(actFloor, 1)), target, null);
                break;
            case SoulKind.Fortune:
                await PowerCmd.Apply<RegenPower>(
                    ctx, target, player ? count : count * EnemyRegen(Math.Max(actFloor, 1)), target, null);
                break;
            case SoulKind.Wonder:
                // 怪物：最大生命 +25%（同时补当前生命，等于变肉）
                for (int i = 0; i < count; i++)
                {
                    int gain = Math.Max(1, (int)Math.Ceiling(target.MaxHp * 0.25m));
                    await CreatureCmd.GainMaxHp(target, gain);
                }
                break;
        }
    }

    // === 玩家碎片收集 ===

    /// <summary>战斗胜利：把该房间的四魂全部收进玩家碎片。</summary>
    internal static async Task AwardRoomSouls(Player player)
    {
        var runState = player.RunState;
        var relic = FindRelic(runState);
        if (relic == null || !relic.Enabled)
        {
            return;
        }
        var souls = GetCurrentRoomSouls(runState);
        if (souls.Length == 0)
        {
            return;
        }

        foreach (var group in souls.GroupBy(s => s))
        {
            relic.AddFragments(group.Key, group.Count());
        }
        relic.RefreshBadge();

        // 奇魂：每获得一片血上限 +5（永久，不随开关回退）
        int wonder = souls.Count(s => s == SoulKind.Wonder);
        if (wonder > 0 && player.Creature != null)
        {
            await CreatureCmd.GainMaxHp(player.Creature, wonder * 5);
        }
    }
}
