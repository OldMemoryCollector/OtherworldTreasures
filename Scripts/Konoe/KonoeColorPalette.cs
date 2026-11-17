using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace OtherworldTreasures.Scripts.Konoe;

// 24 套配色：索引图的灰度像素值就是调色板索引，运行时查表重映射生成 10 帧上色贴图。
// 这样不必再往包里塞 24x10 张 PNG；切换时只处理当前一套，缓存最近两套。
internal static class KonoeColorPalette
{
    internal const int Count = 24;
    internal const int FrameCount = 10;

    private const string PalettePath = "res://OtherworldTreasures/data/konoe_color_palettes.json";
    private const string FramePathPrefix = "res://OtherworldTreasures/images/ui/konoe/color_select/frame_index_";

    private static readonly Dictionary<string, byte[][]> Palettes = new();
    private static readonly Dictionary<int, Texture2D[]> Cache = new();
    private static bool _loaded;

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        try
        {
            string text = Godot.FileAccess.GetFileAsString(PalettePath);
            if (string.IsNullOrEmpty(text))
            {
                Entry.Logger.Warn("[KonoeColor] 调色板数据为空");
                return;
            }
            using JsonDocument doc = JsonDocument.Parse(text);
            foreach (JsonProperty prop in doc.RootElement.EnumerateObject())
            {
                byte[][] table = new byte[256][];
                for (int i = 0; i < 256; i++) table[i] = new byte[] { 0, 0, 0, 255 };
                int n = 0;
                foreach (JsonElement color in prop.Value.EnumerateArray())
                {
                    if (n >= 256) break;
                    int k = 0;
                    foreach (JsonElement channel in color.EnumerateArray())
                    {
                        table[n][k] = (byte)Math.Clamp(channel.GetInt32(), 0, 255);
                        if (++k >= 4) break;
                    }
                    n++;
                }
                Palettes[prop.Name] = table;
            }
        }
        catch (Exception e)
        {
            Entry.Logger.Warn($"[KonoeColor] 调色板解析失败：{e}");
        }
    }

    // 返回该套配色对应的 10 帧贴图（喂给 SpriteFrames 的 pose 动画）
    internal static Texture2D[] Get(int index)
    {
        index = ((index % Count) + Count) % Count;
        if (Cache.TryGetValue(index, out Texture2D[]? hit)) return hit;
        EnsureLoaded();
        if (!Palettes.TryGetValue($"ph{index:00}_00", out byte[][]? table)) return Array.Empty<Texture2D>();

        Texture2D[] frames = new Texture2D[FrameCount];
        for (int i = 0; i < FrameCount; i++)
        {
            Texture2D? source = GD.Load<Texture2D>($"{FramePathPrefix}{i:00}.png");
            Godot.Image? image = source?.GetImage();
            if (image == null || image.IsEmpty()) continue;
            image.Convert(Godot.Image.Format.Rgba8);
            byte[] data = image.GetData();
            for (int p = 0; p + 3 < data.Length; p += 4)
            {
                int idx = data[p];
                if (idx == 0)
                {
                    data[p] = 0; data[p + 1] = 0; data[p + 2] = 0; data[p + 3] = 0;
                    continue;
                }
                byte[] c = table[idx];
                data[p] = c[0]; data[p + 1] = c[1]; data[p + 2] = c[2]; data[p + 3] = 255;
            }
            Godot.Image colored = Godot.Image.CreateFromData(image.GetWidth(), image.GetHeight(), false, Godot.Image.Format.Rgba8, data);
            frames[i] = ImageTexture.CreateFromImage(colored);
        }

        // 只留最近两套，避免 24 套大图常驻内存
        if (Cache.Count >= 2)
        {
            foreach (int old in new List<int>(Cache.Keys)) Cache.Remove(old);
        }
        Cache[index] = frames;
        return frames;
    }
}
