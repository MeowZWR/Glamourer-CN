using Dalamud.Interface.ImGuiNotification;
using Glamourer.Config;
using Glamourer.Designs;
using Glamourer.Interop.Penumbra;
using Glamourer.State;
using ImSharp;
using Luna;

namespace Glamourer.Gui.Tabs.DesignTab;

public sealed class ModAssociationsTab(PenumbraService penumbra, DesignFileSystem fileSystem, DesignManager manager, Configuration config) : IUiService
{
    private readonly ModCombo              _modCombo = new(penumbra, fileSystem);
    private          (Mod, ModSettings)[]? _copy;

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
          + "你也可以使用它快速打开关联的模组页面在 Penumbra 中。\n\n"u8
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
        var (id, name) = penumbra.CurrentCollection;
        if (config.Ephemeral.IncognitoMode)
            name = id.ShortGuid();
        if (ImEx.Button($"尝试应用所有关联的模组到：{name}##applyAll",
                Im.ContentRegion.Available with { Y = 0 }, string.Empty, id == Guid.Empty))
            ApplyAll();
    }

    public void DrawApplyButton()
    {
        var (id, name) = penumbra.CurrentCollection;
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
        using var table = Im.Table.Begin("Mods"u8, config.UseTemporarySettings ? 7 : 6, TableFlags.RowBackground);
        if (!table)
            return;

        table.SetupColumn("##Buttons"u8, TableColumnFlags.WidthFixed, Im.Style.FrameHeight * 3 + Im.Style.ItemInnerSpacing.X * 2);
        table.SetupColumn("模组名称"u8,  TableColumnFlags.WidthStretch);
        if (config.UseTemporarySettings)
            table.SetupColumn("移除"u8, TableColumnFlags.WidthFixed, Im.Font.CalculateSize("移除"u8).X);
        table.SetupColumn("继承"u8,   TableColumnFlags.WidthFixed, Im.Font.CalculateSize("继承"u8).X);
        table.SetupColumn("状态"u8,     TableColumnFlags.WidthFixed, Im.Font.CalculateSize("状态"u8).X);
        table.SetupColumn("优先级"u8,  TableColumnFlags.WidthFixed, Im.Font.CalculateSize("优先级"u8).X);
        table.SetupColumn("##Options"u8, TableColumnFlags.WidthFixed, Im.Font.CalculateSize("应用"u8).X);
        table.HeaderRow();

        Mod?                             removedMod = null;
        (Mod mod, ModSettings settings)? updatedMod = null;
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

    private void DrawAssociatedModRow(in Im.TableDisposable table, Mod mod, ModSettings settings, out Mod? removedMod,
        out (Mod, ModSettings)? updatedMod)
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
            var namesDifferent = mod.Name != mod.DirectoryName;
            Im.Dummy(300 * Im.Style.GlobalScale);
            using (Im.Group())
            {
                if (namesDifferent)
                    Im.Text("目录名称"u8);
                Im.Text("强制继承"u8);
                Im.Text("已启用"u8);
                Im.Text("优先级"u8);
                ModCombo.DrawSettingsLeft(newSettings);
            }

            Im.Line.Same(Math.Max(Im.Item.Size.X + 3 * Im.Style.ItemSpacing.X, 150 * Im.Style.GlobalScale));
            using (Im.Group())
            {
                if (namesDifferent)
                    Im.Text(mod.DirectoryName);

                Im.Text($"{newSettings.ForceInherit}");
                Im.Text($"{newSettings.Enabled}");
                Im.Text($"{newSettings.Priority}");
                ModCombo.DrawSettingsRight(newSettings);
            }
        }

        table.NextColumn();

        if (Im.Selectable($"{mod.Name}##name"))
            penumbra.OpenModPage(mod);
        Im.Tooltip.OnHover($"模组目录：    {mod.DirectoryName}\n\n点击以在 Penumbra 中打开模组页面。");
        if (config.UseTemporarySettings)
        {
            table.NextColumn();
            var remove = settings.Remove;
            if (ImEx.TwoStateCheckbox("##Remove"u8, ref remove))
                updatedMod = (mod, settings with { Remove = remove });
            Im.Tooltip.OnHover(
                "移除由 Glamourer 应用的任何临时设置，而不是应用已配置的设置。仅在使用临时设置时有效，否则会被忽略。"u8);
        }

        table.NextColumn();
        var inherit = settings.ForceInherit;
        if (ImEx.TwoStateCheckbox("##ForceInherit"u8, ref inherit))
            updatedMod = (mod, settings with { ForceInherit = inherit });
        Im.Tooltip.OnHover("强制模组从继承的合集中继承其设置。"u8);
        table.NextColumn();
        var enabled = settings.Enabled;
        if (ImEx.TwoStateCheckbox("##Enabled"u8, ref enabled))
            updatedMod = (mod, settings with { Enabled = enabled });

        table.NextColumn();
        var priority = settings.Priority;
        Im.Item.SetNextWidthFull();
        if (ImEx.InputOnDeactivation.Scalar("##Priority"u8, ref priority))
            updatedMod = (mod, settings with { Priority = priority });
        table.NextColumn();
        if (ImEx.Button("应用"u8, Im.ContentRegion.Available with { Y = 0 }, StringU8.Empty, !penumbra.Available))
        {
            var text = penumbra.SetMod(mod, settings, StateSource.Manual, false);
            if (text.Length > 0)
                Glamourer.Messager.NotificationMessage(text, NotificationType.Warning, false);
        }

        DrawAssociatedModTooltip(settings);
    }

    private static void DrawAssociatedModTooltip(ModSettings settings)
    {
        if (settings is not { Enabled: true, Settings.Count: > 0 } || !Im.Item.Hovered())
            return;

        using var t = Im.Tooltip.Begin();
        Im.Text("还将尝试将以下设置也应用到当前合集："u8);

        Im.Line.New();
        using (Im.Group())
        {
            ModCombo.DrawSettingsLeft(settings);
        }

        Im.Line.Same(Im.ContentRegion.Available.X / 2);
        using (Im.Group())
        {
            ModCombo.DrawSettingsRight(settings);
        }
    }

    private void DrawNewModRow(in Im.TableDisposable table)
    {
        var currentDir = _modCombo.Selection;
        table.NextColumn();
        var tt = currentDir.Length is 0
            ? "请先选择一个模组。"u8
            : Selection.AssociatedMods.ContainsKey(new Mod(_modCombo.SelectionName, currentDir))
                ? "此设计已经关联了选中的模组。"u8
                : StringU8.Empty;

        if (ImEx.Icon.Button(LunaStyle.AddObjectIcon, tt, tt.Length > 0))
            manager.AddMod(Selection, new Mod(_modCombo.SelectionName, _modCombo.Selection), _modCombo.Settings);
        table.NextColumn();
        _modCombo.Draw("##new"u8, Im.ContentRegion.Available.X);
    }
}
