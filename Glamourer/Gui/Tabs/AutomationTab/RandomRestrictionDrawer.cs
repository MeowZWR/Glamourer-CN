using Dalamud.Interface;
using Glamourer.Automation;
using Glamourer.Config;
using Glamourer.Designs;
using Glamourer.Designs.Special;
using Glamourer.Events;
using ImSharp;
using Luna;

namespace Glamourer.Gui.Tabs.AutomationTab;

public sealed class RandomRestrictionDrawer : IService, IDisposable
{
    private AutoDesignSet? _set;
    private int            _designIndex = -1;

    private readonly AutomationChanged   _automationChanged;
    private readonly Configuration       _config;
    private readonly AutoDesignManager   _autoDesignManager;
    private readonly RandomDesignCombo   _randomDesignCombo;
    private readonly AutomationSelection _selection;
    private readonly DesignStorage       _designs;

    private string  _newText = string.Empty;
    private string? _newDefinition;
    private Design? _newDesign;

    public RandomRestrictionDrawer(AutomationChanged automationChanged, Configuration config, AutoDesignManager autoDesignManager,
        RandomDesignCombo randomDesignCombo, AutomationSelection selection, DesignStorage designs)
    {
        _automationChanged = automationChanged;
        _config            = config;
        _autoDesignManager = autoDesignManager;
        _randomDesignCombo = randomDesignCombo;
        _selection         = selection;
        _designs           = designs;
        _automationChanged.Subscribe(OnAutomationChange, AutomationChanged.Priority.RandomRestrictionDrawer);
    }

    public void Dispose()
    {
        _automationChanged.Unsubscribe(OnAutomationChange);
    }

    public void DrawButton(AutoDesignSet set, int designIndex)
    {
        var isOpen = set == _set && designIndex == _designIndex;
        using (ImGuiColor.Button.Push(Im.Style[ImGuiColor.ButtonActive], isOpen)
                   .Push(ImGuiColor.Text,   ColorId.HeaderButtons.Value(), isOpen)
                   .Push(ImGuiColor.Border, ColorId.HeaderButtons.Value(), isOpen))
        {
            using var frame = ImStyleSingle.FrameBorderThickness.Push(2 * Im.Style.GlobalScale, isOpen);
            if (ImEx.Icon.Button(LunaStyle.EditIcon))
            {
                if (isOpen)
                    Close();
                else
                    Open(set, designIndex);
            }
        }

        Im.Tooltip.OnHover("编辑要对随机设计作出的限制。"u8);
    }

    private void Open(AutoDesignSet set, int designIndex)
    {
        if (designIndex < 0 || designIndex >= set.Designs.Count)
            return;

        var design = set.Designs[designIndex];
        if (design.Design is not RandomDesign)
            return;

        _set         = set;
        _designIndex = designIndex;
    }

    private void Close()
    {
        _set         = null;
        _designIndex = -1;
    }

    public void Draw()
    {
        if (_set is null || _designIndex < 0 || _designIndex >= _set.Designs.Count)
            return;

        if (_set != _selection.Set)
        {
            Close();
            return;
        }

        var design = _set.Designs[_designIndex];
        if (design.Design is not RandomDesign random)
            return;

        DrawWindow(random);
    }

    private void DrawWindow(RandomDesign random)
    {
        var flags = WindowFlags.NoFocusOnAppearing
          | WindowFlags.NoCollapse
          | WindowFlags.NoResize;

        // Set position to the right of the main window when attached
        // The downwards offset is implicit through child position.
        if (_config.KeepAdvancedDyesAttached)
        {
            var position = Im.Window.Position;
            position.X += Im.Window.Size.X + Im.Style.WindowPadding.X;
            Im.Window.SetNextPosition(position);
            flags |= WindowFlags.NoMove;
        }

        using var color = ImGuiColor.TitleBackgroundActive.Push(Im.Style[ImGuiColor.TitleBackground]);

        var size = new Vector2(7 * Im.Style.FrameHeight + 3 * Im.Style.ItemInnerSpacing.X + 300 * Im.Style.GlobalScale,
            18 * Im.Style.FrameHeightWithSpacing + Im.Style.WindowPadding.Y + Im.Style.ItemSpacing.Y);
        Im.Window.SetNextSize(size);

        var open   = true;
        var window = Im.Window.Begin($"{_set!.Name} #{_designIndex + 1:D2}###Glamourer Random Design", ref open, flags);
        try
        {
            if (window)
                DrawContent(random);
        }
        finally
        {
            window.Dispose();
        }

        if (!open)
            Close();
    }

