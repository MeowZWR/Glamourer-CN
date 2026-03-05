using Dalamud.Interface.ImGuiNotification;
using Glamourer.Designs;
using ImSharp;
using Luna;

namespace Glamourer.Gui.Tabs.DesignTab;

public sealed class DesignUndoButton(DesignFileSystem fileSystem, DesignManager manager) : BaseIconButton<AwesomeIcon>
{
    public override bool IsVisible
        => fileSystem.Selection.Selection is not null;

    public override AwesomeIcon Icon
        => LunaStyle.ResetIcon;

    public override bool Enabled
        => !((Design)fileSystem.Selection.Selection!.Value).WriteProtected() && manager.CanUndo((Design)fileSystem.Selection.Selection!.Value);

    public override bool HasTooltip
        => true;

    public override void DrawTooltip()
        => Im.Text(
            "撤销上一次将完整设计应用到此设计的操作（如果您不小心用其他设计覆盖了当前设计）。"u8);

    public override void OnClick()
    {
        try
        {
            manager.UndoDesignChange((Design)fileSystem.Selection.Selection!.Value);
        }
        catch (Exception ex)
        {
            Glamourer.Messager.NotificationMessage(ex,
                $"Could not undo last changes to {((Design)fileSystem.Selection.Selection!.Value).Name}.",
                NotificationType.Error, false);
        }
    }
}
