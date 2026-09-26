using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Audio;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using OtherworldTreasures.Scripts.Relics;
using STS2RitsuLib.Scaffolding.Content;

namespace OtherworldTreasures.Scripts.RestSite;

// 篝火注火选项：属于 AshenFlask 遗物
// 继承 RitsuLib 的 ModRestSiteOptionTemplate：
// 通过 AssetProfile 替换图标（基类 Icon 非 virtual），通过 CustomTitle 替换标题
public class AshenFlaskKindleOption : ModRestSiteOptionTemplate
{
    public override string OptionId => "ASHEN_FLASK_KINDLE";
    private readonly AshenFlask _flask;

    public AshenFlaskKindleOption(Player owner, AshenFlask flask) : base(owner)
    {
        _flask = flask;
    }

    // 256x169，与原版 option_heal.png / option_smith.png 同尺寸
    public override RestSiteOptionAssetProfile AssetProfile { get; } =
        new("res://OtherworldTreasures/images/ui/rest_site/option_ashen_flask_kindle.png");

    // 本地化表 rest_site_ui，key 与原版命名规则一致
    public override LocString? CustomTitle =>
        new LocString("rest_site_ui", "OPTION_ASHEN_FLASK_KINDLE.name");

    public override Task<bool> OnSelect()
    {
        _flask.Kindle();
        return Task.FromResult(true);
    }

    // 与原版 KindleRestSiteOption.PlayKindleVfx 一致：火焰音效 + 角色抖动 + 遗物闪光
    public override Task DoLocalPostSelectVfx(CancellationToken ct = default)
    {
        PlayKindleVfx();
        return Task.CompletedTask;
    }

    public override Task DoRemotePostSelectVfx()
    {
        PlayKindleVfx();
        return Task.CompletedTask;
    }

    private void PlayKindleVfx()
    {
        SfxCmd.Play(FmodSfx.fire);
        var character = NRestSiteRoom.Instance?.Characters.First(c => c.Player == Owner);
        character?.Shake();
        var flashVfx = NRelicFlashVfx.Create(_flask);
        if (flashVfx != null)
        {
            character?.AddChildSafely(flashVfx);
            flashVfx.Position = Vector2.Zero;
        }
    }
}