    private void DrawTable(RandomDesign random, List<IDesignPredicate> list)
    {
        using var table = Im.Table.Begin("##table"u8, 3);
        if (!table)
            return;

        using var spacing    = ImStyleDouble.ItemSpacing.Push(Im.Style.ItemInnerSpacing);
        var       buttonSize = new Vector2(Im.Style.FrameHeight);
        var       descWidth  = Im.Font.CalculateSize("or that are set to the color"u8).X;
        table.SetupColumn("desc"u8,  TableColumnFlags.WidthFixed, descWidth);
        table.SetupColumn("input"u8, TableColumnFlags.WidthStretch);
        table.SetupColumn("del"u8,   TableColumnFlags.WidthFixed, buttonSize.X * 2 + Im.Style.ItemInnerSpacing.X);

        var orSize = Im.Font.CalculateSize("或 "u8);
        for (var i = 0; i < random.Predicates.Count; ++i)
        {
            using var id        = Im.Id.Push(i);
            var       predicate = random.Predicates[i];
            table.NextColumn();
            if (i is not 0)
                ImEx.TextFrameAligned("或 "u8);
            else
                Im.Dummy(orSize);
            Im.Line.NoSpacing();
            switch (predicate)
            {
                case RandomPredicate.Contains contains:
                {
                    ImEx.TextFrameAligned("包含这个文本的"u8);
                    table.NextColumn();
                    var data = contains.Value;
                    Im.Item.SetNextWidthFull();
                    if (Im.Input.Text("##match"u8, ref data, "名称、路径或标识符..."u8))
                    {
                        if (data.Length is 0)
                            list.RemoveAt(i);
                        else
                            list[i] = new RandomPredicate.Contains(data);
                        _autoDesignManager.ChangeData(_set!, _designIndex, list);
                    }

                    break;
                }
                case RandomPredicate.StartsWith startsWith:
                {
                    ImEx.TextFrameAligned("路径以此开头的"u8);
                    table.NextColumn();
                    var data = startsWith.Value;
                    Im.Item.SetNextWidthFull();
                    if (Im.Input.Text("##startsWith"u8, ref data, "路径以此开头..."u8))
                    {
                        if (data.Length is 0)
                            list.RemoveAt(i);
                        else
                            list[i] = new RandomPredicate.StartsWith(data);
                        _autoDesignManager.ChangeData(_set!, _designIndex, list);
                    }

                    break;
                }
                case RandomPredicate.Exact { Which: RandomPredicate.Exact.Type.Tag } exact:
                {
                    ImEx.TextFrameAligned("包含这个标签的"u8);
                    table.NextColumn();
                    Im.Item.SetNextWidthFull();
                    var data = exact.Value;
                    if (Im.Input.Text("##color"u8, ref data, "包含标签..."u8))
                    {
                        if (data.Length is 0)
                            list.RemoveAt(i);
                        else
                            list[i] = new RandomPredicate.Exact(RandomPredicate.Exact.Type.Tag, data);
                        _autoDesignManager.ChangeData(_set!, _designIndex, list);
                    }

                    break;
                }
                case RandomPredicate.Exact { Which: RandomPredicate.Exact.Type.Color } exact:
                {
                    ImEx.TextFrameAligned("设置为这个颜色的"u8);
                    table.NextColumn();
                    Im.Item.SetNextWidthFull();
                    var data = exact.Value;
                    if (Im.Input.Text("##color"u8, ref data, "分配的颜色是..."u8))
                    {
                        if (data.Length is 0)
                            list.RemoveAt(i);
                        else
                            list[i] = new RandomPredicate.Exact(RandomPredicate.Exact.Type.Color, data);
                        _autoDesignManager.ChangeData(_set!, _designIndex, list);
                    }

                    break;
                }
                case RandomPredicate.Exact exact:
                {
                    ImEx.TextFrameAligned("完全匹配这个标识符的"u8);
                    table.NextColumn();
                    if (_randomDesignCombo.Draw(exact, out var newDesign, Im.ContentRegion.Available.X))
                    {
                        list[i] = new RandomPredicate.Exact(RandomPredicate.Exact.Type.Identifier, newDesign.Identifier.ToString());
                        _autoDesignManager.ChangeData(_set!, _designIndex, list);
                    }

                    break;
                }
            }

            table.NextColumn();
            if (ImEx.Icon.Button(LunaStyle.DeleteIcon, "删除这条限制。"u8))
            {
                list.RemoveAt(i);
                _autoDesignManager.ChangeData(_set!, _designIndex, list);
            }

            Im.Line.Same();
            DrawLookup(predicate);
        }
    }

