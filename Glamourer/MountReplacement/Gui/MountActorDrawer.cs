using ImSharp;
using Luna;
using Penumbra.GameData.Actors;

namespace Glamourer.MountReplacement.Gui;

public sealed class MountActorDrawer : IUiService
{
    private readonly MountReplacementManager _manager;
    private readonly MountCombo              _combo;

    public MountActorDrawer(MountReplacementManager manager)
    {
        _manager = manager;
        _combo   = new MountCombo(manager, MountCombo.UnsetLabel);
    }

    public void Draw(in ActorIdentifier identifier)
    {
        if (!_manager.Enabled || !_manager.IsLocalPlayerMount(identifier))
            return;

        var source = identifier.DataId.Id;
        if (_combo.Draw("##mountReplacement"u8, _manager.GetTarget(source), 300 * Im.Style.GlobalScale, out var target))
            _manager.SetTarget(source, target);
        LunaStyle.DrawAlignedHelpMarkerLabel("坐骑显示为"u8,
            "在本地将此坐骑的外观显示为另一坐骑，与坐骑替换标签页中此坐骑的规则相同。\n选择「请选择」以取消替换。"u8);
        Im.Separator();
    }
}
