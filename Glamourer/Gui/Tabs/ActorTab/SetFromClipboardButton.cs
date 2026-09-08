using Dalamud.Interface.ImGuiNotification;
using Glamourer.Designs;
using Glamourer.State;
using ImSharp;
using Luna;

namespace Glamourer.Gui.Tabs.ActorTab;

public sealed class SetFromClipboardButton(ActorSelection selection, DesignConverter converter, StateManager stateManager) : BaseIconButton<AwesomeIcon>, IUiService
{
    public override AwesomeIcon Icon
        => LunaStyle.FromClipboardIcon;

    public override bool IsVisible
        => selection.State is not null;

    public override bool Enabled
        => !(selection.State?.IsLocked ?? true);


    public override bool HasTooltip
        => true;

    public override void DrawTooltip()
        => Im.Text("尝试从剪贴板应用设计。\n按住 Ctrl 键仅应用装备。\n按住 Shift 键仅应用外貌。"u8);

    public override void OnClick()
    {
        try
        {
            var (applyGear, applyCustomize) = UiHelpers.ConvertKeysToBool();
            var text = Im.Clipboard.Get();
            var design = converter.FromBase64(text, applyCustomize, applyGear, out _)
             ?? throw new Exception("剪贴板不包含有效数据。");
            stateManager.ApplyDesign(selection.State!, design, ApplySettings.ManualWithLinks with { IsFinal = true });
        }
        catch (Exception ex)
        {
            Glamourer.Messager.NotificationMessage(ex, $"无法应用剪贴板数据到 {selection.Identifier}。",
                $"无法应用剪贴板数据到设计 {selection.Identifier.Incognito(null)}", NotificationType.Error, false);
        }
    }
}