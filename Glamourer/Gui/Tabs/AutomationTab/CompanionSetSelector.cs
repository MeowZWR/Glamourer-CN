using Glamourer.Automation;
using Glamourer.Config;
using ImSharp;
using Luna;
using Penumbra.GameData.Interop;

namespace Glamourer.Gui.Tabs.AutomationTab;

public sealed class CompanionSetSelector(
    CompanionAutoManager manager,
    AutomationSelection selection,
    Configuration config,
    ActorObjectManager objects) : IUiService
{
    private int _dragIndex = -1;

    public void Draw()
    {
        if (manager.Count is 0)
            return;

        Im.Cursor.X += Im.Style.FramePadding.X;
        Im.Text("宠物与召唤物"u8, ColorId.HeaderButtons.Vector);
        Im.Separator();

        var size = new Vector2(0, 2 * Im.Style.TextHeight + Im.Style.ItemSpacing.Y);
        for (var i = 0; i < manager.Count; ++i)
        {
            Im.Cursor.X += Im.Style.FramePadding.X;
            using var id    = Im.Id.Push(i);
            using var group = Im.Group();
            DrawSet(manager[i], i, size);
        }
    }

    private void DrawSet(CompanionAutoSet set, int index, Vector2 size)
    {
        var name = config.Ephemeral.IncognitoMode ? $"Companion Auto Design Set #{index + 1}" : set.Set.Name;
        using (ImGuiColor.Text.Push(set.Set.Enabled ? ColorId.EnabledAutoSet.Vector : ColorId.DisabledAutoSet.Vector))
        {
            if (Im.Selectable(name, set == selection.Companion, SelectableFlags.None, size))
                selection.UpdateCompanion(set, index);
        }

        var lineEnd   = Im.Item.LowerRightCorner;
        var lineStart = new Vector2(Im.Item.UpperLeftCorner.X, lineEnd.Y);
        Im.Window.DrawList.Shape.Line(lineStart, lineEnd, ImGuiColor.Border.Get(), Im.Style.GlobalScale);

        DrawDragDrop(set, index);

        var specific  = set.Targets.Count();
        var allPets   = set.Set.Designs.Any(d => set.GetLimit(d).Kind is CompanionLimitKind.AllPets);
        var available = objects.Keys.Any(id => manager.IsLocalPlayerCompanion(id) && set.Matches(id));
        var text = (set.Set.Designs.Count, allPets, specific) switch
        {
            (0, _, _)  => "无设计",
            (_, false, 0) => "请选择",
            (_, true, 0)  => "所有宠物",
            (_, true, _)  => $"所有宠物 + {specific}",
            _             => $"{specific} 个指定目标",
        };
        var textSize  = Im.Font.CalculateSize(text);
        Im.Cursor.Position = new Vector2(Im.ContentRegion.Available.X - textSize.X - Im.Style.FramePadding.X,
            Im.Cursor.Y - Im.Style.TextHeightWithSpacing);
        Im.Text(text, available ? ColorId.AutomationActorAvailable.Vector : ColorId.AutomationActorUnavailable.Vector);
    }

    private void DrawDragDrop(CompanionAutoSet set, int index)
    {
        using (var target = Im.DragDrop.Target())
        {
            if (target.IsDropping("CompanionSetDragDrop"u8))
            {
                if (_dragIndex >= 0)
                    manager.MoveSet(_dragIndex, index);
                _dragIndex = -1;
            }
        }

        using (var source = Im.DragDrop.Source())
        {
            if (!source)
                return;

            Im.Text($"移动来自第{index + 1}行的自动执行集 {set.Set.Name}...");
            if (source.SetPayload("CompanionSetDragDrop"u8))
                _dragIndex = index;
        }
    }
}
