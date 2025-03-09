using Glamourer.Designs;
using ImGuiNET;
using OtterGui.Text;
using OtterGui.Text.EndObjects;

namespace Glamourer;

[Flags]
public enum DesignPanelFlag : uint
{
    Customization          = 0x0001,
    Equipment              = 0x0002,
    AdvancedCustomizations = 0x0004,
    AdvancedDyes           = 0x0008,
    AppearanceDetails      = 0x0010,
    DesignDetails          = 0x0020,
    ModAssociations        = 0x0040,
    DesignLinks            = 0x0080,
    ApplicationRules       = 0x0100,
    DebugData              = 0x0200,
}

public static class DesignPanelFlagExtensions
{
    public static ReadOnlySpan<byte> ToName(this DesignPanelFlag flag)
        => flag switch
        {
            DesignPanelFlag.Customization          => "外貌"u8,
            DesignPanelFlag.Equipment              => "装备"u8,
            DesignPanelFlag.AdvancedCustomizations => "高级外貌"u8,
            DesignPanelFlag.AdvancedDyes           => "高级染色"u8,
            DesignPanelFlag.DesignDetails          => "设计详情"u8,
            DesignPanelFlag.ApplicationRules       => "应用规则"u8,
            DesignPanelFlag.ModAssociations        => "模组关联"u8,
            DesignPanelFlag.DesignLinks            => "设计链接"u8,
            DesignPanelFlag.DebugData              => "调试数据"u8,
            DesignPanelFlag.AppearanceDetails      => "外观详情"u8,
            _                                      => ""u8,
        };

    public static CollapsingHeader Header(this DesignPanelFlag flag, Configuration config)
    {
        if (config.HideDesignPanel.HasFlag(flag))
            return new CollapsingHeader()
            {
                Disposed = true,
            };

        var expand = config.AutoExpandDesignPanel.HasFlag(flag);
        return ImUtf8.CollapsingHeaderId(flag.ToName(), expand ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None);
    }

    public static void DrawTable(ReadOnlySpan<byte> label, DesignPanelFlag hidden, DesignPanelFlag expanded, Action<DesignPanelFlag> setterHide,
        Action<DesignPanelFlag> setterExpand)
    {
        var       checkBoxWidth = Math.Max(ImGui.GetFrameHeight(), ImUtf8.CalcTextSize("展开"u8).X);
        var       textWidth     = ImUtf8.CalcTextSize(DesignPanelFlag.AdvancedCustomizations.ToName()).X;
        var       tableSize     = 2 * (textWidth + 2 * checkBoxWidth) + 10 * ImGui.GetStyle().CellPadding.X + 2 * ImGui.GetStyle().WindowPadding.X + 2 * ImGui.GetStyle().FrameBorderSize;
        using var table         = ImUtf8.Table(label, 6, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.Borders, new Vector2(tableSize, 6 * ImGui.GetFrameHeight()));
        if (!table)
            return;

        var headerColor    = ImGui.GetColorU32(ImGuiCol.TableHeaderBg);
        var checkBoxOffset = (checkBoxWidth - ImGui.GetFrameHeight()) / 2;
        ImUtf8.TableSetupColumn("面板##1"u8,  ImGuiTableColumnFlags.WidthFixed, textWidth);
        ImUtf8.TableSetupColumn("显示##1"u8,   ImGuiTableColumnFlags.WidthFixed, checkBoxWidth);
        ImUtf8.TableSetupColumn("展开##1"u8, ImGuiTableColumnFlags.WidthFixed, checkBoxWidth);
        ImUtf8.TableSetupColumn("面板##2"u8,  ImGuiTableColumnFlags.WidthFixed, textWidth);                                  
        ImUtf8.TableSetupColumn("显示##2"u8,   ImGuiTableColumnFlags.WidthFixed, checkBoxWidth);
        ImUtf8.TableSetupColumn("展开##2"u8, ImGuiTableColumnFlags.WidthFixed, checkBoxWidth);

        ImGui.TableHeadersRow();
        foreach (var panel in Enum.GetValues<DesignPanelFlag>())
        {
            using var id = ImUtf8.PushId((int)panel);
            ImGui.TableNextColumn();
            ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, headerColor);
            ImUtf8.TextFrameAligned(panel.ToName());
            var isShown    = !hidden.HasFlag(panel);
            var isExpanded = expanded.HasFlag(panel);

            ImGui.TableNextColumn();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + checkBoxOffset);
            if (ImUtf8.Checkbox("##显示"u8, ref isShown))
                setterHide.Invoke(isShown ? hidden & ~panel : hidden | panel);
            ImUtf8.HoverTooltip(
                "在所有相关标签中显示此面板及相关功能。\n\n关闭此选项不会禁用任何功能，只是隐藏显示，因此请谨慎隐藏面板。"u8);

            ImGui.TableNextColumn();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + checkBoxOffset);
            if (ImUtf8.Checkbox("##展开"u8, ref isExpanded))
                setterExpand.Invoke(isExpanded ? expanded | panel : expanded & ~panel);
            ImUtf8.HoverTooltip("在所有相关标签中默认展开此面板。"u8);
        }
    }
}
