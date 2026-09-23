using Glamourer.Designs;
using Glamourer.Designs.CustomizePlus;
using ImSharp;
using Luna;

namespace Glamourer.Gui.Tabs.DesignTab;

/// <summary> Toggles Customize+ application mode (permanent vs temporary), analogous to <see cref="LockButton"/>. </summary>
public sealed class CustomizePlusApplicationModeButton(DesignFileSystem fileSystem, DesignManager manager) : BaseIconButton<AwesomeIcon>
{
    private readonly Im.ColorDisposable _color = new();

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

        if (mode == CustomizePlusApplicationMode.PermanentProfile)
        {
            using (ImGuiColor.Text.Push(ColorId.ActorAvailable.Value))
                Im.Text("正常配置"u8);

            Im.Line.SameInner();
            Im.Text(" C+ 角色配置。"u8);

            Im.Text("\n类似 DynamicBridge 的行为方式。\n不再匹配时尝试恢复到应用前的状态。"u8);
            Im.Text("\n单击切换到临时配置。"u8);
        }
        else
        {
            Im.Text("临时配置 C+ 角色配置。"u8);
            Im.Text("\n不会修改你在 C+ 中的选择。\n但可能不会被 Mare 等插件同步你的 C+ 状态。"u8);

            Im.Line.New();

            Im.Text("单击切换到"u8);
            Im.Line.NoSpacing();

            using (ImGuiColor.Text.Push(ColorId.ActorAvailable.Value))
                Im.Text("正常配置"u8);

            Im.Line.NoSpacing();
            Im.Text("。"u8);
        }
    }

    public override void OnClick()
    {
        var design = (Design)fileSystem.Selection.Selection!.Value;
        var next = design.CustomizePlusApplicationMode == CustomizePlusApplicationMode.PermanentProfile
            ? CustomizePlusApplicationMode.TemporaryProfile
            : CustomizePlusApplicationMode.PermanentProfile;
        manager.ChangeCustomizePlusApplicationMode(design, next);
    }

    protected override void PreDraw()
    {
        if (((Design)fileSystem.Selection.Selection!.Value).CustomizePlusApplicationMode
         == CustomizePlusApplicationMode.PermanentProfile)
            _color.Push(ImGuiColor.Text, ColorId.ActorAvailable.Value);
    }

    protected override void PostDraw()
        => _color.Dispose();
}
