using Dalamud.Interface.ImGuiNotification;
using Glamourer.Designs;
using ImSharp;
using Luna;

namespace Glamourer.Gui.Tabs.DesignTab;

public sealed class SetFromClipboardButton(DesignFileSystem fileSystem, DesignConverter converter, DesignManager manager)
    : BaseIconButton<AwesomeIcon>
{
    public override bool IsVisible
        => fileSystem.Selection.Selection is not null;

    public override AwesomeIcon Icon
        => LunaStyle.FromClipboardIcon;

    public override bool Enabled
        => !((Design)fileSystem.Selection.Selection!.Value).WriteProtected();

    public override bool HasTooltip
        => true;

    public override void DrawTooltip()
        => Im.Text(
            "尝试从剪贴板应用设计到此设计。\n按住 Ctrl 键仅应用装备。\n按住 Shift 键仅应用外貌。"u8);

    public override void OnClick()
    {
        try
        {
            var text = Im.Clipboard.GetUtf16();
            var (applyEquip, applyCustomize) = UiHelpers.ConvertKeysToBool();
            var design = converter.FromBase64(text, applyCustomize, applyEquip, out _)
             ?? throw new Exception("剪贴板不包含有效数据。");
            manager.ApplyDesign((Design)fileSystem.Selection.Selection!.Value, design);
        }
        catch (Exception ex)
        {
            Glamourer.Messager.NotificationMessage(ex, $"无法应用剪贴板数据到 {((Design)fileSystem.Selection.Selection!.Value).Name}。",
                $"无法应用剪贴板数据到设计 {((Design)fileSystem.Selection.Selection!.Value).Identifier}", NotificationType.Error,
                false);
        }
    }
}
