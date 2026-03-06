using ImSharp;
using Luna;

namespace Glamourer.Gui.Tabs.DesignTab;

public sealed class MoveDesignInput(DesignFileSystemDrawer fileSystem) : BaseButton<IFileSystemData>
{
    /// <inheritdoc/>
    public override ReadOnlySpan<byte> Label(in IFileSystemData _)
        => "##Move"u8;

    /// <summary> Replaces the normal menu item handling for a text input, so the other fields are not used. </summary>
    /// <inheritdoc/>
    public override bool DrawMenuItem(in IFileSystemData data)
    {
        var       currentPath = data.FullPath;
        using var style       = Im.Style.PushDefault(ImStyleDouble.FramePadding);
        MenuSeparator.DrawSeparator();
        Im.Text("移动设计:"u8);
        if (Im.Window.Appearing)
            Im.Keyboard.SetFocusHere();
        var ret = Im.Input.Text(Label(data), ref currentPath, flags: InputTextFlags.EnterReturnsTrue);
        Im.Tooltip.OnHover(
            "输入一个完整路径来移动设计或更改其搜索路径。并按情况创建所有必需的父目录。"u8);
        if (!ret)
            return false;

        fileSystem.FileSystem.RenameAndMove(data, currentPath);
        fileSystem.FileSystem.ExpandAllAncestors(data);
        Im.Popup.CloseCurrent();

        return ret;
    }
}