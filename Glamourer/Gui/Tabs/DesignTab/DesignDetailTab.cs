using Dalamud.Interface.ImGuiNotification;
using Glamourer.Config;
using Glamourer.Designs;
using Glamourer.Gui.Tabs.SettingsTab;
using Glamourer.Services;
using ImSharp;
using Luna;

namespace Glamourer.Gui.Tabs.DesignTab;

public sealed class DesignDetailTab : IUiService
{
    private readonly SaveService          _saveService;
    private readonly Configuration        _config;
    private readonly DesignFileSystem     _fileSystem;
    private readonly DesignManager        _manager;
    private readonly DesignColors         _colors;
    private readonly DesignColorCombo     _colorCombo;
    private readonly PredefinedTagManager _predefinedTags;

    private bool _editDescriptionMode;

    public DesignDetailTab(SaveService saveService, DesignManager manager, DesignFileSystem fileSystem,
        DesignColors colors, Configuration config, PredefinedTagManager predefinedTags)
    {
        _saveService    = saveService;
        _manager        = manager;
        _fileSystem     = fileSystem;
        _colors         = colors;
        _config         = config;
        _predefinedTags = predefinedTags;
        _colorCombo     = new DesignColorCombo(_colors, false);
    }

    public void Draw()
    {
        using var h = DesignPanelFlag.DesignDetails.Header(_config);
        if (!h)
            return;

        DrawDesignInfoTable();
        DrawDescription();
        Im.Line.New();
    }

    private Design Selected
        => (Design)_fileSystem.Selection.Selection!.Value;

