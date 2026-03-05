using Dalamud.Interface.ImGuiNotification;
using Glamourer.Designs;
using ImSharp;
using Luna;

namespace Glamourer.Gui.Tabs.DesignTab;

public sealed class ExportToClipboardButton(DesignFileSystem fileSystem, DesignConverter converter) : BaseIconButton<AwesomeIcon>
{
    public override bool IsVisible
        => fileSystem.Selection.Selection is not null;

    public override AwesomeIcon Icon
        => LunaStyle.ToClipboardIcon;

    public override bool HasTooltip
        => true;

    public override void DrawTooltip()
        => Im.Text("复制当前设计到剪贴板。"u8);

    public override void OnClick()
    {
        var design = (Design)fileSystem.Selection.Selection!.Value;
        try
        {
            var text = converter.ShareBase64(design);
            Im.Clipboard.Set(text);
        }
        catch (Exception ex)
        {
            Glamourer.Messager.NotificationMessage(ex, $"无法复制 {design.Name} 数据到剪贴板。",
                $"无法复制设计 {design.Identifier} 数据到剪贴板", NotificationType.Error, false);
        }
    }
}
