using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Glamourer.Designs;
using Glamourer.Interop.Material;
using ImGuiNET;
using OtterGui.Extensions;
using OtterGui.Raii;
using OtterGui.Text;
using static Glamourer.Gui.Tabs.HeaderDrawer;

namespace Glamourer.Gui.Tabs.DesignTab;

public class MultiDesignPanel(
    DesignFileSystemSelector selector,
    DesignManager editor,
    DesignColors colors,
    Configuration config)
{
    private readonly Button[] _leftButtons  = [];
    private readonly Button[] _rightButtons = [new IncognitoButton(config)];

    private readonly DesignColorCombo _colorCombo = new(colors, true);

    public void Draw()
    {
        if (selector.SelectedPaths.Count == 0)
            return;

        HeaderDrawer.Draw(string.Empty, 0, ImGui.GetColorU32(ImGuiCol.FrameBg), _leftButtons, _rightButtons);
        using var child = ImUtf8.Child("##MultiPanel"u8, default, true);
        if (!child)
            return;

        var width       = ImGuiHelpers.ScaledVector2(145, 0);
        var treeNodePos = ImGui.GetCursorPos();
        _numDesigns = DrawDesignList();
        DrawCounts(treeNodePos);
        var offset = DrawMultiTagger(width);
        DrawMultiColor(width, offset);
        DrawMultiQuickDesignBar(offset);
        DrawMultiLock(offset);
        DrawMultiResetSettings(offset);
        DrawMultiResetDyes(offset);
        DrawMultiForceRedraw(offset);
        DrawAdvancedButtons(offset);
        DrawApplicationButtons(offset);
    }

    private void DrawCounts(Vector2 treeNodePos)
    {
        var startPos   = ImGui.GetCursorPos();
        var numFolders = selector.SelectedPaths.Count - _numDesigns;
        var text = (_numDesigns, numFolders) switch
        {
            (0, 0)   => string.Empty, // should not happen
            (> 0, 0) => $"{_numDesigns} 个设计",
            (0, > 0) => $"{numFolders} 个折叠组",
            _        => $"{_numDesigns} 个设计, {numFolders} 个折叠组",
        };
        ImGui.SetCursorPos(treeNodePos);
        ImUtf8.TextRightAligned(text);
        ImGui.SetCursorPos(startPos);
    }

    private void ResetCounts()
    {
        _numQuickDesignEnabled      = 0;
        _numDesignsLocked           = 0;
        _numDesignsForcedRedraw     = 0;
        _numDesignsResetSettings    = 0;
        _numDesignsResetDyes        = 0;
        _numDesignsWithAdvancedDyes = 0;
        _numAdvancedDyes            = 0;
    }

    private bool CountLeaves(DesignFileSystem.IPath path)
    {
        if (path is not DesignFileSystem.Leaf l)
            return false;

        if (l.Value.QuickDesign)
            ++_numQuickDesignEnabled;
        if (l.Value.WriteProtected())
            ++_numDesignsLocked;
        if (l.Value.ResetTemporarySettings)
            ++_numDesignsResetSettings;
        if (l.Value.ForcedRedraw)
            ++_numDesignsForcedRedraw;
        if (l.Value.ResetAdvancedDyes)
            ++_numDesignsResetDyes;
        if (l.Value.Materials.Count > 0)
        {
            ++_numDesignsWithAdvancedDyes;
            _numAdvancedDyes += l.Value.Materials.Count;
        }

        return true;
    }

    private int DrawDesignList()
    {
        ResetCounts();
        using var tree = ImUtf8.TreeNode("当前选中的对象"u8, ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.NoTreePushOnOpen);
        ImGui.Separator();
        if (!tree)
            return selector.SelectedPaths.Count(CountLeaves);

        var sizeType             = new Vector2(ImGui.GetFrameHeight());
        var availableSizePercent = (ImGui.GetContentRegionAvail().X - sizeType.X - 4 * ImGui.GetStyle().CellPadding.X) / 100;
        var sizeMods             = availableSizePercent * 35;
        var sizeFolders          = availableSizePercent * 65;

        var numDesigns = 0;
        using (var table = ImUtf8.Table("mods"u8, 3, ImGuiTableFlags.RowBg))
        {
            if (!table)
                return selector.SelectedPaths.Count(l => l is DesignFileSystem.Leaf);

            ImUtf8.TableSetupColumn("type"u8, ImGuiTableColumnFlags.WidthFixed, sizeType.X);
            ImUtf8.TableSetupColumn("mod"u8,  ImGuiTableColumnFlags.WidthFixed, sizeMods);
            ImUtf8.TableSetupColumn("path"u8, ImGuiTableColumnFlags.WidthFixed, sizeFolders);

            var i = 0;
            foreach (var (fullName, path) in selector.SelectedPaths.Select(p => (p.FullName(), p))
                         .OrderBy(p => p.Item1, StringComparer.OrdinalIgnoreCase))
            {
                using var id = ImRaii.PushId(i++);
                var (icon, text) = path is DesignFileSystem.Leaf l
                    ? (FontAwesomeIcon.FileCircleMinus, l.Value.Name.Text)
                    : (FontAwesomeIcon.FolderMinus, string.Empty);
                ImGui.TableNextColumn();
                if (ImUtf8.IconButton(icon, "从选择中移除。"u8, sizeType))
                    selector.RemovePathFromMultiSelection(path);

                ImUtf8.DrawFrameColumn(text);
                ImUtf8.DrawFrameColumn(fullName);

                if (CountLeaves(path))
                    ++numDesigns;
            }
        }

        ImGui.Separator();
        return numDesigns;
    }

    private          string              _tag = string.Empty;
    private          int                 _numQuickDesignEnabled;
    private          int                 _numDesignsLocked;
    private          int                 _numDesignsForcedRedraw;
    private          int                 _numDesignsResetSettings;
    private          int                 _numDesignsResetDyes;
    private          int                 _numAdvancedDyes;
    private          int                 _numDesignsWithAdvancedDyes;
    private          int                 _numDesigns;
    private readonly List<Design>        _addDesigns    = [];
    private readonly List<(Design, int)> _removeDesigns = [];

    private float DrawMultiTagger(Vector2 width)
    {
        ImUtf8.TextFrameAligned("批量标签："u8);
        ImGui.SameLine();
        var offset = ImGui.GetItemRectSize().X + ImGui.GetStyle().WindowPadding.X;
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 2 * (width.X + ImGui.GetStyle().ItemSpacing.X));
        ImUtf8.InputText("##tag"u8, ref _tag, "标签名称..."u8);

        UpdateTagCache();
        var label = _addDesigns.Count > 0
            ? $"添加到{_addDesigns.Count}个设计"
            : "添加";
        var tooltip = _addDesigns.Count == 0
            ? _tag.Length == 0
                ? "未指定标签。"
                : $"所选的所有设计都已包含该标记：\"{_tag}\"."
            : $"添加本地标签“{_tag}”到{_addDesigns.Count}个设计：\n\n\t{string.Join("\n\t", _addDesigns.Select(m => m.Name.Text))}";
        ImGui.SameLine();
        if (ImUtf8.ButtonEx(label, tooltip, width, _addDesigns.Count == 0))
            foreach (var design in _addDesigns)
                editor.AddTag(design, _tag);

        label = _removeDesigns.Count > 0
            ? $"从{_removeDesigns.Count}个设计移除"
            : "移除";
        tooltip = _removeDesigns.Count == 0
            ? _tag.Length == 0
                ? "未指定标签。"
                : $"选中的设计不包含这个本地标签：“{_tag}”。"
            : $"从{_removeDesigns.Count}个设计移除本地标签“{_tag}”：\n\n\t{string.Join("\n\t", _removeDesigns.Select(m => m.Item1.Name.Text))}";
        ImGui.SameLine();
        if (ImUtf8.ButtonEx(label, tooltip, width, _removeDesigns.Count == 0))
            foreach (var (design, index) in _removeDesigns)
                editor.RemoveTag(design, index);
        ImGui.Separator();
        return offset;
    }

    private void DrawMultiQuickDesignBar(float offset)
    {
        ImUtf8.TextFrameAligned("批量快速设计栏："u8);
        ImGui.SameLine(offset, ImGui.GetStyle().ItemSpacing.X);
        var buttonWidth = new Vector2((ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X) / 2, 0);
        var diff        = _numDesigns - _numQuickDesignEnabled;
        var tt = diff == 0
            ? $"当前所有{_numDesigns}个选中设计方案已在快速设计栏显示"
            : $"将为全部{_numDesigns}个选中设计方案启用快速设计栏显示（影响{diff}个设计）";
        if (ImUtf8.ButtonEx("在快速设计栏中显示选中的设计"u8, tt, buttonWidth, diff == 0))
        {
            foreach (var design in selector.SelectedPaths.OfType<DesignFileSystem.Leaf>())
                editor.SetQuickDesign(design.Value, true);
        }

        ImGui.SameLine();
        tt = _numQuickDesignEnabled == 0
            ? $"当前所有{_numDesigns}个选中设计方案未在快速设计栏显示"
            : $"将为全部{_numDesigns}个选中设计方案关闭快速设计栏显示（影响{_numQuickDesignEnabled}个设计）";
        if (ImUtf8.ButtonEx("在快速设计栏中隐藏选中的设计"u8, tt, buttonWidth, _numQuickDesignEnabled == 0))
        {
            foreach (var design in selector.SelectedPaths.OfType<DesignFileSystem.Leaf>())
                editor.SetQuickDesign(design.Value, false);
        }

        ImGui.Separator();
    }

    private void DrawMultiLock(float offset)
    {
        ImUtf8.TextFrameAligned("批量锁定："u8);
        ImGui.SameLine(offset, ImGui.GetStyle().ItemSpacing.X);
        var buttonWidth = new Vector2((ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X) / 2, 0);
        var diff        = _numDesigns - _numDesignsLocked;
        var tt = diff == 0
            ? $"所有{_numDesigns}个选中方案已启用写保护"
            : $"为全部{_numDesigns}个设计方案启用写保护（影响{diff}个设计）";
        if (ImUtf8.ButtonEx("启用写保护"u8, tt, buttonWidth, diff == 0))
            foreach (var design in selector.SelectedPaths.OfType<DesignFileSystem.Leaf>())
                editor.SetWriteProtection(design.Value, true);

        ImGui.SameLine();
        tt = _numDesignsLocked == 0
            ? $"当前{_numDesigns}个选中方案均未启用写保护"
            : $"移除全部{_numDesigns}个选中方案的写保护（影响{_numDesignsLocked}个设计）";
        if (ImUtf8.ButtonEx("移除写保护"u8, tt, buttonWidth, _numDesignsLocked == 0))
            foreach (var design in selector.SelectedPaths.OfType<DesignFileSystem.Leaf>())
                editor.SetWriteProtection(design.Value, false);
        ImGui.Separator();
    }

    private void DrawMultiResetSettings(float offset)
    {
        ImUtf8.TextFrameAligned("设置："u8);
        ImGui.SameLine(offset, ImGui.GetStyle().ItemSpacing.X);
        var buttonWidth = new Vector2((ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X) / 2, 0);
        var diff        = _numDesigns - _numDesignsResetSettings;
        var tt = diff == 0
            ? $"所有{_numDesigns}个选中方案已设置重置临时设置"
            : $"为全部{_numDesigns}个设计方案启用临时设置重置（影响{diff}个设计）";
        if (ImUtf8.ButtonEx("设置重置临时设置"u8, tt, buttonWidth, diff == 0))
            foreach (var design in selector.SelectedPaths.OfType<DesignFileSystem.Leaf>())
                editor.ChangeResetTemporarySettings(design.Value, true);

        ImGui.SameLine();
        tt = _numDesignsResetSettings == 0
            ? $"当前{_numDesigns}个选中方案均未配置重置临时设置"
            : $"取消全部{_numDesigns}个方案的临时设置重置（影响{_numDesignsResetSettings}个设计）";
        if (ImUtf8.ButtonEx("取消重置临时设置"u8, tt, buttonWidth, _numDesignsResetSettings == 0))
            foreach (var design in selector.SelectedPaths.OfType<DesignFileSystem.Leaf>())
                editor.ChangeResetTemporarySettings(design.Value, false);
        ImGui.Separator();
    }

    private void DrawMultiResetDyes(float offset)
    {
        ImUtf8.TextFrameAligned("高级染色："u8);
        ImGui.SameLine(offset, ImGui.GetStyle().ItemSpacing.X);
        var buttonWidth = new Vector2((ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X) / 2, 0);
        var diff        = _numDesigns - _numDesignsResetDyes;
        var tt = diff == 0
            ? $"所有{_numDesigns}个选中方案已设置重置高级染色"
            : $"为全部{_numDesigns}个设计方案启用高级染色重置（影响{diff}个设计）";
        if (ImUtf8.ButtonEx("设置重置染色"u8, tt, buttonWidth, diff == 0))
            foreach (var design in selector.SelectedPaths.OfType<DesignFileSystem.Leaf>())
                editor.ChangeResetAdvancedDyes(design.Value, true);

        ImGui.SameLine();
        tt = _numDesignsLocked == 0
            ? $"当前{_numDesigns}个选中方案均未设置重置染色"
            : $"取消全部{_numDesigns}个方案的高级染色重置（影响{_numDesignsResetDyes}个设计）";
        if (ImUtf8.ButtonEx("取消重置染色"u8, tt, buttonWidth, _numDesignsResetDyes == 0))
            foreach (var design in selector.SelectedPaths.OfType<DesignFileSystem.Leaf>())
                editor.ChangeResetAdvancedDyes(design.Value, false);
        ImGui.Separator();
    }

    private void DrawMultiForceRedraw(float offset)
    {
        ImUtf8.TextFrameAligned("强制重绘："u8);
        ImGui.SameLine(offset, ImGui.GetStyle().ItemSpacing.X);
        var buttonWidth = new Vector2((ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X) / 2, 0);
        var diff        = _numDesigns - _numDesignsForcedRedraw;
        var tt = diff == 0
            ? $"所有{_numDesigns}个选中方案已启用强制重绘"
            : $"为全部{_numDesigns}个设计方案启用强制重绘（影响{diff}个设计）";
        if (ImUtf8.ButtonEx("强制重绘"u8, tt, buttonWidth, diff == 0))
            foreach (var design in selector.SelectedPaths.OfType<DesignFileSystem.Leaf>())
                editor.ChangeForcedRedraw(design.Value, true);

        ImGui.SameLine();
        tt = _numDesignsLocked == 0
            ? $"当前{_numDesigns}个选中方案均未启用强制重绘"
            : $"取消全部{_numDesigns}个方案的强制重绘（影响{_numDesignsForcedRedraw}个设计）";
        if (ImUtf8.ButtonEx("取消强制重绘"u8, tt, buttonWidth, _numDesignsForcedRedraw == 0))
            foreach (var design in selector.SelectedPaths.OfType<DesignFileSystem.Leaf>())
                editor.ChangeForcedRedraw(design.Value, false);
        ImGui.Separator();
    }

    private void DrawMultiColor(Vector2 width, float offset)
    {
        ImUtf8.TextFrameAligned("批量配色："u8);
        ImGui.SameLine(offset, ImGui.GetStyle().ItemSpacing.X);
        _colorCombo.Draw("##color", _colorCombo.CurrentSelection ?? string.Empty, "选择一个设计颜色。",
            ImGui.GetContentRegionAvail().X - 2 * (width.X + ImGui.GetStyle().ItemSpacing.X), ImGui.GetTextLineHeight());

        UpdateColorCache();
        var label = _addDesigns.Count > 0
            ? $"设置{_addDesigns.Count}个设计"
            : "设置";
        var tooltip = _addDesigns.Count == 0
            ? _colorCombo.CurrentSelection switch
            {
                null                       => "未指定颜色。",
                DesignColors.AutomaticName => "使用另一个按钮设置为自动配色。",
                _                          => $"所选的所有设计都已设置为该颜色“{_colorCombo.CurrentSelection}”。",
            }
            : $"将 {_addDesigns.Count} 个的颜色设置为“{_colorCombo.CurrentSelection}”\n\n\t{string.Join("\n\t", _addDesigns.Select(m => m.Name.Text))}";
        ImGui.SameLine();
        if (ImUtf8.ButtonEx(label, tooltip, width, _addDesigns.Count == 0))
        {
            foreach (var design in _addDesigns)
                editor.ChangeColor(design, _colorCombo.CurrentSelection!);
        }

        label = _removeDesigns.Count > 0
            ? $"取消设置{_removeDesigns.Count}个设计"
            : "取消设置";
        tooltip = _removeDesigns.Count == 0
            ? "没有选中设计设置为非自动配色。"
            : $"设置 {_removeDesigns.Count} 个设计为重新使用自动配色：\n\n\t{string.Join("\n\t", _removeDesigns.Select(m => m.Item1.Name.Text))}";
        ImGui.SameLine();
        if (ImUtf8.ButtonEx(label, tooltip, width, _removeDesigns.Count == 0))
        {
            foreach (var (design, _) in _removeDesigns)
                editor.ChangeColor(design, string.Empty);
        }

        ImGui.Separator();
    }

    private void DrawAdvancedButtons(float offset)
    {
        ImUtf8.TextFrameAligned("删除高级染色"u8);
        ImGui.SameLine(offset, ImGui.GetStyle().ItemSpacing.X);
        var enabled = config.DeleteDesignModifier.IsActive();
        var tt = _numDesignsWithAdvancedDyes is 0
            ? "选中的设计中不包含任何高级染色。"
            : $"从 {_numDesignsWithAdvancedDyes} 个选中的设计中删除 {_numAdvancedDyes} 个高级染色。";
        if (ImUtf8.ButtonEx("删除所有高级染料"u8, tt, new Vector2(ImGui.GetContentRegionAvail().X, 0),
                !enabled || _numDesignsWithAdvancedDyes is 0))

            foreach (var design in selector.SelectedPaths.OfType<DesignFileSystem.Leaf>())
            {
                while (design.Value.Materials.Count > 0)
                    editor.ChangeMaterialValue(design.Value, MaterialValueIndex.FromKey(design.Value.Materials[0].Item1), null);
            }

        if (!enabled && _numDesignsWithAdvancedDyes is not 0)
            ImUtf8.HoverTooltip(ImGuiHoveredFlags.AllowWhenDisabled, $"点击时按住 {config.DeleteDesignModifier} 。");
        ImGui.Separator();
    }

    private void DrawApplicationButtons(float offset)
    {
        ImUtf8.TextFrameAligned("应用规则"u8);
        ImGui.SameLine(offset, ImGui.GetStyle().ItemSpacing.X);
        var   width     = new Vector2((ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X) / 2, 0);
        var   enabled   = config.DeleteDesignModifier.IsActive();
        bool? equip     = null;
        bool? customize = null;
        var   group     = ImUtf8.Group();
        if (ImUtf8.ButtonEx("禁用所有"u8,
                _numDesigns > 0
                    ? $"禁用所有内容的应用，包括所有 {_numDesigns} 个设计的任何现有高级染色、高级外貌、队徽和湿身。"
                    : "未选择设计。", width, !enabled))
        {
            equip     = false;
            customize = false;
        }

        if (!enabled)
            ImUtf8.HoverTooltip(ImGuiHoveredFlags.AllowWhenDisabled, $"点击时按住 {config.DeleteDesignModifier}。");

        ImGui.SameLine();
        if (ImUtf8.ButtonEx("启用所有"u8,
                _numDesigns > 0
                    ? $"启用所有内容的应用，包括所有 {_numDesigns} 个设计的任何现有高级染色、高级自定义、队徽和湿身。"
                    : "未选择设计。", width, !enabled))
        {
            equip     = true;
            customize = true;
        }

        if (!enabled)
            ImUtf8.HoverTooltip(ImGuiHoveredFlags.AllowWhenDisabled, $"点击时按住 {config.DeleteDesignModifier}。");

        if (ImUtf8.ButtonEx("仅装备"u8,
                _numDesigns > 0
                    ? $"启用与装备相关的所有内容的应用，禁用所有与装备无关的内容的应用，适用于所有 {_numDesigns} 个设计。"
                    : "未选择设计。", width, !enabled))
        {
            equip     = true;
            customize = false;
        }

        if (!enabled)
            ImUtf8.HoverTooltip(ImGuiHoveredFlags.AllowWhenDisabled, $"点击时按住 {config.DeleteDesignModifier}。");

        ImGui.SameLine();
        if (ImUtf8.ButtonEx("仅外貌"u8,
                _numDesigns > 0
                    ? $"启用与自定义相关的所有内容的应用，禁用所有与自定义无关的内容的应用，适用于所有 {_numDesigns} 个设计。"
                    : "未选择设计。", width, !enabled))
        {
            equip     = false;
            customize = true;
        }

        if (!enabled)
            ImUtf8.HoverTooltip(ImGuiHoveredFlags.AllowWhenDisabled, $"点击时按住 {config.DeleteDesignModifier}。");

        if (ImUtf8.ButtonEx("默认应用"u8,
                _numDesigns > 0
                    ? $"将应用规则设置为默认值，就像 {_numDesigns} 个设计是新创建的一样，没有任何高级功能或湿身。"
                    : "未选择设计。", width, !enabled))
            foreach (var design in selector.SelectedPaths.OfType<DesignFileSystem.Leaf>().Select(l => l.Value))
            {
                editor.ChangeApplyMulti(design, true, true, true, false, true, true, false, true);
                editor.ChangeApplyMeta(design, MetaIndex.Wetness, false);
            }

        if (!enabled)
            ImUtf8.HoverTooltip(ImGuiHoveredFlags.AllowWhenDisabled, $"点击时按住 {config.DeleteDesignModifier}。");

        ImGui.SameLine();
        if (ImUtf8.ButtonEx("禁用高级"u8, _numDesigns > 0
                ? $"禁用所有高级染色和高级外貌，但保留所有其他内容的应用，适用于所有 {_numDesigns} 个设计。"
                : "未选择设计。", width, !enabled))
            foreach (var design in selector.SelectedPaths.OfType<DesignFileSystem.Leaf>().Select(l => l.Value))
                editor.ChangeApplyMulti(design, null, null, null, false, null, null, false, null);

        if (!enabled)
            ImUtf8.HoverTooltip(ImGuiHoveredFlags.AllowWhenDisabled, $"点击时按住 {config.DeleteDesignModifier}。");

        group.Dispose();
        ImGui.Separator();
        if (equip is null && customize is null)
            return;

        foreach (var design in selector.SelectedPaths.OfType<DesignFileSystem.Leaf>().Select(l => l.Value))
        {
            editor.ChangeApplyMulti(design, equip, customize, equip, customize.HasValue && !customize.Value ? false : null, null, equip, equip,
                equip);
            if (equip.HasValue)
            {
                editor.ChangeApplyMeta(design, MetaIndex.HatState,    equip.Value);
                editor.ChangeApplyMeta(design, MetaIndex.VisorState,  equip.Value);
                editor.ChangeApplyMeta(design, MetaIndex.WeaponState, equip.Value);
            }

            if (customize.HasValue)
                editor.ChangeApplyMeta(design, MetaIndex.Wetness, customize.Value);
        }
    }

    private void UpdateTagCache()
    {
        _addDesigns.Clear();
        _removeDesigns.Clear();
        if (_tag.Length == 0)
            return;

        foreach (var leaf in selector.SelectedPaths.OfType<DesignFileSystem.Leaf>())
        {
            var index = leaf.Value.Tags.IndexOf(_tag);
            if (index >= 0)
                _removeDesigns.Add((leaf.Value, index));
            else
                _addDesigns.Add(leaf.Value);
        }
    }

    private void UpdateColorCache()
    {
        _addDesigns.Clear();
        _removeDesigns.Clear();
        var selection = _colorCombo.CurrentSelection ?? DesignColors.AutomaticName;
        foreach (var leaf in selector.SelectedPaths.OfType<DesignFileSystem.Leaf>())
        {
            if (leaf.Value.Color.Length > 0)
                _removeDesigns.Add((leaf.Value, 0));
            if (selection != DesignColors.AutomaticName && leaf.Value.Color != selection)
                _addDesigns.Add(leaf.Value);
        }
    }
}
