using Glamourer.Config;
using Glamourer.Designs;
using Glamourer.Interop.Material;
using Glamourer.Services;
using ImSharp;
using Luna;
using Penumbra.GameData.Enums;

namespace Glamourer.Gui.Tabs.DesignTab;

public sealed class MultiDesignPanel(
    DesignFileSystem fileSystem,
    DesignManager editor,
    DesignColors colors,
    Configuration config,
    PredefinedTagManager predefinedTags) : IUiService
{
    private readonly DesignColorCombo _colorCombo = new(colors, true);

    public void Draw()
    {
        if (fileSystem.Selection.OrderedNodes.Count is 0)
            return;

        var treeNodePos = Im.Cursor.Position;
        DrawDesignList();
        DrawCounts(treeNodePos);
        var offset = DrawMultiTagger(out var width);
        DrawMultiColor(width, offset);
        DrawMultiQuickDesignBar(width, offset);
        DrawMultiLock(width, offset);
        DrawMultiResetSettings(width, offset);
        DrawMultiResetDyes(width, offset);
        DrawMultiForceRedraw(width, offset);
        DrawAdvancedButtons(offset);
        DrawApplicationButtons(width, offset);
    }

    private void DrawCounts(Vector2 treeNodePos)
    {
        var startPos   = Im.Cursor.Position;
        var numDesigns = fileSystem.Selection.DataNodes.Count;
        var numFolders = fileSystem.Selection.Folders.Count;
        Im.Cursor.Position = treeNodePos;
        ImEx.TextRightAligned((numDesigns, numFolders) switch
        {
            (0, 0)   => StringU8.Empty, // should not happen
            (> 0, 0) => $"{numDesigns} 个设计",
            (0, > 0) => $"{numFolders} 个折叠组",
            _        => $"{numDesigns} 个设计, {numFolders} 个折叠组",
        });
        Im.Cursor.Position = startPos;
    }

    private void ResetCounts()
    {
        _numQuickDesignEnabled      = 0;
        _numDesignsLocked           = 0;
        _numDesignsForcedRedraw     = 0;
        _numDesignsResetSettings    = 0;
        _numDesignsResetSomeDyes    = 0;
        _numDesignsResetAllDyes     = 0;
        _numDesignsWithAdvancedDyes = 0;
        _numAdvancedDyes            = 0;
    }

    private void CountLeaves(IFileSystemNode path)
    {
        if (path is not IFileSystemData<Design> l)
            return;

        if (l.Value.QuickDesign)
            ++_numQuickDesignEnabled;
        if (l.Value.WriteProtected())
            ++_numDesignsLocked;
        if (l.Value.ResetTemporarySettings)
            ++_numDesignsResetSettings;
        if (l.Value.ForcedRedraw)
            ++_numDesignsForcedRedraw;
        if (l.Value.ResetAdvancedDyes is not 0)
            ++_numDesignsResetSomeDyes;
        if (l.Value.ResetAdvancedDyes.HasFlag(EquipFlagExtensions.AllCombined))
            ++_numDesignsResetAllDyes;
        if (l.Value.Materials.Count > 0)
        {
            ++_numDesignsWithAdvancedDyes;
            _numAdvancedDyes += l.Value.Materials.Count;
        }
    }

    private void DrawDesignList()
    {
        ResetCounts();
        using var tree = Im.Tree.Node("当前选中的设计"u8, TreeNodeFlags.DefaultOpen | TreeNodeFlags.NoTreePushOnOpen);
        Im.Separator();
        if (!tree)
            return;

        var sizeType             = new Vector2(Im.Style.FrameHeight);
        var availableSizePercent = (Im.ContentRegion.Available.X - sizeType.X - 4 * Im.Style.CellPadding.X) / 100;
        var sizeMods             = availableSizePercent * 35;
        var sizeFolders          = availableSizePercent * 65;

        using (var table = Im.Table.Begin("mods"u8, 3, TableFlags.RowBackground))
        {
            if (!table)
                return;

            table.SetupColumn("type"u8, TableColumnFlags.WidthFixed, sizeType.X);
            table.SetupColumn("mod"u8,  TableColumnFlags.WidthFixed, sizeMods);
            table.SetupColumn("path"u8, TableColumnFlags.WidthFixed, sizeFolders);

            foreach (var (index, node) in fileSystem.Selection.OrderedNodes.Index())
            {
                using var id = Im.Id.Push(index);
                var (icon, text) = node is IFileSystemData<Design> l
                    ? (LunaStyle.RemoveFileIcon, l.Value.Name)
                    : (LunaStyle.RemoveFolderIcon, string.Empty);
                table.NextColumn();
                if (ImEx.Icon.Button(icon, "从选择中移除。"u8, sizeType))
                    fileSystem.Selection.RemoveFromSelection(node);

                table.DrawFrameColumn(text);
                table.DrawFrameColumn(node.FullPath);

                CountLeaves(node);
            }
        }

        Im.Separator();
    }

    private          string              _tag = string.Empty;
    private          int                 _numQuickDesignEnabled;
    private          int                 _numDesignsLocked;
    private          int                 _numDesignsForcedRedraw;
    private          int                 _numDesignsResetSettings;
    private          int                 _numDesignsResetSomeDyes;
    private          int                 _numDesignsResetAllDyes;
    private          int                 _numAdvancedDyes;
    private          int                 _numDesignsWithAdvancedDyes;
    private readonly List<Design>        _addDesigns    = [];
    private readonly List<(Design, int)> _removeDesigns = [];

    private float DrawMultiTagger(out Vector2 width)
    {
        ImEx.TextFrameAligned("批量标签："u8);
        Im.Line.Same();
        width  = new Vector2((Im.ContentRegion.Available.X - Im.Style.ItemInnerSpacing.X) / 2, 0);
        var offset = Im.Item.Size.X + Im.Style.WindowPadding.X;
        Im.Item.SetNextWidth(width.X);
        Im.Input.Text("##tag"u8, ref _tag, "标签名称..."u8);

        var buttonWidth = new Vector2((width.X - Im.Style.ItemInnerSpacing.X) / 2, 0);
        UpdateTagCache();
        Im.Line.SameInner();
        if (ImEx.Button(_addDesigns.Count > 0
                    ? $"添加到 {_addDesigns.Count} 个设计"
                    : "添加"u8, buttonWidth, _addDesigns.Count is 0
                    ? _tag.Length is 0
                        ? "未指定标签。"u8
                        : $"选中的设计已包含该标签：“{_tag}”。"
                    : $"将标签“{_tag}”添加到 {_addDesigns.Count} 个设计作为本地标签：\n\n\t{StringU8.Join("\n\t"u8, _addDesigns.Select(m => m.Name))}",
                _addDesigns.Count is 0))
            foreach (var design in _addDesigns)
                editor.AddTag(design, _tag);

        Im.Line.SameInner();
        if (predefinedTags.Enabled)
            buttonWidth.X -= Im.Style.ItemInnerSpacing.X + Im.Style.FrameHeight;

        if (ImEx.Button(_removeDesigns.Count > 0
                    ? $"从 {_removeDesigns.Count} 个设计移除"
                    : "移除"u8, buttonWidth, _removeDesigns.Count is 0
                    ? _tag.Length is 0
                        ? "未指定标签。"u8
                        : $"选中的设计不包含这个本地标签：“{_tag}”。"
                    : $"从 {_removeDesigns.Count} 个设计移除本地标签“{_tag}”：\n\n\t{string.Join("\n\t", _removeDesigns.Select(m => m.Item1.Name))}",
                _removeDesigns.Count is 0))
            foreach (var (design, index) in _removeDesigns)
                editor.RemoveTag(design, index);

        if (predefinedTags.Enabled)
        {
            Im.Line.SameInner();
            predefinedTags.DrawToggleButton();
            predefinedTags.DrawListMulti(fileSystem.Selection.DataNodes.Select(n => (Design)n.Value));
        }
        Im.Separator();
        return offset;
    }

    private void DrawMultiQuickDesignBar(Vector2 width, float offset)
    {
        ImEx.TextFrameAligned("批量快速设计栏："u8);
        Im.Line.Same(offset, Im.Style.ItemSpacing.X);
        var diff        = fileSystem.Selection.DataNodes.Count - _numQuickDesignEnabled;
        if (ImEx.Button("在快速设计栏中显示选中的设计"u8, width, diff is 0
                    ? $"全部 {fileSystem.Selection.DataNodes.Count} 个选中的设计已显示在快速设计栏中。"
                    : $"在快速设计栏中显示全部 {fileSystem.Selection.DataNodes.Count} 个选中的设计。影响 {diff} 个设计。",
                diff is 0))
            foreach (var design in fileSystem.Selection.DataNodes)
                editor.SetQuickDesign(design.GetValue<Design>()!, true);

        Im.Line.SameInner();
        if (ImEx.Button("在快速设计栏中隐藏选中的设计"u8, width, _numQuickDesignEnabled is 0
                    ? $"全部 {fileSystem.Selection.DataNodes.Count} 个选中的设计已隐藏在快速设计栏中。"
                    : $"在快速设计栏中隐藏全部 {fileSystem.Selection.DataNodes.Count} 个选中的设计。影响 {_numQuickDesignEnabled} 个设计。",
                _numQuickDesignEnabled is 0))
            foreach (var design in fileSystem.Selection.DataNodes)
                editor.SetQuickDesign(design.GetValue<Design>()!, false);

        Im.Separator();
    }

    private void DrawMultiLock(Vector2 width, float offset)
    {
        ImEx.TextFrameAligned("批量锁定："u8);
        Im.Line.Same(offset, Im.Style.ItemSpacing.X);
        var diff        = fileSystem.Selection.DataNodes.Count - _numDesignsLocked;
        if (ImEx.Button("启用写保护"u8, width, diff is 0
                ? $"全部 {fileSystem.Selection.DataNodes.Count} 个选中的设计已启用写保护。"
                : $"启用写保护全部 {fileSystem.Selection.DataNodes.Count} 个设计。影响 {diff} 个设计。", diff is 0))
            foreach (var design in fileSystem.Selection.DataNodes)
                editor.SetWriteProtection(design.GetValue<Design>()!, true);

        Im.Line.SameInner();
        if (ImEx.Button("移除写保护"u8, width, _numDesignsLocked is 0
                    ? $"全部 {fileSystem.Selection.DataNodes.Count} 个选中的设计均未启用写保护。"
                    : $"移除全部 {fileSystem.Selection.DataNodes.Count} 个选中的设计的写保护。影响 {_numDesignsLocked} 个设计。",
                _numDesignsLocked is 0))
            foreach (var design in fileSystem.Selection.DataNodes)
                editor.SetWriteProtection(design.GetValue<Design>()!, false);
        Im.Separator();
    }

    private void DrawMultiResetSettings(Vector2 width, float offset)
    {
        ImEx.TextFrameAligned("设置："u8);
        Im.Line.Same(offset, Im.Style.ItemSpacing.X);
        var diff        = fileSystem.Selection.DataNodes.Count - _numDesignsResetSettings;
        if (ImEx.Button("设置重置临时设置"u8, width, diff is 0
                    ? $"全部 {fileSystem.Selection.DataNodes.Count} 个选中的设计已重置临时设置。"
                    : $"设置全部 {fileSystem.Selection.DataNodes.Count} 个选中的设计重置临时设置。影响 {diff} 个设计。",
                diff is 0))
            foreach (var design in fileSystem.Selection.DataNodes)
                editor.ChangeResetTemporarySettings(design.GetValue<Design>()!, true);

        Im.Line.SameInner();
        if (ImEx.Button("移除重置临时设置"u8, width, _numDesignsResetSettings is 0
                    ? $"全部 {fileSystem.Selection.DataNodes.Count} 个选中的设计均未重置临时设置。"
                    : $"停止全部 {fileSystem.Selection.DataNodes.Count} 个选中的设计重置临时设置。影响 {_numDesignsResetSettings} 个设计。",
                _numDesignsResetSettings is 0))
            foreach (var design in fileSystem.Selection.DataNodes)
                editor.ChangeResetTemporarySettings(design.GetValue<Design>()!, false);
        Im.Separator();
    }

    private void DrawMultiResetDyes(Vector2 width, float offset)
    {
        ImEx.TextFrameAligned("高级染色："u8);
        Im.Line.Same(offset, Im.Style.ItemSpacing.X);
        var diff        = fileSystem.Selection.DataNodes.Count - _numDesignsResetAllDyes;
        if (ImEx.Button("设置重置染色"u8, width, diff is 0
                ? $"全部 {fileSystem.Selection.DataNodes.Count} 个选中的设计已重置高级染色。"
                : $"设置全部 {fileSystem.Selection.DataNodes.Count} 个选中的设计重置高级染色。影响 {diff} 个设计。", diff is 0))
            foreach (var design in fileSystem.Selection.DataNodes)
                editor.ChangeResetAdvancedDyes(design.GetValue<Design>()!, EquipFlagExtensions.AllCombined);

        Im.Line.SameInner();
        if (ImEx.Button("移除重置染色"u8, width, _numDesignsLocked is 0
                    ? $"全部 {fileSystem.Selection.DataNodes.Count} 个选中的设计均未重置高级染色。"
                    : $"停止全部 {fileSystem.Selection.DataNodes.Count} 个选中的设计重置高级染色。影响 {_numDesignsResetSomeDyes} 个设计。",
                _numDesignsResetSomeDyes is 0))
            foreach (var design in fileSystem.Selection.DataNodes)
                editor.ChangeResetAdvancedDyes(design.GetValue<Design>()!, 0);
        Im.Separator();
    }

    private void DrawMultiForceRedraw(Vector2 width, float offset)
    {
        ImEx.TextFrameAligned("重绘："u8);
        Im.Line.Same(offset, Im.Style.ItemSpacing.X);
        var diff        = fileSystem.Selection.DataNodes.Count - _numDesignsForcedRedraw;
        if (ImEx.Button("启用强制重绘"u8, width, diff is 0
                ? $"全部 {fileSystem.Selection.DataNodes.Count} 个选中的设计已强制重绘。"
                : $"为全部 {fileSystem.Selection.DataNodes.Count} 个设计方案启用强制重绘。影响 {diff} 个设计。", diff is 0))
            foreach (var design in fileSystem.Selection.DataNodes)
                editor.ChangeForcedRedraw(design.GetValue<Design>()!, true);

        Im.Line.SameInner();
        if (ImEx.Button("移除强制重绘"u8, width, _numDesignsLocked is 0
                    ? $"全部 {fileSystem.Selection.DataNodes.Count} 个选中的设计均未强制重绘。"
                    : $"停止全部 {fileSystem.Selection.DataNodes.Count} 个选中的设计强制重绘。影响 {_numDesignsForcedRedraw} 个设计。",
                _numDesignsForcedRedraw is 0))
            foreach (var design in fileSystem.Selection.DataNodes)
                editor.ChangeForcedRedraw(design.GetValue<Design>()!, false);
        Im.Separator();
    }

    private string _colorComboSelection = string.Empty;

    private void DrawMultiColor(Vector2 width, float offset)
    {
        ImEx.TextFrameAligned("批量配色："u8);
        Im.Line.Same(offset, Im.Style.ItemSpacing.X);
        if (_colorCombo.Draw("##color"u8, _colorComboSelection, "选择一个设计颜色。"u8, width.X, out var newSelection))
            _colorComboSelection = newSelection;

        var buttonWidth = new Vector2((width.X - Im.Style.ItemInnerSpacing.X) / 2, 0); 
        UpdateColorCache();
        Im.Line.SameInner();
        if (ImEx.Button(_addDesigns.Count > 0
                    ? $"设置 {_addDesigns.Count} 个设计"
                    : "设置"u8, buttonWidth, _addDesigns.Count is 0
                    ? _colorComboSelection switch
                    {
                        null                       => "未指定颜色。"u8,
                        DesignColors.AutomaticName => "使用另一个按钮设置为自动配色。"u8,
                        _                          => $"所选的所有设计都已设置为该颜色：“{_colorComboSelection}”。",
                    }
                    : $"将 {_addDesigns.Count} 个设计的颜色设置为“{_colorComboSelection}”：\n\n\t{StringU8.Join("\n\t"u8, _addDesigns.Select(m => m.Name))}",
                _addDesigns.Count is 0))
            foreach (var design in _addDesigns)
                editor.ChangeColor(design, _colorComboSelection!);

        Im.Line.SameInner();
        if (ImEx.Button(_removeDesigns.Count > 0
                    ? $"取消设置 {_removeDesigns.Count} 个设计"
                    : "取消设置"u8, buttonWidth, _removeDesigns.Count is 0
                    ? "未选中设计设置为非自动配色。"u8
                    : $"将 {_removeDesigns.Count} 个设计重新使用自动配色：\n\n\t{StringU8.Join("\n\t"u8, _removeDesigns.Select(m => m.Item1.Name))}",
                _removeDesigns.Count is 0))
            foreach (var (design, _) in _removeDesigns)
                editor.ChangeColor(design, string.Empty);

        Im.Separator();
    }

    private void DrawAdvancedButtons(float offset)
    {
        ImEx.TextFrameAligned("删除高级染色"u8);
        Im.Line.Same(offset, Im.Style.ItemSpacing.X);
        var enabled = config.DeleteDesignModifier.IsActive();
        if (ImEx.Button("删除所有高级染色"u8, Im.ContentRegion.Available with { Y = 0 }, _numDesignsWithAdvancedDyes is 0
                    ? "选中的设计中不包含任何高级染色。"u8
                    : $"从 {_numDesignsWithAdvancedDyes} 个选中的设计中删除 {_numAdvancedDyes} 个高级染色。",
                !enabled || _numDesignsWithAdvancedDyes is 0))

            foreach (var design in fileSystem.Selection.DataNodes)
            {
                while (design.GetValue<Design>()!.Materials.Count > 0)
                    editor.ChangeMaterialValue(design.GetValue<Design>()!,
                        MaterialValueIndex.FromKey(design.GetValue<Design>()!.Materials[0].Item1), null);
            }

        if (!enabled && _numDesignsWithAdvancedDyes is not 0)
            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"按住 {config.DeleteDesignModifier} 点击删除。");
        Im.Separator();
    }

    private void DrawApplicationButtons(Vector2 width, float offset)
    {
        ImEx.TextFrameAligned("应用规则"u8);
        Im.Line.Same(offset, Im.Style.ItemSpacing.X);
        var   enabled   = config.DeleteDesignModifier.IsActive();
        bool? equip     = null;
        bool? customize = null;
        using (Im.Group())
        {
            if (ImEx.Button("禁用所有"u8, width,
                    fileSystem.Selection.DataNodes.Count > 0
                        ? $"禁用所有内容的应用，包括所有 {fileSystem.Selection.DataNodes.Count} 个设计的任何现有高级染色、高级外貌、队徽和湿身。"
                        : "未选择设计。"u8, !enabled))
            {
                equip     = false;
                customize = false;
            }

            if (!enabled)
                Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"按住 {config.DeleteDesignModifier} 点击。");

            Im.Line.SameInner();
            if (ImEx.Button("启用所有"u8, width,
                    fileSystem.Selection.DataNodes.Count > 0
                        ? $"启用所有内容的应用，包括所有 {fileSystem.Selection.DataNodes.Count} 个设计的任何现有高级染色、高级外貌、队徽和湿身。"
                        : "未选择设计。"u8, !enabled))
            {
                equip     = true;
                customize = true;
            }

            if (!enabled)
                Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"按住 {config.DeleteDesignModifier} 点击。");

            if (ImEx.Button("仅装备"u8, width,
                    fileSystem.Selection.DataNodes.Count > 0
                        ? $"启用与装备相关的所有应用，禁用与装备无关的所有应用。"
                        : "未选择设计。"u8, !enabled))
            {
                equip     = true;
                customize = false;
            }

            if (!enabled)
                Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"按住 {config.DeleteDesignModifier} 点击。");

            Im.Line.SameInner();
            if (ImEx.Button("仅外貌"u8, width,
                    fileSystem.Selection.DataNodes.Count > 0
                        ? $"启用与外貌相关的所有应用，禁用与外貌无关的所有应用。"
                        : "未选择设计。"u8, !enabled))
            {
                equip     = false;
                customize = true;
            }

            if (!enabled)
                Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"按住 {config.DeleteDesignModifier} 点击。");

            if (ImEx.Button("默认应用"u8, width,
                    fileSystem.Selection.DataNodes.Count > 0
                        ? $"将应用规则设置为默认值，就像设计是新创建的一样，没有任何高级功能或湿身效果。"
                        : "未选择设计。"u8, !enabled))
                foreach (var design in fileSystem.Selection.DataNodes.Select(l => l.GetValue<Design>()!))
                {
                    editor.ChangeApplyMulti(design, true, true, true, false, true, true, false, true);
                    editor.ChangeApplyMeta(design, MetaIndex.Wetness, false);
                }

            if (!enabled)
                Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"按住 {config.DeleteDesignModifier} 点击。");

            Im.Line.SameInner();
            if (ImEx.Button("禁用高级"u8, width, fileSystem.Selection.DataNodes.Count > 0
                    ? $"禁用所有高级染色和外貌，但保留其他所有设置。"
                    : "未选择设计。"u8, !enabled))
                foreach (var design in fileSystem.Selection.DataNodes.Select(l => l.GetValue<Design>()!))
                    editor.ChangeApplyMulti(design, null, null, null, false, null, null, false, null);

            if (!enabled)
                Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"按住 {config.DeleteDesignModifier} 点击。");
        }

        Im.Separator();
        if (equip is null && customize is null)
            return;

        foreach (var design in fileSystem.Selection.DataNodes.Select(l => l.GetValue<Design>()!))
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
        if (_tag.Length is 0)
            return;

        foreach (var leaf in fileSystem.Selection.DataNodes)
        {
            var design = leaf.GetValue<Design>()!;
            var index  = design.Tags.AsEnumerable().IndexOf(_tag);
            if (index >= 0)
                _removeDesigns.Add((design, index));
            else
                _addDesigns.Add(design);
        }
    }

    private void UpdateColorCache()
    {
        _addDesigns.Clear();
        _removeDesigns.Clear();
        var selection = string.IsNullOrEmpty(_colorComboSelection) ? DesignColors.AutomaticName : _colorComboSelection;
        foreach (var leaf in fileSystem.Selection.DataNodes)
        {
            var design = leaf.GetValue<Design>()!;
            if (design.Color.Length > 0)
                _removeDesigns.Add((design, 0));
            if (selection != DesignColors.AutomaticName && design.Color != selection)
                _addDesigns.Add(design);
        }
    }
}
