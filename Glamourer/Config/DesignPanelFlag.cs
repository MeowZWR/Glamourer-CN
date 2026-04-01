using ImSharp;
using Luna.Generators;

namespace Glamourer.Config;

[Flags]
[NamedEnum(Utf16: false)]
public enum DesignPanelFlag : uint
{
    [Name("外貌")]
    Customization = 0x0001,

    [Name("装备")]
    Equipment = 0x0002,

    [Name("高级外貌")]
    AdvancedCustomizations = 0x0004,

    [Name("高级染色")]
    AdvancedDyes = 0x0008,

    [Name("外观详情")]
    AppearanceDetails = 0x0010,

    [Name("设计详情")]
    DesignDetails = 0x0020,

    [Name("模组关联")]
    ModAssociations = 0x0040,

    [Name("设计链接")]
    DesignLinks = 0x0080,

    [Name("应用规则")]
    ApplicationRules = 0x0100,

    [Name("调试数据")]
    DebugData = 0x0200,

    [Name("Customize+")]
    CustomizePlusAssociations = 0x0400,
}

public static partial class DesignPanelFlagExtensions
{
    private static readonly StringU8 Expand = new("展开"u8);

    public static Im.HeaderDisposable Header(this DesignPanelFlag flag, Configuration config)
    {
        if (config.HideDesignPanel.HasFlag(flag))
            return default;

        var expand = config.AutoExpandDesignPanel.HasFlag(flag);
        return Im.Tree.HeaderId(flag.ToNameU8(), expand ? TreeNodeFlags.DefaultOpen : TreeNodeFlags.None);
    }

    public static void DrawTable(ReadOnlySpan<byte> label, DesignPanelFlag hidden, DesignPanelFlag expanded, Action<DesignPanelFlag> setterHide,
        Action<DesignPanelFlag> setterExpand)
    {
        var checkBoxWidth = Math.Max(Im.Style.FrameHeight, Expand.CalculateSize().X);
        var textWidth     = AdvancedCustomizations_Name__GenU8.CalculateSize().X;
        var tableSize = 2 * (textWidth + 2 * checkBoxWidth)
          + 10 * Im.Style.CellPadding.X
          + 2 * Im.Style.WindowPadding.X
          + 2 * Im.Style.FrameBorderThickness;
        using var table = Im.Table.Begin(label, 6, TableFlags.RowBackground | TableFlags.Borders,
            new Vector2(tableSize, DesignPanelFlag.Values.Count * Im.Style.FrameHeight));
        if (!table)
            return;

        var headerColor    = Im.Color.Get(ImGuiColor.TableHeaderBackground);
        var checkBoxOffset = (checkBoxWidth - Im.Style.FrameHeight) / 2;
        table.SetupColumn("面板##1"u8,  TableColumnFlags.WidthFixed, textWidth);
        table.SetupColumn("显示##1"u8,   TableColumnFlags.WidthFixed, checkBoxWidth);
        table.SetupColumn("展开##1"u8, TableColumnFlags.WidthFixed, checkBoxWidth);
        table.SetupColumn("面板##2"u8,  TableColumnFlags.WidthFixed, textWidth);
        table.SetupColumn("显示##2"u8,   TableColumnFlags.WidthFixed, checkBoxWidth);
        table.SetupColumn("展开##2"u8, TableColumnFlags.WidthFixed, checkBoxWidth);

        table.HeaderRow();
        foreach (var panel in DesignPanelFlag.Values)
        {
            using var id = Im.Id.Push((int)panel);
            table.NextColumn();
            table.SetBackgroundColor(TableBackgroundTarget.Cell, headerColor);
            ImEx.TextFrameAligned(panel.ToNameU8());
            var isShown    = !hidden.HasFlag(panel);
            var isExpanded = expanded.HasFlag(panel);

            table.NextColumn();
            Im.Cursor.X += checkBoxOffset;
            if (Im.Checkbox("##show"u8, ref isShown))
                setterHide.Invoke(isShown ? hidden & ~panel : hidden | panel);
            Im.Tooltip.OnHover(
                "在所有相关选项卡中显示此面板及相关功能。\n\n注意：关闭此项并不会禁用功能本身，仅隐藏其界面显示。请自行承担隐藏面板带来的操作风险。"u8);

            table.NextColumn();
            Im.Cursor.X += checkBoxOffset;
            if (Im.Checkbox("##expand"u8, ref isExpanded))
                setterExpand.Invoke(isExpanded ? expanded | panel : expanded & ~panel);
            Im.Tooltip.OnHover("在所有相关选项卡中默认展开此面板。"u8);
        }
    }
}
