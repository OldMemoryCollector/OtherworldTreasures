using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace OtherworldTreasures.Scripts.Monsters;

// 植物召唤物的表现层工具。
// 所有贴图都是"可选"的：文件不存在时静默跳过视觉，功能照常（方便先做功能、后补美术）。
internal static class PlantFx
{
    // 读取贴图；文件不存在时返回 null（不报错、不刷日志）
    internal static Texture2D? LoadTexture(string path)
    {
        if (!ResourceLoader.Exists(path))
        {
            return null;
        }
        return GD.Load<Texture2D>(path);
    }

    // 在节点树里找 Sprite2D（植物的形象就是 Sprite2D 主体）
    internal static Sprite2D? FindSprite(Node? node)
    {
        if (node == null)
        {
            return null;
        }
        if (node is Sprite2D sprite)
        {
            return sprite;
        }
        foreach (var child in node.GetChildren())
        {
            var found = FindSprite(child);
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }

    // 在 parent 下生成一个显示贴图的 Sprite2D，并放到指定的画布坐标
    internal static Sprite2D SpawnSprite(Node2D parent, Texture2D texture, Vector2 canvasPosition, float scale)
    {
        var sprite = new Sprite2D
        {
            Texture = texture,
            Scale = Vector2.One * scale,
            ZIndex = 10, // 盖在植物形象之上（但不要高过 UI）
        };
        parent.AddChild(sprite);
        sprite.GlobalPosition = canvasPosition;
        return sprite;
    }

    // 等一个 Tween 跑完；带超时兜底，避免动画异常时卡住回合流程
    internal static async Task AwaitTween(Node host, Tween tween, float timeoutSeconds)
    {
        await Task.WhenAny(
            AwaitTweenFinished(host, tween),
            Cmd.CustomScaledWait(timeoutSeconds, timeoutSeconds));
    }

    private static async Task AwaitTweenFinished(Node host, Tween tween)
    {
        try
        {
            await host.ToSignal(tween, Tween.SignalName.Finished);
        }
        catch
        {
            // 节点提前释放等异常情况，忽略即可
        }
    }

    // 能量计数器在当前"战斗画布空间"里的位置（阳光飞过去用）
    internal static Vector2? GetEnergyCounterCanvasPosition(Node2D referenceNode)
    {
        var container = NCombatRoom.Instance?.Ui?.EnergyCounterContainer;
        if (container == null)
        {
            return null;
        }
        // 控件局部中心 → 屏幕坐标 → 当前画布坐标
        var screen = container.GetGlobalTransformWithCanvas() * (container.Size * 0.5f);
        return referenceNode.GetCanvasTransform().AffineInverse() * screen;
    }
}
