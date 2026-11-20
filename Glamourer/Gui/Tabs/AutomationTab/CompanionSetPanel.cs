using Glamourer.Automation;
using Glamourer.Config;
using Glamourer.Designs;
using Glamourer.Designs.Special;
using ImSharp;
using Luna;

namespace Glamourer.Gui.Tabs.AutomationTab;

public sealed class CompanionSetPanel(
    CompanionAutoManager manager,
    AutoDesignManager autoDesigns,
    CompanionTargetCombo targetCombo,
    CompanionDesignCombo designCombo,
    RandomRestrictionDrawer randomDrawer,
    AutomationSelection selection,
    Configuration config) : IUiService
{
    private int     _dragIndex = -1;
    private Action? _endAction;

    public void Draw(CompanionAutoSet set)
    {
        using (Im.Group())
        {
            var enabled = set.Set.Enabled;
            if (Im.Checkbox("##Enabled"u8, ref enabled))
                manager.SetState(set, enabled);
            LunaStyle.DrawAlignedHelpMarkerLabel("启用"u8,
                "是否将该自动执行集中的设计应用到当前登录角色自己的宠物和召唤物上。\n"u8
              + "多个已启用的执行集包含同一目标时，排在前面的执行集生效。\n"u8
              + "宠物或召唤物会被变为人类，以便套用人类设计。可能在和他人同步时造成未知问题，请谨慎使用。"u8);

            var useGame = set.Set.BaseState is AutoDesignSet.Base.Game;
            if (Im.Checkbox("##gameState"u8, ref useGame))
                manager.ChangeBaseState(set, useGame ? AutoDesignSet.Base.Game : AutoDesignSet.Base.Current);
            LunaStyle.DrawAlignedHelpMarkerLabel("使用游戏状态作为基础"u8,
                "启用此选项后，设计将按顺序应用于游戏中角色的外观上。"u8
              + "禁用此选项后，设计将应用于角色当前被 Glamourer 修改后的实际外观上。"u8);
        }

        Im.Line.Same();
        using (Im.Group())
        {
            var resetSettings = set.Set.ResetTemporarySettings;
            if (Im.Checkbox("##resetSettings"u8, ref resetSettings))
                manager.ChangeResetSettings(set, resetSettings);
            LunaStyle.DrawAlignedHelpMarkerLabel("重置临时设置"u8,
                "每次应用此自动执行集时，始终重置由 Glamourer 应用的所有临时设置，无论当前设计是否激活。"u8);
        }

        Im.Dummy(Vector2.Zero);
        Im.Separator();
        Im.Dummy(Vector2.Zero);

        var flags = config.Ephemeral.IncognitoMode ? InputTextFlags.ReadOnly | InputTextFlags.Password : InputTextFlags.None;
        Im.Item.SetNextWidthScaled(330);
        if (ImEx.InputOnDeactivation.Text("重命名执行集##Name"u8, selection.Name, out string newName, default, flags))
        {
            manager.Rename(set, newName);
            selection.UpdateCompanion(set, manager.IndexOf(set));
        }

        Im.Dummy(Vector2.Zero);
        Im.Separator();
        Im.Dummy(Vector2.Zero);

        DrawDesignTable(set);
    }

    private void DrawDesignTable(CompanionAutoSet set)
    {
        using var table = Im.Table.Begin("CompanionSetTable"u8, 5, TableFlags.RowBackground | TableFlags.ScrollX | TableFlags.ScrollY);
        if (!table)
            return;

        table.SetupScrollFreeze(0, 2);
        table.SetupColumn("##del"u8,   TableColumnFlags.WidthFixed, Im.Style.FrameHeight);
        table.SetupColumn("##Index"u8, TableColumnFlags.WidthFixed, 30 * Im.Style.GlobalScale);
        table.SetupColumn("角色设计"u8, TableColumnFlags.WidthFixed, 220 * Im.Style.GlobalScale);
        if (config.ShowAllAutomatedApplicationRules)
            table.SetupColumn("执行规则"u8, TableColumnFlags.WidthFixed, 7 * Im.Style.FrameHeight + 12 * Im.Style.GlobalScale);
        else
            table.SetupColumn("使用"u8, TableColumnFlags.WidthFixed, Im.Font.CalculateSize("使用"u8).X);
        table.SetupColumn("宠物/召唤物限制"u8, TableColumnFlags.WidthStretch);
        table.HeaderRow();

        table.NextColumn();
        table.DrawFrameColumn($"#{set.Set.Designs.Count + 1:D2}");
        table.NextColumn();
        if (designCombo.Draw("##newDesign"u8, null, out var newDesign, Im.ContentRegion.Available.X) && newDesign is not null)
        {
            var design = newDesign;
            _endAction = () => manager.AddDesign(set, design);
        }

        table.DrawFrameColumn("添加新设计"u8);
        table.DrawFrameColumn(CompanionTargetCombo.UnsetLabel);

        var height = Im.Style.FrameHeight + 2 * Im.Style.CellPadding.Y;
        using (var clip = new Im.ListClipper(set.Set.Designs.Count, height))
        {
            foreach (var index in clip)
                DrawRow(table, set, index);
        }

        _endAction?.Invoke();
        _endAction = null;
    }

    private void DrawRow(in Im.TableDisposable table, CompanionAutoSet set, int index)
    {
        using var id     = Im.Id.Push(index);
        var       design = set.Set.Designs[index];

        table.NextColumn();
        var keyValid = config.DeleteDesignModifier.IsActive();
        if (ImEx.Icon.Button(LunaStyle.DeleteIcon, "从执行集中移除此设计。"u8, !keyValid))
        {
            var row = index;
            _endAction = () => autoDesigns.DeleteDesign(set.Set, row);
        }

        if (!keyValid)
            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"按住 {config.DeleteDesignModifier} 来移除此设计。");

        table.NextColumn();
        Im.Selectable($"#{index + 1:D2}");
        DrawDragDrop(set, index);

        table.NextColumn();
        if (design.Design is RandomDesign)
        {
            randomDrawer.DrawButton(set.Set, index);
            Im.Line.SameInner();
        }

        if (designCombo.Draw("##design"u8, design.Design, out var newDesign, Im.ContentRegion.Available.X) && newDesign is not null)
            _endAction = () => autoDesigns.ChangeDesign(set.Set, index, newDesign);
        DrawDragDrop(set, index);

        table.NextColumn();
        DrawApplicationTypeBoxes(set.Set, design, index);

        table.NextColumn();
        if (targetCombo.Draw("##target"u8, set.GetLimit(design), Im.ContentRegion.Available.X, out var newLimit))
            manager.ChangeTarget(set, design, newLimit);
    }

    private void DrawApplicationTypeBoxes(AutoDesignSet set, AutoDesign design, int index)
    {
        using var style   = ImStyleDouble.ItemSpacing.PushX(2 * Im.Style.GlobalScale);
        var       newType = CompanionDesignRules.Normalize(design.Type);
        using (ImStyleBorder.Frame.Push(ColorId.FolderLine.Vector))
        {
            Im.Checkbox("##all"u8, ref newType, ApplicationType.All & ~CompanionDesignRules.Forbidden);
        }

        Im.Tooltip.OnHover("一键开关"u8);
        if (config.ShowAllAutomatedApplicationRules)
            foreach (var type in ApplicationTypeExtensions.Types)
            {
                using var id        = Im.Id.Push((uint)type);
                var       required  = (CompanionDesignRules.Required & type) != 0;
                var       forbidden = (CompanionDesignRules.Forbidden & type) != 0;
                var       value     = required || (!forbidden && newType.HasFlag(type));
                Im.Line.Same();
                using (Im.Disabled(required || forbidden))
                {
                    if (Im.Checkbox(StringU8.Empty, ref value) && !required && !forbidden)
                        newType = value ? newType | type : newType & ~type;
                }

                if (required)
                    Im.Tooltip.OnHover("宠物和召唤物必须应用外貌。"u8);
                else if (forbidden)
                    Im.Tooltip.OnHover(type is ApplicationType.Weapons
                        ? "宠物和召唤物不能应用武器。"u8
                        : "宠物和召唤物不能应用 C+ 角色配置。"u8);
                else
                    Im.Tooltip.OnHover(type.Tooltip());
            }

        autoDesigns.ChangeApplicationType(set, index, CompanionDesignRules.Normalize(newType));
    }

    private void DrawDragDrop(CompanionAutoSet set, int index)
    {
        using (var target = Im.DragDrop.Target())
        {
            if (target.IsDropping("CompanionDesignDragDrop"u8))
            {
                if (_dragIndex >= 0)
                {
                    var from = _dragIndex;
                    _endAction = () => autoDesigns.MoveDesign(set.Set, from, index);
                }

                _dragIndex = -1;
            }
        }

        using (var source = Im.DragDrop.Source())
        {
            if (!source)
                return;

            Im.Text($"移动角色设计 #{index + 1:D2}...");
            if (source.SetPayload("CompanionDesignDragDrop"u8))
                _dragIndex = index;
        }
    }
}
