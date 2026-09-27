using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;

namespace OtherworldTreasures.Scripts.Monsters;

// 被【桃太郎丸子】驯服的怪物：本局游戏内永久随从。
// - 记录跨战斗保存（怪物 ModelId + 最大生命 + 当前生命），随遗物的 SavedProperty 存进 run 存档。
// - 战斗中的活动实例注册在这里，意图驱动/禁疗 Harmony 前缀/死亡移除都查这张表。
// 记录编码： "MONSTER.entry|80|72.5"，用 | 分隔，decimal 走 invariant 防止欧洲小数点炸档。
public static class TamedPets
{
    public const decimal MaxTameableHp = 80m;

    // 可驯服的血线：当前生命不超过这个值（含）
    public const decimal TameableCurrentHp = 10m;

    // 本局已驯服怪物的持久记录（读档后由遗物用存档值覆盖）
    public static List<TamedPetRecord> Records { get; private set; } = new();

    // 本场战斗的活动随从：creature -> 对应记录
    private static readonly Dictionary<Creature, TamedPetRecord> s_active = new();

    public static bool IsTamed(Creature creature) => s_active.ContainsKey(creature);

    public static IEnumerable<Creature> ActiveCreatures => s_active.Keys.ToList();

    // 活动实例登记（战斗开始重召 / 当场驯服时）
    public static void Register(Creature creature, TamedPetRecord record)
    {
        s_active[creature] = record;
    }

    // 死亡：移除活动实例 + 永久记录；返回被删掉的记录
    public static TamedPetRecord? NotifyDeath(Creature creature)
    {
        if (!s_active.Remove(creature, out var record))
        {
            return null;
        }
        Records.Remove(record);
        return record;
    }

    // 战斗结束：把存活随从的当前血量写回记录
    public static void SnapshotAndClear()
    {
        foreach (var (creature, record) in s_active)
        {
            if (creature.IsAlive)
            {
                record.SyncCurrentHp(creature.CurrentHp);
            }
            // 死亡的在 AfterDeath 已从 Records 移除，这里不处理
        }
        s_active.Clear();
    }

    public static void ResetRun()
    {
        s_active.Clear();
        Records = new List<TamedPetRecord>();
    }

    // 读档恢复。存档里是一条字符串（[SavedProperty] 不支持 List<string>，会抛序列化异常），
    // 多条记录用 ';' 分隔，单条内部用 '|' 分隔
    public static void LoadRecords(string? encoded)
    {
        var records = new List<TamedPetRecord>();
        if (!string.IsNullOrWhiteSpace(encoded))
        {
            foreach (var line in encoded.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                if (TamedPetRecord.TryDecode(line, out var record) && record != null)
                {
                    records.Add(record);
                }
            }
        }
        Records = records;
        s_active.Clear();
    }

    // 写档
    public static string EncodeRecords()
    {
        return string.Join(';', Records.Select(r => r.Encode()));
    }

    // 能否用丸子驯服：敌方、存活、非首领房间、最大生命 <= 80、当前生命 <= 10
    // （"效果写在玩家方"的招式不再限制捕捉 —— 随从执行时会自动转成 6 点格挡）
    public static bool CanTame(Creature target, Creature playerCreature)
    {
        // 首领不可收服。Boss 的血量本来就超过上限，这里再显式挡一层，
        // 免得以后出现"低血 Boss"或血量被削后变成可收服
        var room = playerCreature?.Player?.RunState?.CurrentRoom;
        if (room != null && room.RoomType == RoomType.Boss)
        {
            return false;
        }

        return target != null
            && target.IsAlive
            && target.Side != playerCreature.Side
            && target.MaxHp <= MaxTameableHp
            && target.CurrentHp <= TameableCurrentHp;
    }

    // === 表现层：朝向 / 站位 / 血条 ===

    // 向日葵（植物召唤物）的站位偏移：玩家位置 + (100, 120)。随从沿用这一水平线。
    private static readonly Vector2 LayoutOffset = new(100f, 120f);

    // 按"捕捉顺序"返回某玩家在场的随从：越早捕捉的越靠前（越靠近敌人）。
    // 位置规则来自需求：新捕捉的随从取代原本最靠玩家的位置，原有随从依次前移。
    public static List<Creature> ActiveInTameOrder(Player owner)
    {
        var result = new List<Creature>();
        foreach (var record in Records)
        {
            foreach (var pair in s_active)
            {
                if (ReferenceEquals(pair.Value, record) && pair.Key.PetOwner == owner && pair.Key.IsAlive)
                {
                    result.Add(pair.Key);
                    break;
                }
            }
        }
        return result;
    }

