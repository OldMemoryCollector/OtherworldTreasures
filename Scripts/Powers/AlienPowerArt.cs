using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.Powers;

internal static class AlienPowerArt
{
    internal static PowerAssetProfile Profile(string key)
    {
        var path = $"res://OtherworldTreasures/images/powers/{key.ToLowerInvariant()}.svg";
        return new(IconPath: path, BigIconPath: path);
    }
}
