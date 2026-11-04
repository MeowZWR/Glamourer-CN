global using ModObjectIdentifier = (System.Guid Identifier, string? Name);
using Dalamud.Interface.ImGuiNotification;
using Glamourer.Config;
using Glamourer.Designs;
using Glamourer.Interop.Penumbra;
using Glamourer.State;
using ImSharp;
using Luna;
using Penumbra.Api.Preset;
using Penumbra.GameData.Gui;

namespace Glamourer.Gui.Tabs.DesignTab;

public sealed class ModAssociationsTab(PenumbraSubscriber penumbra, DesignFileSystem fileSystem, DesignManager manager, Configuration config)
    : IUiService
{
    private readonly ModCombo                              _modCombo = new(penumbra, fileSystem);
    private          (ModIdentifier, SettingPresetData)[]? _copy;

    private string _groupNameInput        = string.Empty;
    private string _optionNameInput       = string.Empty;
    private Guid   _groupIdentifierInput  = Guid.Empty;
    private Guid   _optionIdentifierInput = Guid.Empty;

    private Design Selection
        => (Design)fileSystem.Selection.Selection!.Value;

    public void Draw()
    {
        using var h = DesignPanelFlag.ModAssociations.Header(config);
        if (!h.Alive)
            return;

        Im.Tooltip.OnHover(
            "此面板可以存储与该设计关联的特定模组的信息。\n\n"u8
          + "它不会自动更改任何模组设置，尽管有手动应用所需模组设置的功能。\n"u8
          + "你也可以使用它跳转到关联模组的 Penumbra 页面。\n\n"u8
          + "在一般情况下，不太可能自动应用这些更改，因为没有办法恢复这些更改并同时处理多个生效的设计。"u8);
        if (!h)
            return;

        DrawApplyAllButton();
        DrawTable();
        DrawCopyButtons();
    }

    private void DrawCopyButtons()
    {
        var size = new Vector2((Im.ContentRegion.Available.X - 2 * Im.Style.ItemSpacing.X) / 3, 0);
        if (Im.Button("全部复制到剪贴板"u8, size))
            _copy = Selection.AssociatedMods.Select(kvp => (kvp.Key, kvp.Value)).ToArray();

        Im.Line.Same();

        if (ImEx.Button("从剪贴板添加"u8, size,
                _copy is not null
                    ? $"从剪贴板添加{_copy.Length}个模组关联。"
                    : "请先将一些模组关联复制到剪贴板。"u8, _copy is null))
            foreach (var (mod, setting) in _copy!)
                manager.UpdateMod(Selection, mod, setting);

        Im.Line.Same();

        if (ImEx.Button("从剪贴板设置"u8, size,
                _copy is not null
                    ? $"从剪贴板设置 {_copy.Length} 个模组关联并丢弃现有的。"
                    : "请先将一些模组关联复制到剪贴板。"u8, _copy is null))
        {
            while (Selection.AssociatedMods.Count > 0)
                manager.RemoveMod(Selection, Selection.AssociatedMods.Keys[0]);
            foreach (var (mod, setting) in _copy!)
                manager.AddMod(Selection, mod, setting);
        }
    }

    private void DrawApplyAllButton()
    {
        var (id, name, _) = penumbra.CurrentCollection;
        if (config.Ephemeral.IncognitoMode)
            name = id.ShortGuid();
        if (ImEx.Button($"尝试应用所有关联的模组到：{name}##applyAll",
                Im.ContentRegion.Available with { Y = 0 }, string.Empty, id == Guid.Empty))
            ApplyAll();
    }

    public void DrawApplyButton()
    {
        var (id, name, _) = penumbra.CurrentCollection;
        if (ImEx.Button("应用模组关联"u8, Vector2.Zero,
                $"尝试应用所有关联的模组设置到 Penumbra 当前的合集：{name}",
                Selection.AssociatedMods.Count is 0 || id == Guid.Empty))
            ApplyAll();
    }

    public void ApplyAll()
    {
        foreach (var (mod, settings) in Selection.AssociatedMods)
            penumbra.SetMod(mod, settings, StateSource.Manual, false);
    }

    private void DrawTable()
    {
        using var table = Im.Table.Begin("Mods"u8, 5, TableFlags.RowBackground);
        if (!table)
            return;

        table.SetupColumn("##Buttons"u8, TableColumnFlags.WidthFixed, Im.Style.FrameHeight * 3 + Im.Style.ItemInnerSpacing.X * 2);
        table.SetupColumn("模组名称"u8,  TableColumnFlags.WidthStretch);
        table.SetupColumn("状态"u8,     TableColumnFlags.WidthFixed, 85 * Im.Style.GlobalScale);
        table.SetupColumn("优先级"u8,  TableColumnFlags.WidthFixed, Im.Font.CalculateSize("优先级"u8).X + Im.Style.FrameHeightWithSpacing);
        table.SetupColumn("##Options"u8, TableColumnFlags.WidthFixed,
            Im.Font.CalculateSize("应用"u8).X + Im.Style.FrameHeight + Im.Style.ItemInnerSpacing.X);
        table.HeaderRow();

        ModIdentifier?                                   removedMod = null;
        (ModIdentifier mod, SettingPresetData settings)? updatedMod = null;
        foreach (var (idx, (mod, settings)) in Selection.AssociatedMods.Index())
        {
            using var id = Im.Id.Push(idx);
            DrawAssociatedModRow(table, mod, settings, out var removedModTmp, out var updatedModTmp);
            if (removedModTmp.HasValue)
                removedMod = removedModTmp;
            if (updatedModTmp.HasValue)
                updatedMod = updatedModTmp;
        }

        DrawNewModRow(table);

        if (removedMod.HasValue)
            manager.RemoveMod(Selection, removedMod.Value);

        if (updatedMod.HasValue)
            manager.UpdateMod(Selection, updatedMod.Value.mod, updatedMod.Value.settings);
    }

    private void DrawAssociatedModRow(in Im.TableDisposable table, ModIdentifier mod, SettingPresetData settings, out ModIdentifier? removedMod,
        out (ModIdentifier, SettingPresetData)? updatedMod)
    {
        removedMod = null;
        updatedMod = null;
        table.NextColumn();
        var canDelete = config.DeleteDesignModifier.IsActive();
        if (ImEx.Icon.Button(LunaStyle.DeleteIcon, "从关联中删除此模组。"u8, !canDelete))
            removedMod = mod;
        if (!canDelete)
            Im.Tooltip.OnHover($"\n按住{config.DeleteDesignModifier}来删除。");

        Im.Line.SameInner();
        if (ImEx.Icon.Button(LunaStyle.ToClipboardIcon, "复制这个模组设置到剪贴板。"u8))
            _copy = [(mod, settings)];

        Im.Line.SameInner();
        ImEx.Icon.Button(LunaStyle.RefreshIcon, "更新这个模组关联的设置。"u8);
        if (Im.Item.Hovered())
        {
            var newSettings = penumbra.GetModSettings(mod, out var source);
            if (Im.Item.Clicked())
                updatedMod = (mod, newSettings);

            using var style = ImStyleSingle.PopupBorderThickness.Push(2 * Im.Style.GlobalScale);
            using var tt    = Im.Tooltip.Begin();
            if (source.Length > 0)
                Im.Text($"使用由 {source} 创建的临时设置。");
            Im.Separator();
            var namesDifferent = mod.Name != mod.Identifier;
            Im.ScaledDummy(300);
            using (Im.Group())
            {
                if (namesDifferent)
                    Im.Text("目录名称"u8);
                Im.Text("状态"u8);
                Im.Text("优先级"u8);
                ModCombo.DrawSettingsLeft(newSettings);
            }

            Im.Line.Same(Math.Max(Im.Item.Size.X + 3 * Im.Style.ItemSpacing.X, 150 * Im.Style.GlobalScale));
            using (Im.Group())
            {
                if (namesDifferent)
                    Im.Text(mod.Identifier);
                Im.Text(newSettings.State.StringU8);
                Im.Text(newSettings._hasPriority ? $"{newSettings._priority}" : "忽略"u8);
                ModCombo.DrawSettingsRight(newSettings);
            }
        }

        table.NextColumn();

        if (Im.Selectable($"{mod.Name}##name"))
            penumbra.Ui.OpenMod(mod);
        Im.Tooltip.OnHover($"模组目录：    {mod.Identifier}\n\n点击以在 Penumbra 中打开模组页面。");

        table.NextColumn();
        if (settings.DrawState(Im.ContentRegion.Available with { Y = 0 }, out var newState))
            updatedMod = (mod, settings with { _state = (byte)newState });
        table.NextColumn();
        if (settings.DrawPriority(Im.ContentRegion.Available with { Y = 0 }, out var newPriority))
            updatedMod = (mod, settings with
            {
                _hasPriority = newPriority.HasValue,
                _priority = newPriority ?? 0,
            });
        table.NextColumn();
        var modIndex = !penumbra.Available ? -1 : penumbra.Mods.IndexByName(mod);
        if (ImEx.Button("应用"u8, Vector2.Zero, StringU8.Empty, modIndex < 0))
        {
            var text = penumbra.SetMod(mod, settings, StateSource.Manual, false);
            if (text.Length > 0)
                Glamourer.Messager.NotificationMessage(text, NotificationType.Warning, false);
        }

        DrawApplicationTooltip(modIndex, settings);
        Im.Line.SameInner();
        if (ImEx.Icon.Button(LunaStyle.EditIcon, "打开弹窗以编辑此预设的设置。"u8))
            Im.Popup.Open("##popup"u8);

        if (DrawPopup(mod, settings, out var updatedMod2))
            updatedMod = updatedMod2;
    }

    private bool DrawPopup(ModIdentifier mod, SettingPresetData settings, out (ModIdentifier, SettingPresetData)? updatedMod)
    {
        updatedMod = null;
        Im.Window.SetNextSizeConstraints(ImEx.ScaledVector(200), ImEx.ScaledVector(1000, 640));
        using var popup = Im.Popup.Begin("##popup"u8, WindowFlags.AlwaysAutoResize);
        if (!popup)
            return false;

        var                        indent       = Im.Style.FrameHeight + Im.Style.ItemInnerSpacing.X;
        var                        size         = ImEx.ScaledVectorX(400f, Im.Style.FrameHeight);
        var                        optionSize   = size.AddX(-indent);
        using var                  modReference = penumbra.Mods.GetByName(mod);
        using var                  id           = Im.Id.Empty();
        Action<SettingPresetData>? update       = null;
        ImEx.TextCentered($"正在编辑 {mod.Name} 的设置{(modReference is null ? "（未找到）" : string.Empty)}");
        Im.Line.Same(Im.ContentRegion.Maximum.X - Im.Style.FrameHeight);
        if (PenumbraOptionDisplay.DrawConfigButton(config.Ephemeral.PenumbraOptionDisplay, out var newDisplay))
            config.Ephemeral.PenumbraOptionDisplay = newDisplay;

        foreach (var (idx, (group, groupSettings)) in settings.Settings.Index())
        {
            id.Push(idx);
            var (actualGroupIndex, actualGroupId) = modReference?.FindGroup(group) ?? (-1, default);
            LunaStyle.DrawSeparator();

            if (SettingPresetData.DrawGroup(size, idx, group, actualGroupIndex >= 0 ? actualGroupId : null, groupSettings.DisableAllUnknown,
                    out var newGroup, out var disable, config.Ephemeral.PenumbraOptionDisplay))
                update = s =>
                {
                    if (disable.HasValue)
                        s.SetDisableUnknownOptions(group, disable.Value);
                    else if (newGroup.HasValue)
                        s.Settings.ReplaceGroupIdentifier(group, newGroup.Value);
                    else
                        s.Settings.Remove(group);
                };

            using var i = Im.Indent(indent);
            foreach (var (optionIndex, (optionIdentifier, optionState)) in groupSettings.Options.Index())
            {
                id.Push(optionIndex);
                var (actualOptionIndex, actualOptionIdentifier) = modReference?.FindOption(optionIdentifier, actualGroupIndex) ?? (-1, default);

                if (!config.Ephemeral.PenumbraOptionDisplay.CheckAll(PenumbraOptionDisplay.HideIndex
                      | PenumbraOptionDisplay.HideIds
                      | PenumbraOptionDisplay.HideResolve))
                    Im.Cursor.Y += Im.Style.ItemSpacing.Y;

                if (SettingPresetData.DrawOption(optionSize, optionIndex, optionIdentifier,
                        actualOptionIndex >= 0 ? actualOptionIdentifier : null,
                        (OptionState)optionState, out var changedOption, out var newState, config.Ephemeral.PenumbraOptionDisplay))
                    update = s =>
                    {
                        if (newState.HasValue)
                            groupSettings.Options[optionIdentifier] = (byte)newState;
                        else if (changedOption.HasValue)
                            s.Settings.ReplaceOptionIdentifiers(optionIdentifier, changedOption.Value, group);
                        else
                            groupSettings.Options.Remove(optionIdentifier);
                    };

                id.Pop();
            }

            if (!config.Ephemeral.PenumbraOptionDisplay.HasFlag(PenumbraOptionDisplay.HideAddOption))
                Im.Cursor.Y += Im.Style.ItemSpacing.Y;
            var currentNewOption = new ModObjectIdentifier(_optionIdentifierInput, _optionNameInput);
            if (SettingPresetData.DrawAddOption(optionSize, groupSettings, ref _optionIdentifierInput, ref _optionNameInput,
                    out var newOption, modReference?.FindOption(currentNewOption, actualGroupIndex).Option,
                    config.Ephemeral.PenumbraOptionDisplay))
                update = _ => groupSettings.Options.Add(newOption, (byte)OptionState.Ignored);

            id.Pop();
        }

        if (!config.Ephemeral.PenumbraOptionDisplay.HasFlag(PenumbraOptionDisplay.HideAddOption))
            LunaStyle.DrawSeparator();
        var currentNewGroup = new ModObjectIdentifier(_groupIdentifierInput, _groupNameInput);
        if (settings.DrawAddGroup(size, ref _groupIdentifierInput, ref _groupNameInput, out var newGroupIdentifier,
                modReference?.FindGroup(currentNewGroup).Group, config.Ephemeral.PenumbraOptionDisplay))
            update = s => s.AddGroup(newGroupIdentifier);

        if (update is null)
            return false;

        update.Invoke(settings);
        updatedMod = (mod, settings);
        return true;
    }

    private void DrawApplicationTooltip(int modIndex, in SettingPresetData settings)
    {
        if (!Im.Item.Hovered(HoveredFlags.AllowWhenDisabled))
            return;

        using var t = Im.Tooltip.Begin();
        if (modIndex < 0)
        {
            Im.Text("当前未安装与所存模组名称匹配的模组。"u8, LunaStyle.ErrorForeground);
            return;
        }

        using var collection = penumbra.Current;
        if (collection is null)
            Im.Text("未连接到 Penumbra。"u8);
        else if (collection.CanUnlock(modIndex, PenumbraSubscriber.KeyManual))
            collection.DrawPresetTooltip(modIndex, settings);
        else
            Im.Text($"匹配的模组已有由 {collection.GetTemporarySource(modIndex)} 创建的锁定临时设置。",
                LunaStyle.ErrorForeground);
    }

    private void DrawNewModRow(in Im.TableDisposable table)
    {
        var currentDir = _modCombo.Selection;
        table.NextColumn();
        var tt = currentDir.Length is 0
            ? "请先选择一个模组。"u8
            : Selection.AssociatedMods.ContainsKey(new ModIdentifier(currentDir, _modCombo.SelectionName))
                ? "此设计已经关联了选中的模组。"u8
                : StringU8.Empty;

        if (ImEx.Icon.Button(LunaStyle.AddObjectIcon, tt, tt.Length > 0))
            manager.AddMod(Selection, new ModIdentifier(_modCombo.Selection, _modCombo.SelectionName), _modCombo.Settings);
        table.NextColumn();
        _modCombo.Draw("##new"u8, Im.ContentRegion.Available.X);
    }
}
