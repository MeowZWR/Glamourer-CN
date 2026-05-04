using Glamourer.Designs;
using Glamourer.Designs.CustomizePlus;
using ImSharp;
using Luna;

namespace Glamourer.Gui.Tabs.DesignTab;

/// <summary> Toggles Customize+ application mode (permanent vs temporary), analogous to <see cref="LockButton"/>. </summary>
public sealed class CustomizePlusApplicationModeButton(DesignFileSystem fileSystem, DesignManager manager) : BaseIconButton<AwesomeIcon>
{
    public override bool IsVisible
        => fileSystem.Selection.Selection is not null;

    public override AwesomeIcon Icon
        => ((Design)fileSystem.Selection.Selection!.Value).CustomizePlusApplicationMode
         == CustomizePlusApplicationMode.PermanentProfile
            ? LunaStyle.LinkIcon
            : LunaStyle.UnlinkIcon;

    public override bool HasTooltip
        => true;

    public override void DrawTooltip()
    {
        var mode = ((Design)fileSystem.Selection.Selection!.Value).CustomizePlusApplicationMode;
        Im.Text(ModeDescription(mode));
        Im.Text(mode == CustomizePlusApplicationMode.PermanentProfile
            ? "\n单击切换到临时配置。"u8
            : "\n单击切换到正常配置。"u8);
    }

    public override void OnClick()
    {
        var design = (Design)fileSystem.Selection.Selection!.Value;
        var next = design.CustomizePlusApplicationMode == CustomizePlusApplicationMode.PermanentProfile
            ? CustomizePlusApplicationMode.TemporaryProfile
            : CustomizePlusApplicationMode.PermanentProfile;
        manager.ChangeCustomizePlusApplicationMode(design, next);
    }

    private static string ModeDescription(CustomizePlusApplicationMode mode)
        => mode switch
        {
            CustomizePlusApplicationMode.PermanentProfile =>
                "正常配置 C+ 角色配置，类似 DynamicBridge 的应用方式。\n应用前会清理 Glamourer 创建的临时配置。\n不再匹配时会尝试恢复应用前已启用的配置。",
            _ =>
                "临时配置 C+ 角色配置，不会修改你在 C+ 中的选择。\n临时配置可能不会被 Mare 等插件同步你的 C+ 状态。",
        };
}
