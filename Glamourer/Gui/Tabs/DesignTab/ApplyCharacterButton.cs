using Dalamud.Interface;
using Dalamud.Interface.ImGuiNotification;
using Glamourer.Designs;
using Glamourer.State;
using ImSharp;
using Luna;
using Penumbra.GameData.Interop;

namespace Glamourer.Gui.Tabs.DesignTab;

public sealed class ApplyCharacterButton(
    DesignFileSystem fileSystem,
    DesignManager manager,
    ActorObjectManager objects,
    StateManager stateManager,
    DesignConverter converter) : BaseIconButton<AwesomeIcon>
{
    private static readonly AwesomeIcon UserIcon = FontAwesomeIcon.UserEdit;

    public override bool IsVisible
        => fileSystem.Selection.Selection is not null && objects.Player.Valid;

    public override AwesomeIcon Icon
        => UserIcon;

    public override bool Enabled
        => !((Design)fileSystem.Selection.Selection!.Value).WriteProtected();

    public override bool HasTooltip
        => true;

    public override void DrawTooltip()
        => Im.Text("覆盖此设计为当前角色状态。"u8);

    public override void OnClick()
    {
        var selection = (Design)fileSystem.Selection.Selection!.Value;
        try
        {
            var (player, actor) = objects.PlayerData;
            if (!player.IsValid || !actor.Valid || !stateManager.GetOrCreate(player, actor.Objects[0], out var state))
                throw new Exception("没有可用的玩家状态。");

            var design = converter.Convert(state, ApplicationRules.FromModifiers(state))
             ?? throw new Exception("剪贴板不包含有效数据。");
            selection.GetMaterialDataRef().Clear();
            manager.ApplyDesign(selection, design);
        }
        catch (Exception ex)
        {
            Glamourer.Messager.NotificationMessage(ex, $"无法应用玩家状态到 {selection.Name}。",
                $"无法应用玩家状态到设计 {selection.Identifier}", NotificationType.Error, false);
        }
    }
}
