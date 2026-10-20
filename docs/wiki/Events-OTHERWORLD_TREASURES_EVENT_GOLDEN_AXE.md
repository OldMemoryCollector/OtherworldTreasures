# 樵夫之泉

[首页](Home.md) / [事件图鉴](Events.md)

<img src="https://raw.githubusercontent.com/OldMemoryCollector/OtherworldTreasures/main/docs/wiki/images/OTHERWORLD_TREASURES_EVENT_GOLDEN_AXE.webp" width="800" alt="樵夫之泉">

## 出现条件

| 项目 | 规则 |
| --- | --- |
| 出现层数 | 第二、第三幕 |
| 出现位置 | 普通问号事件，无固定的小楼层或房间序号。 |
| 前置条件 | 本局尚未遇见本事件，且队伍至少一名玩家拥有可金银强化的遗物，或拥有一张可移除的基础／普通攻击牌、技能牌或能力牌；遇过「旧忆收藏家」后仍可出现。 |
| 重复限制 | 本局至多遇见一次。 |

**选项可用条件：**金银强化要求遗物尚未镀金或镀银、未熔化、未蜡化、未耗尽且支持对应强化；投入对象由本次事件随机决定。

层数按第一、第二、第三幕计算；满足条件表示可以进入候选，不保证本幕一定遇见。

你没有回答，转身离去。<br>
<br>
女神愣了一下，随后消失在湖水中。

## 金银强化

诚实归还只有一个选项，本次为金色还是银色由遗物适配情况与随机决定。金色主要提高正面收益，银色主要降低触发所需回合或牌数。具体结果见[金色遗物](Golden-Relics.md)与[银色遗物](Silver-Relics.md)。

## 分支选项

不同页面的同名选项属于不同分支，并不同时出现。实际奖励与数量以本次事件预览为准。

| 选项 | 效果 |
| --- | --- |
| 离开 | 继续前进。 |
| 诚实 | 「破旧的那件才是我的。」获得**金色遗物**。 |
| 说谎 | 「**金色的那件**是我的！」取回原遗物，获得【愧疚】。 |
| 离开 | 放弃原遗物。 |
| 诚实 | 「都不是，我掉的是普通的卡牌。」失去原卡牌，获得1张稀有牌和1张罕见牌。 |
| 说谎 | 「**金色的那张**是我的！」取回原卡牌，获得【愧疚】。 |
| 离开 | 放弃原卡牌。 |
| 诚实 | 「破旧的那件才是我的。」获得银色遗物。 |
| 说谎 | 「银色的那件是我的！」取回原遗物，获得【愧疚】。 |
| 离开 | 放弃原遗物。 |

**条件来源：**[OtherworldEvent.cs](https://github.com/OldMemoryCollector/OtherworldTreasures/blob/main/Scripts/Events/OtherworldEvent.cs) · [GoldenAxe.cs](https://github.com/OldMemoryCollector/OtherworldTreasures/blob/main/Scripts/Events/GoldenAxe.cs) · [GoldenRelics.cs](https://github.com/OldMemoryCollector/OtherworldTreasures/blob/main/Scripts/Events/GoldenRelics.cs) · [SilverRelics.cs](https://github.com/OldMemoryCollector/OtherworldTreasures/blob/main/Scripts/Events/SilverRelics.cs)

---

[实现来源](https://github.com/OldMemoryCollector/OtherworldTreasures/blob/main/Scripts/Events/GoldenAxe.cs) · [返回事件图鉴](Events.md)
