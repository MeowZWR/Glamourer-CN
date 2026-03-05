using Glamourer.Config;
using Glamourer.Designs;
using ImSharp;
using Luna;

namespace Glamourer.Gui.Tabs.SettingsTab;

public sealed class DesignColorUi(DesignColors colors, Configuration config) : IUiService
{
    private string _newName = string.Empty;

    public void Draw()
    {
        using var table = Im.Table.Begin("designColors"u8, 3, TableFlags.RowBackground);
        if (!table)
            return;

        var     changeString = string.Empty;
        Rgba32? changeValue  = null;

        table.SetupColumn("##Delete"u8,   TableColumnFlags.WidthFixed, Im.Style.FrameHeight);
        table.SetupColumn("##Select"u8,   TableColumnFlags.WidthFixed, Im.Style.FrameHeight);
        table.SetupColumn("颜色名称"u8, TableColumnFlags.WidthStretch);

        table.HeaderRow();

        table.NextColumn();
        if (ImEx.Icon.Button(LunaStyle.RefreshIcon, "恢复缺失设计颜色的默认颜色。"u8,
                colors.MissingColor == DesignColors.MissingColorDefault))
        {
            changeString = DesignColors.MissingColorName;
            changeValue  = DesignColors.MissingColorDefault;
        }

        table.NextColumn();
        if (DrawColorButton(DesignColors.MissingColorNameU8, colors.MissingColor, out var newColor))
        {
            changeString = DesignColors.MissingColorName;
            changeValue  = newColor;
        }

        table.NextColumn();
        Im.Cursor.X += Im.Style.FramePadding.X;
        Im.Text(DesignColors.MissingColorNameU8);
        Im.Tooltip.OnHover("当设计中指定的颜色不可用时使用此颜色。"u8);

        var disabled = !config.DeleteDesignModifier.IsActive();
        foreach (var (idx, (name, color)) in colors.Index())
        {
            using var id = Im.Id.Push(idx);
            table.NextColumn();

            if (ImEx.Icon.Button(LunaStyle.DeleteIcon, "删除此颜色。这不会从使用它的设计中删除它。"u8, disabled))
            {
                changeString = name;
                changeValue  = null;
            }

            if (disabled)
                Im.Tooltip.OnHover($"\n按住 {config.DeleteDesignModifier} 删除。");

            table.NextColumn();
            if (DrawColorButton(name, color, out newColor))
            {
                changeString = name;
                changeValue  = newColor;
            }

            table.NextColumn();
            Im.Cursor.X += Im.Style.FramePadding.X;
            Im.Text(name);
        }

        table.NextColumn();
        (var tt, disabled) = _newName.Length == 0
            ? ("首先指定一个新颜色的名称。", true)
            : _newName is DesignColors.MissingColorName or DesignColors.AutomaticName
                ? ($"不能使用名称 {DesignColors.MissingColorName} 或 {DesignColors.AutomaticName}，请选择一个不同的名称。", true)
                : colors.ContainsKey(_newName)
                    ? ($"颜色 {_newName} 已存在，请选择一个不同的名称。", true)
                    : ($"将新颜色 {_newName} 添加到您的列表中。", false);
        if (ImEx.Icon.Button(LunaStyle.AddObjectIcon, tt, disabled))
        {
            changeString = _newName;
            changeValue  = 0xFFFFFFFF;
        }

        table.NextColumn();
        table.NextColumn();
        Im.Item.SetNextWidth(Im.ContentRegion.Available.X);
        if (Im.Input.Text("##newDesignColor"u8, ref _newName, "新颜色名称..."u8, InputTextFlags.EnterReturnsTrue))
        {
            changeString = _newName;
            changeValue  = 0xFFFFFFFF;
        }

        if (changeString.Length > 0)
        {
            if (!changeValue.HasValue)
                colors.DeleteColor(changeString);
            else
                colors.SetColor(changeString, changeValue.Value);
        }
    }

    public static bool DrawColorButton(Utf8StringHandler<LabelStringHandlerBuffer> tooltip, Rgba32 color, out Rgba32 newColor)
    {
        var ret = Im.Color.Editor(tooltip, ref color, ColorEditorFlags.AlphaPreviewHalf | ColorEditorFlags.NoInputs);
        Im.Tooltip.OnHover(ref tooltip);
        newColor = color;
        return ret;
    }
}