    private void DrawDesignInfoTable()
    {
        using var style = ImStyleDouble.ButtonTextAlign.Push(new Vector2(0, 0.5f));
        using var table = Im.Table.Begin("详细信息"u8, 2);
        if (!table)
            return;

        table.SetupColumn("类型"u8, TableColumnFlags.WidthFixed, Im.Font.CalculateSize("重置临时设置"u8).X);
        table.SetupColumn("数据"u8, TableColumnFlags.WidthStretch);

        table.DrawFrameColumn("设计名称"u8);
        table.NextColumn();
        var width = Im.ContentRegion.Available with { Y = 0 };
        Im.Item.SetNextWidth(width.X);
        if (ImEx.InputOnDeactivation.Text("##Name"u8, Selected.Name, out string newName))
            _manager.Rename(Selected, newName);

        var identifier = Selected.Identifier.ToString();
        table.DrawFrameColumn("唯一标识符"u8);
        table.NextColumn();
        var fileName = _saveService.FileNames.DesignFile(Selected);
        using (Im.Font.PushMono())
        {
            if (Im.Button(identifier, width))
                try
                {
                    Process.Start(new ProcessStartInfo(fileName) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    Glamourer.Messager.NotificationMessage(ex, $"无法打开文件 {fileName} 。", $"无法打开文件 {fileName}",
                        NotificationType.Warning);
                }

            if (Im.Item.RightClicked())
                Im.Clipboard.Set(identifier);
        }

        Im.Tooltip.OnHover(
            $"打开此文件：\n\t{fileName}\n在您选择的.json编辑器中包控制此设计。\n\n右键单击可将标识符复制到剪贴板。");

        table.DrawFrameColumn("完整选择器路径"u8);
        table.NextColumn();
        Im.Item.SetNextWidth(width.X);
        if (ImEx.InputOnDeactivation.Text("##Path"u8, Selected.Path.CurrentPath, out string newPath))
            try
            {
                _fileSystem.RenameAndMove(Selected.Node!, newPath);
            }
            catch (Exception ex)
            {
                Glamourer.Messager.NotificationMessage(ex, ex.Message, "无法重命名或移动设计", NotificationType.Error);
            }

        table.DrawFrameColumn("快速设计栏"u8);
        table.NextColumn();
        if (Im.RadioButton("显示##qdb"u8, Selected.QuickDesign))
            _manager.SetQuickDesign(Selected, true);
        var hovered = Im.Item.Hovered();
        Im.Line.SameInner();
        if (Im.RadioButton("隐藏##qdb"u8, !Selected.QuickDesign))
            _manager.SetQuickDesign(Selected, false);
        if (hovered || Im.Item.Hovered())
            Im.Tooltip.Set("在快速设计栏中显示或隐藏此设计。"u8);

        var forceRedraw = Selected.ForcedRedraw;
        table.DrawFrameColumn("强制重绘"u8);
        table.NextColumn();
        if (Im.Checkbox("##ForceRedraw"u8, ref forceRedraw))
            _manager.ChangeForcedRedraw(Selected, forceRedraw);
        Im.Tooltip.OnHover("设置此设计在以任何方式应用时，均会强制重新绘制。"u8);

        var resetAdvancedDyes = Selected.ResetAdvancedDyes;
        table.DrawFrameColumn("重置高级染色"u8);
        Im.Line.SameInner();
        LunaStyle.DrawAlignedHelpMarker(
            "设置此设计在以任何方式应用时，均会重置之前应用的高级染色。\n\n如果此设计是设计链接或自动执行集的一部分，设置此选项的行为与设置重置所有高级染色不同：\n- 设置此选项会重置之前应用的所有高级染色，但允许较低的设计应用高级染色。\n- 设置重置所有高级染色会重置之前应用的所有高级染色，并阻止较低的设计应用高级染色。"u8);
        table.NextColumn();
        if (UiHelpers.DrawItemSlots("##ResetAdvancedDyes"u8, ref resetAdvancedDyes))
            _manager.ChangeResetAdvancedDyes(Selected, resetAdvancedDyes);

        var resetTemporarySettings = Selected.ResetTemporarySettings;
        table.DrawFrameColumn("重置临时设置"u8);
        table.NextColumn();
        if (Im.Checkbox("##ResetTemporarySettings"u8, ref resetTemporarySettings))
            _manager.ChangeResetTemporarySettings(Selected, resetTemporarySettings);
        Im.Tooltip.OnHover(
            "设置此设计在以任何方式应用时，均会重置之前应用到关联合集中的所有临时设置。"u8);

        table.DrawFrameColumn("配色"u8);
        table.NextColumn();
        if (_colorCombo.Draw("##colorCombo"u8, Selected.Color.Length is 0 ? DesignColors.AutomaticName : Selected.Color,
                "将颜色与此设计相关联。\n"u8
              + "右键单击可恢复为自动配色。\n"u8
              + "按住键盘Ctrl键并滚动鼠标滚轮进行滚动选择。"u8,
                width.X - Im.Style.ItemSpacing.X - Im.Style.FrameHeight, out var newColorName))
            _manager.ChangeColor(Selected, newColorName == DesignColors.AutomaticName ? string.Empty : newColorName);

        if (Im.Item.RightClicked())
            _manager.ChangeColor(Selected, string.Empty);

        if (_colors.TryGetValue(Selected.Color, out var currentColor))
        {
            Im.Line.Same();
            if (DesignColorUi.DrawColorButton($"与此设计相关联的颜色 {Selected.Color}", currentColor, out var newColor))
                _colors.SetColor(Selected.Color, newColor);
        }
        else if (Selected.Color.Length is not 0)
        {
            Im.Line.Same();
            ImEx.Icon.Draw(LunaStyle.WarningIcon, _colors.MissingColor);
            Im.Tooltip.OnHover("与此设计相关联的颜色不存在。"u8);
        }

        table.DrawFrameColumn("创建日期"u8);
        table.NextColumn();
        ImEx.TextFramed($"{Selected.CreationDate.LocalDateTime:F}", width, 0);

        table.DrawFrameColumn("最后更新日期"u8);
        table.NextColumn();
        ImEx.TextFramed($"{Selected.LastEdit.LocalDateTime:F}", width, 0);

        table.DrawFrameColumn("标签"u8);
        table.NextColumn();
        DrawTags();
    }

    private void DrawTags()
    {
        var predefinedTagButtonOffset = _predefinedTags.Enabled
            ? Im.Style.FrameHeight + Im.Style.WindowPadding.X + (Im.Scroll.MaximumY > 0 ? Im.Style.ScrollbarSize : 0)
            : 0;
        var idx = TagButtons.Draw(StringU8.Empty, StringU8.Empty, Selected.Tags, out var editedTag, rightEndOffset: predefinedTagButtonOffset);
        if (_predefinedTags.Enabled)
            _predefinedTags.DrawAddFromSharedTagsAndUpdateTags(Selected, true);

        if (idx < 0)
            return;

        if (idx < Selected.Tags.Length)
        {
            if (editedTag.Length is 0)
                _manager.RemoveTag(Selected, idx);
            else
                _manager.RenameTag(Selected, idx, editedTag);
        }
        else
        {
            _manager.AddTag(Selected, editedTag);
        }
    }

    private void DrawDescription()
    {
        var desc = Selected.Description;
        var size = Im.ContentRegion.Available with { Y = 12 * Im.Style.TextHeightWithSpacing };
        if (!_editDescriptionMode)
        {
            using (var textBox = Im.ListBox.Begin("##desc"u8, size))
            {
                if (textBox)
                    Im.TextWrapped(desc);
            }

            if (Im.Button("编辑描述"u8))
                _editDescriptionMode = true;
        }
        else
        {
            if (ImEx.InputOnDeactivation.MultiLine("##desc"u8, desc, out string newDescription, size))
                _manager.ChangeDescription(Selected, newDescription);

            if (Im.Button("停止编辑"u8))
                _editDescriptionMode = false;
        }
    }
}
