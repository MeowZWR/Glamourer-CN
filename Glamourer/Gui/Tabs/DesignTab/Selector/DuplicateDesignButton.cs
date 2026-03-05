using Glamourer.Designs;
using ImSharp;
using Luna;

namespace Glamourer.Gui.Tabs.DesignTab;

public sealed class DuplicateDesignButton(DesignFileSystem fileSystem, DesignManager designManager) : BaseIconButton<AwesomeIcon>
{
    private readonly WeakReference<Design> _design = new(null!);

    public override AwesomeIcon Icon
        => LunaStyle.DuplicateIcon;

    public override bool HasTooltip
        => true;

    public override bool Enabled
        => fileSystem.Selection.Selection is not null;

    public override void DrawTooltip()
        => Im.Text(fileSystem.Selection.Selection is null ? "未选择设计。"u8 : "复制当前选中的设计。"u8);

    public override void OnClick()
    {
        _design.SetTarget(fileSystem.Selection.Selection?.GetValue<Design>()!);
        Im.Popup.Open("##CloneDesign"u8);
    }

    protected override void PostDraw()
    {
        if (!InputPopup.OpenName("##CloneDesign"u8, out var newName))
            return;

        if (_design.TryGetTarget(out var design))
            designManager.CreateClone(design, newName, true);
        _design.SetTarget(null!);
    }
}