    private void DrawLookup(IDesignPredicate predicate)
    {
        ImEx.Icon.Button(FontAwesomeIcon.MagnifyingGlassChart.Icon(), StringU8.Empty);
        if (!Im.Item.Hovered())
            return;

        var designs = predicate.Get(_designs);
        LookupTooltip(designs);
    }

    private static void LookupTooltip(IEnumerable<Design> designs)
    {
        const int displayLimit = 30;
        using var _            = Im.Tooltip.Begin();
        using var enumerator   = designs.GetEnumerator();
        var       counter      = 0;
        if (enumerator.MoveNext())
        {
            ++counter;
            Im.Text("匹配以下设计："u8);
            var name = enumerator.Current.Path.CurrentPath;
            Im.Separator();
            Im.BulletText(name);
            while (enumerator.MoveNext())
            {
                name = enumerator.Current.Path.CurrentPath;
                if (counter <= displayLimit)
                    Im.BulletText(name);
                ++counter;
            }

            if (counter > displayLimit)
                Im.Text($"And {counter - displayLimit} other designs...");

            return;
        }

        Im.Text("没有匹配到现有的设计。"u8);
    }

    private void DrawNewButtons(List<IDesignPredicate> list)
    {
        Im.Item.SetNextWidthFull();
        Im.Input.Text("##newText"u8, ref _newText, "添加新限制..."u8);
        var invalid = _newText.Length is 0;

        var buttonSize = new Vector2((Im.ContentRegion.Available.X - 3 * Im.Style.ItemInnerSpacing.X) / 4, 0);
        var changed = ImEx.Button("路径以此开头"u8, buttonSize,
                "添加一个新条件：设计路径必须以此开头。"u8, invalid)
         && Add(new RandomPredicate.StartsWith(_newText));

        Im.Line.SameInner();
        changed |= ImEx.Button("包含文本"u8, buttonSize,
                "添加一个新条件：设计路径、名称或标识符必须包含这个文本。"u8, invalid)
         && Add(new RandomPredicate.Contains(_newText));

        Im.Line.SameInner();
        changed |= ImEx.Button("指定标签"u8, buttonSize,
                "添加一个新条件：设计必须包含这个标签。"u8, invalid)
         && Add(new RandomPredicate.Exact(RandomPredicate.Exact.Type.Tag, _newText));

        Im.Line.SameInner();
        changed |= ImEx.Button("分配颜色"u8, buttonSize,
                "添加一个新条件：设计必须分配给这个颜色。"u8, invalid)
         && Add(new RandomPredicate.Exact(RandomPredicate.Exact.Type.Color, _newText));

        if (_randomDesignCombo.Draw("##c"u8, _newDesign, out var newDesign,
                Im.ContentRegion.Available.X - Im.Style.ItemInnerSpacing.X - buttonSize.X))
            _newDesign = newDesign as Design;
        Im.Line.SameInner();
        if (ImEx.Button("具体设计"u8, buttonSize, "添加单个指定的设计。"u8, _newDesign is null))
        {
            Add(new RandomPredicate.Exact(RandomPredicate.Exact.Type.Identifier, _newDesign!.Identifier.ToString()));
            changed    = true;
            _newDesign = null;
        }

        if (changed)
            _autoDesignManager.ChangeData(_set!, _designIndex, list);

        return;

        bool Add(IDesignPredicate predicate)
        {
            list.Add(predicate);
            return true;
        }
    }