    // 随从加入战斗后调用（挂在 NCombatRoom.AddCreature 之后）：
    // 1) 朝向：敌方骨骼默认朝左（面向玩家），移到玩家侧后水平镜像，让它面向敌人
    // 2) 站位：与向日葵同一条水平线；越早捕捉的越靠前（越靠近敌人），互不重叠
    // 3) 血条：原版 AddCreature 会对所有宠物 ToggleIsInteractable(false)，血条随之隐藏，这里恢复
    public static void ApplyLayout()
    {
        var room = NCombatRoom.Instance;
        if (room == null)
        {
            return;
        }

        var owners = s_active.Keys
            .Where(c => c.PetOwner != null)
            .Select(c => c.PetOwner!)
            .Distinct()
            .ToList();

        foreach (var owner in owners)
        {
            var playerNode = room.GetCreatureNode(owner.Creature);
            if (playerNode == null)
            {
                continue;
            }

            var basePos = playerNode.Position + LayoutOffset;
            var ordered = ActiveInTameOrder(owner); // [0] = 最早捕捉

            // 从"最新捕捉"（最靠近玩家）开始往敌人方向铺，这样最早捕捉的落在最前面；
            // 新随从加入时就会自然占据原最靠玩家的位置，老随从整体前移
            var offsetX = 0f;
            for (var i = ordered.Count - 1; i >= 0; i--)
            {
                var node = room.GetCreatureNode(ordered[i]);
                if (node == null)
                {
                    continue;
                }

                FlipToFaceEnemy(node);

                // 血条（ToggleIsInteractable 同时控制血条可见性）
                node.ToggleIsInteractable(true);

                // 先按体型调整视觉大小，再用"缩放后的实际宽度"来排位置
                ApplyBodyScale(ordered[i]);

                // 位置：横向错开。Bounds 控件本身不随缩放变化（原版只改 Hitbox），
                // 所以必须乘上 Visuals.Scale 才是玩家真正看到的宽度，否则缩小的随从之间会留一大段空白
                node.Position = basePos + new Vector2(offsetX, 0f);
                var displayWidth = Math.Abs(node.Visuals.Scale.X) * node.Visuals.Bounds.Size.X;
                offsetX += Math.Max(displayWidth, 80f) + 24f;
            }
        }
    }

    // 让随从面向敌人：用 Spine 骨架自身的 scale_x 做水平镜像
    // （节点 Scale 会被原版的缩放 tween 重置，不能用来翻转）。
    // 注意：这里刻意走 native 调用，不经过 MegaSkeleton 包装 —— 那种 wrapper 是瞬态对象，
    // 泄漏到 finalizer 线程会与主线程竞争 Spine 的信号连接表，造成随机的 UI 异常（偶现 bug）。
    private static void FlipToFaceEnemy(NCreature node)
    {
        var spine = node.Visuals.SpineBody;
        if (spine == null)
        {
            return;
        }
        var skeleton = spine.BoundObject.Call("get_skeleton").AsGodotObject();
        skeleton?.Call("set_scale_x", -1f);
    }

    // 体型过大的随从（比如蛙寄生虫）自动缩小到与玩家/植物协调的尺寸。
    // 这是纯视觉缩放（改 Visuals.Scale），不是任何 buff，不影响数值。
    // 基准取"玩家角色的视觉宽度"，随从最多到它的 MaxPetWidthRatio 倍。
    private const float MaxPetWidthRatio = 1.4f;
    private const float FallbackPlayerWidth = 180f;

    public static void ApplyBodyScale(Creature pet)
    {
        try
        {
            var room = NCombatRoom.Instance;
            var node = room?.GetCreatureNode(pet);
            if (room == null || node == null || !pet.IsAlive)
            {
                return;
            }

            var owner = pet.PetOwner;
            var playerNode = owner == null ? null : room.GetCreatureNode(owner.Creature);
            var playerWidth = playerNode?.Visuals.Bounds.Size.X ?? 0f;
            if (playerWidth <= 1f)
            {
                playerWidth = FallbackPlayerWidth;
            }

            var maxWidth = playerWidth * MaxPetWidthRatio;
            var petWidth = node.Visuals.Bounds.Size.X;
            if (petWidth <= maxWidth || petWidth <= 1f)
            {
                return;
            }

            var scale = maxWidth / petWidth;
            node.SetScaleAndHue(scale, 0f);
            // 缩放后要重算 Bounds，否则血条/意图还停在"没缩小"时的位置上（原版 OstyScaleToSize 同理）
            s_updateBoundsMethod?.Invoke(node, new object[] { node.Visuals });
            Entry.Logger.Info($"[MomotaroDumplings] {pet.Monster?.Id.Entry} 体型过大（宽 {petWidth:0} > {maxWidth:0}），视觉缩放到 {scale:0.##}");
        }
        catch (Exception e)
        {
            Entry.Logger.Info($"[MomotaroDumplings] 随从体型缩放失败（已忽略）：{e.Message}");
        }
    }

    // NCreature.UpdateBounds(Node) 是 private，用反射调用（原版缩放流程里也是它负责重算血条位置）
    private static readonly MethodInfo? s_updateBoundsMethod =
        typeof(NCreature).GetMethod("UpdateBounds",
            BindingFlags.NonPublic | BindingFlags.Instance, null,
            new[] { typeof(Node) }, null);
}

// 单只驯服怪物的跨战斗记录
public class TamedPetRecord
{
    public ModelId MonsterId { get; }
    public decimal MaxHp { get; private set; }
    public decimal CurrentHp { get; private set; }

    public TamedPetRecord(ModelId monsterId, decimal maxHp, decimal currentHp)
    {
        MonsterId = monsterId;
        MaxHp = maxHp;
        CurrentHp = Math.Min(currentHp, maxHp);
    }

    public void SyncCurrentHp(decimal currentHp)
    {
        CurrentHp = Math.Max(1m, Math.Min(currentHp, MaxHp));
    }

    public string Encode()
    {
        return string.Join('|',
            MonsterId.ToString(),
            MaxHp.ToString(CultureInfo.InvariantCulture),
            CurrentHp.ToString(CultureInfo.InvariantCulture));
    }

    public static bool TryDecode(string text, out TamedPetRecord? record)
    {
        record = null;
        var parts = text.Split('|');
        if (parts.Length != 3)
        {
            return false;
        }
        if (!decimal.TryParse(parts[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var maxHp)
            || !decimal.TryParse(parts[2], NumberStyles.Number, CultureInfo.InvariantCulture, out var currentHp))
        {
            return false;
        }
        try
        {
            record = new TamedPetRecord(ModelId.Deserialize(parts[0]), maxHp, currentHp);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
