using Dalamud.Interface;
using Glamourer.Automation;
using Glamourer.Config;
using Glamourer.Designs;
using Glamourer.Designs.Links;
using ImSharp;
using Luna;

namespace Glamourer.Gui.Tabs.DesignTab;

public class DesignLinkDrawer(
    DesignLinkManager linkManager,
    DesignFileSystem fileSystem,
    LinkDesignCombo combo,
    DesignColors colorManager,
    Configuration config) : IUiService
{
    private int       _dragDropIndex       = -1;
    private LinkOrder _dragDropOrder       = LinkOrder.None;
    private int       _dragDropTargetIndex = -1;
    private LinkOrder _dragDropTargetOrder = LinkOrder.None;

    private Design Selected
        => (Design)fileSystem.Selection.Selection!.Value;

    public void Draw()
    {
        using var h = DesignPanelFlag.DesignLinks.Header(config);
        if (!h.Alive)
            return;

        Im.Tooltip.OnHover(
            "设计链接是指向其他设计的链接，这些设计将根据规则直接或通过自动执行应用于角色。\n"u8
          + "它们从上到下生效，就像自动执行里的一样，所以前面的设计设置的任何内容都不会被后面的设计再次设置，顺序很重要。\n"u8
          + "如果已链接设计被链接到其他设计，它们也将被应用，因此禁止循环链接。"u8);
        if (!h)
            return;

        DrawList();
    }

    private void MoveLink()
    {
        if (_dragDropTargetIndex < 0 || _dragDropIndex < 0)
            return;

        if (_dragDropOrder is LinkOrder.Self)
            switch (_dragDropTargetOrder)
            {
                case LinkOrder.Before:
                    for (var i = Selected.Links.Before.Count - 1; i >= _dragDropTargetIndex; --i)
                        linkManager.MoveDesignLink(Selected, i, LinkOrder.Before, 0, LinkOrder.After);
                    break;
                case LinkOrder.After:
                    for (var i = 0; i <= _dragDropTargetIndex; ++i)
                    {
                        linkManager.MoveDesignLink(Selected, 0, LinkOrder.After, Selected.Links.Before.Count,
                            LinkOrder.Before);
                    }

                    break;
            }
        else if (_dragDropTargetOrder is LinkOrder.Self)
            linkManager.MoveDesignLink(Selected, _dragDropIndex, _dragDropOrder, Selected.Links.Before.Count,
                LinkOrder.Before);
        else
            linkManager.MoveDesignLink(Selected, _dragDropIndex, _dragDropOrder, _dragDropTargetIndex, _dragDropTargetOrder);

        _dragDropIndex       = -1;
        _dragDropTargetIndex = -1;
        _dragDropOrder       = LinkOrder.None;
        _dragDropTargetOrder = LinkOrder.None;
    }

    private void DrawList()
    {
        using var table = Im.Table.Begin("table"u8, 3, TableFlags.RowBackground | TableFlags.BordersOuter);
        if (!table)
            return;

        table.SetupColumn("Del"u8,  TableColumnFlags.WidthFixed, Im.Style.FrameHeight);
        table.SetupColumn("Name"u8, TableColumnFlags.WidthStretch);
        table.SetupColumn("Detail"u8, TableColumnFlags.WidthFixed,
            6 * Im.Style.FrameHeight + 5 * Im.Style.ItemInnerSpacing.X);

        using var style = ImStyleDouble.ItemSpacing.Push(Im.Style.ItemInnerSpacing);
        DrawSubList(table, Selected.Links.Before, LinkOrder.Before);
        DrawSelf(table);
        DrawSubList(table, Selected.Links.After, LinkOrder.After);
        DrawNew(table);
        MoveLink();
    }

    private void DrawSelf(in Im.TableDisposable table)
    {
        using var id = Im.Id.Push((int)LinkOrder.Self);
        table.NextColumn();
        var color = colorManager.GetColor(Selected);
        using (AwesomeIcon.Font.Push())
        {
            using var c = ImGuiColor.Text.Push(color);
            Im.Cursor.FrameAlign();
            ImEx.TextRightAligned(FontAwesomeIcon.ArrowRightLong.Icon().Span);
        }

        table.NextColumn();
        using (ImGuiColor.Text.Push(color))
        {
            Im.Cursor.FrameAlign();
            Im.Selectable(config.Ephemeral.IncognitoMode ? Selected.Incognito : Selected.Name);
        }

        Im.Tooltip.OnHover("当前设计"u8);
        DrawDragDrop(Selected, LinkOrder.Self, 0);
        table.NextColumn();
        using (AwesomeIcon.Font.Push())
        {
            using var c = ImGuiColor.Text.Push(color);
            Im.Cursor.FrameAlign();
            ImEx.TextRightAligned(FontAwesomeIcon.ArrowLeftLong.Icon().Span);
        }
    }

    private void DrawSubList(in Im.TableDisposable table, IReadOnlyList<DesignLink> list, LinkOrder order)
    {
        using var id = Im.Id.Push((int)order);

        for (var i = 0; i < list.Count; ++i)
        {
            id.Push(i);

            table.NextColumn();
            var delete = ImEx.Icon.Button(LunaStyle.DeleteIcon, "删除此链接。"u8);
            var (design, flags) = list[i];
            table.NextColumn();

            using (ImGuiColor.Text.Push(colorManager.GetColor(design)))
            {
                Im.Cursor.FrameAlign();
                Im.Selectable(config.Ephemeral.IncognitoMode ? design.Incognito : design.Name);
            }

            DrawDragDrop(design, order, i);

            table.NextColumn();
            Im.Cursor.FrameAlign();
            DrawApplicationBoxes(i, order, flags);

            if (delete)
                linkManager.RemoveDesignLink(Selected, i--, order);
        }
    }

    private void DrawNew(in Im.TableDisposable table)
    {
        table.NextColumn();
        table.NextColumn();
        combo.Draw(StringU8.Empty, Im.ContentRegion.Available.X);
        table.NextColumn();
        string ttBefore,     ttAfter;
        bool   canAddBefore, canAddAfter;
        var    design = combo.NewSelection;
        if (design is null)
        {
            ttAfter      = ttBefore    = "请先选中一个设计";
            canAddBefore = canAddAfter = false;
        }
        else
        {
            canAddBefore = LinkContainer.CanAddLink(Selected, design, LinkOrder.Before, out var error);
            ttBefore = canAddBefore
                ? $"将{design.Name}链接到上方。"
                : $"不能为{design.Name}添加链接：\n{error}";
            canAddAfter = LinkContainer.CanAddLink(Selected, design, LinkOrder.After, out error);
            ttAfter = canAddAfter
                ? $"将{design.Name}链接到下方。"
                : $"不能为{design.Name}添加链接：\n{error}";
        }

        if (ImEx.Icon.Button(FontAwesomeIcon.ArrowCircleUp.Icon(), ttBefore, !canAddBefore))
        {
            linkManager.AddDesignLink(Selected, design!, LinkOrder.Before);
            linkManager.MoveDesignLink(Selected, Selected.Links.Before.Count - 1, LinkOrder.Before, 0, LinkOrder.Before);
        }

        Im.Line.Same();
        if (ImEx.Icon.Button(FontAwesomeIcon.ArrowCircleDown.Icon(), ttAfter, !canAddAfter))
            linkManager.AddDesignLink(Selected, design!, LinkOrder.After);
    }

    private void DrawDragDrop(Design design, LinkOrder order, int index)
    {
        using (var source = Im.DragDrop.Source())
        {
            if (source)
            {
                source.SetPayload("DraggingLink"u8);
                Im.Text($"Reordering {design.Name}...");
                _dragDropIndex = index;
                _dragDropOrder = order;
            }
        }

        using var target = Im.DragDrop.Target();
        if (!target.IsDropping("DraggingLink"u8))
            return;

        _dragDropTargetIndex = index;
        _dragDropTargetOrder = order;
    }

    private void DrawApplicationBoxes(int idx, LinkOrder order, ApplicationType current)
    {
        var newType = current;
        using (ImStyleBorder.Frame.Push(ColorId.FolderLine.Value()))
        {
            Im.Checkbox("##all"u8, ref newType, ApplicationType.All);
        }

        Im.Tooltip.OnHover("应用规则总开关"u8);

        Im.Line.Same();
        Box(0);
        Im.Line.Same();
        Box(1);
        Im.Line.Same();

        Box(2);
        Im.Line.Same();
        Box(3);
        Im.Line.Same();
        Box(4);
        if (newType != current)
            linkManager.ChangeApplicationType(Selected, idx, order, newType);
        return;

        void Box(int i)
        {
            var applicationType = ApplicationTypeExtensions.Types[i];
            using var id    = Im.Id.Push((uint)applicationType);
            var       value = current.HasFlag(applicationType);
            if (Im.Checkbox(StringU8.Empty, ref value))
                newType = value ? newType | applicationType : newType & ~applicationType;
            Im.Tooltip.OnHover(applicationType.Tooltip());
        }
    }
}