    private void DrawManualInput(IReadOnlyList<IDesignPredicate> list)
    {
        Im.Dummy(Vector2.Zero);
        Im.Separator();
        Im.Dummy(Vector2.Zero);
        DrawTotalPreview(list);
        var currentDefinition = RandomPredicate.GeneratePredicateString(list);
        var definition        = _newDefinition ?? currentDefinition;
        definition = definition.Replace(";", ";\n\t").Replace("{", "{\n\t").Replace("}", "\n}");
        var lines = definition.Count(c => c is '\n');
        if (Im.Input.MultiLine("##definition"u8, ref definition,
                Im.ContentRegion.Available with { Y = (lines + 1) * Im.Style.TextHeight + Im.Style.FrameHeight },
                InputTextFlags.CtrlEnterForNewLine))
            _newDefinition = definition;
        if (Im.Item.DeactivatedAfterEdit && _newDefinition is not null && _newDefinition != currentDefinition)
        {
            var predicates = RandomPredicate.GeneratePredicates(_newDefinition.Replace("\n", string.Empty).Replace("\t", string.Empty));
            _autoDesignManager.ChangeData(_set!, _designIndex, predicates);
            _newDefinition = null;
        }

        if (Im.Button("复制不含换行符的数据到剪贴板"u8, Im.ContentRegion.Available with { Y = 0 }))
        {
            try
            {
                Im.Clipboard.Set(currentDefinition);
            }
            catch
            {
                // ignored
            }
        }
    }

    private void DrawTotalPreview(IReadOnlyList<IDesignPredicate> list)
    {
        var designs = IDesignPredicate.Get(list, _designs).ToList();
        Im.Button(designs.Count > 0
            ? $"所有限制条件的组合匹配到{designs.Count}个设计"
            : "限制条件没能匹配到任何设计"u8, Im.ContentRegion.Available with { Y = 0 });
        if (Im.Item.Hovered())
            LookupTooltip(designs);
    }

    private void DrawContent(RandomDesign random)
    {
        Im.Cursor.Y += Im.Style.GlobalScale - Im.Style.WindowPadding.Y;
        Im.Separator();
        Im.Dummy(Vector2.Zero);
        var reset = random.ResetOnRedraw;
        if (Im.Checkbox("每次重绘时重置选择的设计"u8, ref reset))
            _autoDesignManager.ChangeData(_set!, _designIndex, reset);
        Im.Separator();
        Im.Dummy(Vector2.Zero);

        var list = random.Predicates.ToList();
        if (list.Count is 0)
        {
            Im.Text("未设置限制。在现存所有设计中进行选择。"u8);
        }
        else
        {
            Im.Text("从这些设计中选择..."u8);
            DrawTable(random, list);
        }

        Im.Dummy(Vector2.Zero);
        Im.Separator();
        Im.Dummy(Vector2.Zero);

        DrawNewButtons(list);
        DrawManualInput(list);
    }

    private void OnAutomationChange(in AutomationChanged.Arguments arguments)
    {
        if (arguments.Set != _set || _set is null)
            return;

        switch (arguments.Type)
        {
            case AutomationChanged.Type.DeletedSet:
            case AutomationChanged.Type.DeletedDesign when arguments.As<AutomationChanged.DeletedDesignArguments>().Index == _designIndex:
                Close();
                break;
            case AutomationChanged.Type.MovedDesign:
                var data = arguments.As<AutomationChanged.MovedDesignArguments>();
                if (_designIndex == data.OldIndex)
                    _designIndex = data.NewIndex;
                else if (_designIndex < data.OldIndex && _designIndex > data.NewIndex)
                    _designIndex++;
                else if (_designIndex > data.NewIndex && _designIndex < data.OldIndex)
                    _designIndex--;
                break;
            case AutomationChanged.Type.ChangedDesign when arguments.As<AutomationChanged.ChangedDesignArguments>().DesignIndex == _designIndex:
                Close();
                break;
        }
    }
}
