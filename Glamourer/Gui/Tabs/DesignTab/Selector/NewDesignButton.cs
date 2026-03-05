using Glamourer.Designs;
using ImSharp;
using Luna;

namespace Glamourer.Gui.Tabs.DesignTab;

public sealed class NewDesignButton(DesignManager designManager) : BaseIconButton<AwesomeIcon>
{
    public override AwesomeIcon Icon
        => LunaStyle.AddObjectIcon;

    public override bool HasTooltip
        => true;

    public override void DrawTooltip()
        => Im.Text("按默认配置创建一个新设计。"u8);

    public override void OnClick()
        => Im.Popup.Open("##NewDesign"u8);

    protected override void PostDraw()
    {
        if (!InputPopup.OpenName("##NewDesign"u8, out var newName))
            return;

        designManager.CreateEmpty(newName, true);
    }
}
