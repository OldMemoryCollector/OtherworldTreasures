using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.ValueProps;

namespace OtherworldTreasures.Scripts.Monsters;

// 玩家侧随从的承伤链（由前到后挨打）：
//   坚果墙 > 骨手(Osty) > 驯服的怪物 > 豌豆射手 > 向日葵 > 玩家自己
//
// 这套逻辑原本长在植物模型（PlantSummonBase）上，但那样只有玩家持有【戴夫的种子】时才生效。
// 为了让【桃太郎丸子】驯服的随从在没有植物时也能替玩家承伤，抽成共用的静态实现：
// 植物模型与桃太郎丸子遗物各自薄薄地调用它，承伤顺序只有一份定义。
//
// 溢出规则（关键区别）：
//   - 植物（坚果墙/豌豆射手/向日葵）接下 → 溢出**吞掉**，绝不落到玩家身上
//   - 骨手 / 驯服怪物接下 → 它们没有"吞掉溢出"的设定：溢出在**同一击内**继续传给链上的下一棒
//                           （骨手 → 驯服怪物 → 豌豆射手 → 向日葵）；没人能接时才回给玩家
//
// 注意：原版这几个 Hook 都是"链式"调用（逐个模型执行，把上一个的结果当作下一个的输入），
// 所以下面的方法必须保持幂等。
public static class PetAbsorb
{
    // "这一击由谁接下"的标记：AfterOsty 阶段要靠它区分"溢出"与"普通伤害"
    private static Creature? s_absorber;

    // 承伤优先级（数字越小越先挨打）；int.MaxValue = 不参与这条链
    private static int Priority(Creature pet) => pet.Monster switch
    {
        WallNutPlant => 0,
        Osty => 1,
        _ when TamedPets.IsTamed(pet) => 2,
        PeaShooterPlant => 3,
        SunflowerPlant => 4,
        _ => int.MaxValue,
    };

    // 按优先级挑一个活着的随从（含原版骨手、驯服的怪物）
    public static Creature? PickAbsorber(Player owner) => Pick(owner);

    // 溢出级的"下一棒"：按优先级挑排在 current 之后的下一个活着随从。
    // 骨手(1) 之后是驯服怪物(2)，再往后才是豌豆射手(3) / 向日葵(4)。
    // （植物自己接下时不会走到这里 —— 它们会把溢出吞掉。）
    private static Creature? PickNextAbsorber(Player owner, Creature current)
    {
        var currentRank = Priority(current);
        var pets = owner.PlayerCombatState?.Pets;
        if (pets == null)
        {
            return null;
        }
        Creature? best = null;
        var bestRank = int.MaxValue;
        foreach (var pet in pets)
        {
            if (pet == null || !pet.IsAlive || ReferenceEquals(pet, current))
            {
                continue;
            }
            var rank = Priority(pet);
            if (rank > currentRank && rank < bestRank)
            {
                bestRank = rank;
                best = pet;
            }
        }
        return best;
    }

    private static Creature? Pick(Player owner)
    {
        var pets = owner.PlayerCombatState?.Pets;
        if (pets == null)
        {
            return null;
        }
        Creature? best = null;
        var bestRank = int.MaxValue;
        foreach (var pet in pets)
        {
            if (pet == null || !pet.IsAlive)
            {
                continue;
            }
            var rank = Priority(pet);
            if (rank < bestRank)
            {
                bestRank = rank;
                best = pet;
            }
        }
        return best;
    }

    // 替主人承受"被强化的攻击"伤害（原版 ModifyUnblockedDamageTarget）。
    // 无论原版 DieForYouPower 在本 hook 之前还是之后跑，结果都一致：
    // 坚果墙活着就由坚果墙吃，它死了下一击才轮到骨手，然后驯服的怪物、豌豆射手/向日葵，最后才轮到玩家。
    public static Creature ModifyTarget(Player? owner, Creature target, ValueProp props)
    {
        if (owner == null || !props.IsPoweredAttack())
        {
            return target;
        }
        // 只介入"打向主人 或 主人的随从"的伤害，避免影响其它目标（例如玩家打敌人）
        if (target != owner.Creature && target.PetOwner != owner)
        {
            return target;
        }

        var absorber = PickAbsorber(owner);
        if (absorber == null)
        {
            return target; // 没有可承伤的随从 → 玩家自己吃
        }

        s_absorber = absorber;
        return absorber;
    }

    // 新一轮伤害开始：清掉标记（原版在结算前会对原目标调用 BeforeOsty）
    public static void ResetBeforeDamage(Player? owner, Creature target)
    {
        if (owner != null && target == owner.Creature)
        {
            s_absorber = null;
        }
    }

    // 溢出处理（原版 ModifyHpLostAfterOsty）。原版在这个阶段会调用两次：
    //   1) target = 转移后的目标（随从）→ 它该掉的血
    //   2) target = 原目标（玩家），amount = 溢出的那部分（OverkillDamage）
    public static decimal ModifyOverflow(Player? owner, Creature target, decimal amount, ValueProp props, Creature? dealer)
    {
        if (owner == null)
        {
            return amount;
        }

        // 这一击落在我们的随从身上：随从自己攒的格挡必须在这里手动消耗。
        // 原版伤害流程里，对"打向随从"的伤害取的是**主人**的格挡
        // （CreatureCmd: creature = originalTarget.PetOwner?.Creature ?? originalTarget），
        // 随从自己的格挡全程用不上 —— 不补这一步，坚果墙加格挡就会"看起来没效果"。
        if (target != owner.Creature)
        {
            if (target.PetOwner != owner)
            {
                return amount;
            }
            var blocked = target.DamageBlockInternal(amount, props);
            return Math.Max(amount - blocked, 0m);
        }

        if (s_absorber == null)
        {
            // 没有标记 → 这是普通伤害，绝不能返回 0，否则玩家对所有攻击免疫
            return amount;
        }
        var absorber = s_absorber;
        s_absorber = null;

        // 由植物自己接下 → 溢出吞掉，绝不落到玩家身上
        if (absorber.Monster is PlantSummonBase)
        {
            return 0m;
        }

        // 骨手 / 驯服怪物接下 → 它们没有植物那种"吞掉溢出"的设定，
        // 溢出在同一击内继续传给链上的下一棒（骨手 → 驯服怪物 → 豌豆射手 → 向日葵）；
        // 没有下一棒能接才按原版回给玩家
        var next = PickNextAbsorber(owner, absorber);
        if (next == null || amount <= 0m)
        {
            return amount;
        }
        // Unpowered：这是同一份伤害的延续，不要再吃一次力量/易伤等修正，也避免再次被转移
        var cascadeProps = props | ValueProp.Unpowered;
        TaskHelper.RunSafely(CreatureCmd.Damage(
            new BlockingPlayerChoiceContext(), new List<Creature> { next }, amount, cascadeProps, dealer, null, null));
        Entry.Logger.Info($"[PetAbsorb] 溢出伤害 {amount} 由 {absorber.Monster?.Id.Entry} 转给 {next.Monster?.Id.Entry}");
        return 0m;
    }
}
