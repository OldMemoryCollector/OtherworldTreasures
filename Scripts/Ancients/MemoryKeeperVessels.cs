using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Events;
#if !STS2_113
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Events;
#endif
using OtherworldTreasures.Scripts.Relics;

namespace OtherworldTreasures.Scripts.Ancients;

// 按当前遗物选项填充已有背景节点。113由原版背景配置回调接入，111保留布局接入。
//
// 场景节点约定（OtherworldTreasures/scenes/events/background_scenes/memory_keeper.tscn）：
//   MemoryKeeperBackground
//     Bottles/Bottle1..4/Relic        —— 前排四只瓶子，一瓶对应一个选项
//     JarRelics/JarSlot1..9/Relic     —— 背景画好的罐子里的空位，放没被抽到的遗物
internal static class MemoryKeeperVessels
{
#if !STS2_113
    private const string RootName = "MemoryKeeperBackground";
#endif
    private const int BottleCount = 4;

    // 选项池里能出现的全部遗物与它们的图标。新增池成员时这里要同步加一行，
    // 否则那一件在两个地方都不会出现。
    private static readonly (Type Type, string Path)[] Pool =
    [
        (typeof(DeckOfWonders), "res://OtherworldTreasures/images/ancients/vessels/deck_of_wonders.png"),
        (typeof(KurasDice), "res://OtherworldTreasures/images/ancients/vessels/kuras_dice.png"),
        (typeof(AshenFlask), "res://OtherworldTreasures/images/ancients/vessels/ashen_flask.png"),
        (typeof(FourDimensionalPocket), "res://OtherworldTreasures/images/ancients/vessels/four_dimensional_pocket.png"),
        (typeof(DavesSeeds), "res://OtherworldTreasures/images/ancients/vessels/daves_seeds.png"),
        (typeof(FourSoulsGem), "res://OtherworldTreasures/images/ancients/vessels/four_souls_gem.png"),
        (typeof(Omnitrix), "res://OtherworldTreasures/images/ancients/vessels/omnitrix.png")
    ];

#if !STS2_113
    internal static void Apply(Node layout, IReadOnlyList<EventOption> options, int attempt = 0)
    {
        // 选完之后界面会换成单个「继续」选项，那种情况保持上一帧的摆放不动。
        var chosen = options.Where(o => o.Relic != null).Select(o => o.Relic!.GetType()).ToList();
        if (chosen.Count == 0) return;

        var bg = layout.FindChild(RootName, true, false) as Control;
        if (bg == null)
        {
            // 背景节点理论上先于选项创建；真没找到就等一帧再试，不无限重试。
            if (attempt >= 3) return;
            Callable.From(() => Apply(layout, options, attempt + 1)).CallDeferred();
            return;
        }

        ApplyToBackground(bg, options);
    }
#endif

    internal static void ApplyToBackground(Control bg, IReadOnlyList<EventOption> options)
    {
        // 选完后的“继续”不覆盖已有图案。
        var chosen = options.Where(o => o.Relic != null).Select(o => o.Relic!.GetType()).ToList();
        if (chosen.Count == 0) return;

        FillBottles(bg, chosen);
        FillJars(bg, chosen);
    }

    private static void FillBottles(Node bg, IReadOnlyList<Type> chosen)
    {
        for (int i = 0; i < BottleCount; i++)
        {
            var bottle = bg.GetNodeOrNull<Control>($"Bottles/Bottle{i + 1}");
            if (bottle == null) continue;

            bool used = i < chosen.Count;
            bottle.Visible = used;
            if (!used) continue;

            SetIcon(bottle.GetNodeOrNull<TextureRect>("Relic"), chosen[i], .94f);
        }
    }

    private static void FillJars(Node bg, IReadOnlyList<Type> chosen)
    {
        var slots = bg.GetNodeOrNull<Control>("JarRelics");
        if (slots == null) return;

        var rest = Pool.Where(p => !chosen.Contains(p.Type)).ToList();
        if (rest.Count == 0) return;

        var order = Enumerable.Range(0, slots.GetChildCount()).ToList();
        Shuffle(order);
        var random = new Random((int)GD.Randi());

        int used = Math.Min(rest.Count, order.Count);
        for (int i = 0; i < used; i++)
        {
            var slot = slots.GetChild(order[i]) as Control;
            if (slot == null) continue;
            SetIcon(slot.GetNodeOrNull<TextureRect>("Relic"), rest[random.Next(rest.Count)].Type, .94f);
        }
    }

    private static void SetIcon(TextureRect? rect, Type relicType, float alpha)
    {
        if (rect == null) return;

        var entry = Array.Find(Pool, p => p.Type == relicType);
        if (entry.Path == null)
        {
            GD.PushWarning($"[OtherworldTreasures] 记忆收藏家：遗物 {relicType.Name} 没有登记图标路径。");
            return;
        }

        var texture = ResourceLoader.Load<Texture2D>(entry.Path);
        if (texture == null)
        {
            GD.PushWarning($"[OtherworldTreasures] 记忆收藏家：图标加载失败 {entry.Path}");
            return;
        }

        rect.Texture = texture;
        // 冷紫调偏色，让它看起来像隔着玻璃；透明度由场景里的呼吸动画驱动 modulate 再乘一次。
        rect.SelfModulate = new Color(.94f, .90f, 1f, alpha);
    }

    private static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = GD.RandRange(0, i);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}

#if !STS2_113
// NEventRoom.SetOptions 会先建好背景（InitializeVisuals），再 Layout.AddOptions 挂按钮，
// 所以在这里补图时背景节点一定已经在场景树里。
[HarmonyPatch(typeof(NEventLayout), nameof(NEventLayout.AddOptions))]
internal static class MemoryKeeperVesselsPatch
{
    private static void Postfix(NEventLayout __instance, IEnumerable<EventOption> options)
    {
        try
        {
            MemoryKeeperVessels.Apply(__instance, options.ToList());
        }
        catch (Exception e)
        {
            GD.PushWarning("[OtherworldTreasures] 记忆收藏家罐体填充失败：" + e);
        }
    }
}

#endif
