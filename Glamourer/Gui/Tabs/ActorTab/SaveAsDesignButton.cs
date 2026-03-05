using Glamourer.Designs;
using ImSharp;
using Luna;

namespace Glamourer.Gui.Tabs.ActorTab;

public sealed class SaveAsDesignButton(ActorSelection selection, DesignConverter converter, DesignManager designManager)
    : BaseIconButton<AwesomeIcon>, IUiService
{
    public override AwesomeIcon Icon
        => LunaStyle.SaveIcon;

    public override bool IsVisible
        => selection.State?.ModelData.ModelId is 0;

    public override bool Enabled
        => !(selection.State?.IsLocked ?? true);


    public override bool HasTooltip
        => true;

    public override void DrawTooltip()
        => Im.Text(
            "将当前状态保存为设计。\n按住 Ctrl 键以禁用保存设计的外貌。\n按住 Shift 键以禁用保存设计的装备。"u8);

    private string      _newName = string.Empty;
    private DesignBase? _newDesign;

    public override void OnClick()
    {
        Im.Popup.Open("Save as Design"u8);
        _newName   = selection.State!.Identifier.ToName();
        _newDesign = converter.Convert(selection.State, ApplicationRules.FromModifiers(selection.State));
    }

    protected override void PostDraw()
    {
        if (!InputPopup.Open("Save as Design"u8, _newName, out var newName, "Enter Design Name..."u8))
            return;

        if (_newDesign is not null && newName.Length > 0)
            designManager.CreateClone(_newDesign, newName, true);
        _newDesign = null;
        _newName   = string.Empty;
    }
}
