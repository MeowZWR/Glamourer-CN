using Glamourer.Automation;
using Glamourer.Designs;
using Glamourer.Designs.Special;
using Glamourer.Interop;
using Glamourer.Services;
using Glamourer.Unlocks;
using Glamourer.Config;
using Glamourer.Events;
using ImSharp;
using Luna;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;

namespace Glamourer.Gui.Tabs.AutomationTab;

public sealed class SetPanel(
    AutoDesignManager manager,
    JobService jobs,
    ItemUnlockManager itemUnlocks,
    SpecialDesignCombo designCombo,
    CustomizeUnlockManager customizeUnlocks,
    CustomizeService customizations,
    IdentifierDrawer identifierDrawer,
    Configuration config,
    RandomRestrictionDrawer randomDrawer,
    AutomationSelection selection,
    AutomationChanged automationChanged) : IPanel
{
    private readonly AutomationSelection _selection         = selection;
    private readonly AutomationChanged   _automationChanged = automationChanged;
    private readonly JobGroupCombo       _jobGroupCombo     = new(manager, jobs);

    private readonly AutoDesignNameFilter    _nameFilter    = new(config);
    private readonly AutoDesignJobFilter     _jobFilter     = new(config);
    private readonly AutoDesignEnabledFilter _enabledFilter = new(config);

    private int _dragIndex = -1;

    private Action? _endAction;

    public ReadOnlySpan<byte> Id
        => "SetPanel"u8;

    public void Draw()
    {
        if (_selection.Index < 0)
            return;

        using (Im.Group())
        {
            var enabled = _selection.Set!.Enabled;
            if (Im.Checkbox("##Enabled"u8, ref enabled))
                manager.SetState(_selection.Index, enabled);
            LunaStyle.DrawAlignedHelpMarkerLabel("启用"u8,
                "是否应用该自动执行集中的设计。一个角色同时只能启用一个执行集。"u8);

            var useGame = _selection.Set!.BaseState is AutoDesignSet.Base.Game;
            if (Im.Checkbox("##gameState"u8, ref useGame))
                manager.ChangeBaseState(_selection.Index, useGame ? AutoDesignSet.Base.Game : AutoDesignSet.Base.Current);
            LunaStyle.DrawAlignedHelpMarkerLabel("使用游戏状态作为基础"u8,
                "启用此选项后，符合条件的角色设计将按顺序应用于游戏中角色的外观上。"u8
              + "禁用此选项后，设计将应用于角色当前被 Glamourer 修改后的实际外观上。"u8);
        }

        Im.Line.Same();
        using (Im.Group())
        {
            var editing = config.ShowAutomationSetEditing;
            if (Im.Checkbox("##Show Editing"u8, ref editing))
            {
                config.ShowAutomationSetEditing = editing;
                config.Save();
            }

            LunaStyle.DrawAlignedHelpMarkerLabel("显示可编辑内容"u8,
                "显示更改此执行集的名称、关联角色/NPC的选项。取消勾选以精简视图。"u8);

            var resetSettings = _selection.Set!.ResetTemporarySettings;
            if (Im.Checkbox("##resetSettings"u8, ref resetSettings))
                manager.ChangeResetSettings(_selection.Index, resetSettings);

            LunaStyle.DrawAlignedHelpMarkerLabel("重置临时设置"u8,
                "每次应用此自动执行集时，始终重置由 Glamourer 应用的所有临时设置，无论当前设计是否激活。"u8);
        }

        if (config.ShowAutomationSetEditing)
        {
            Im.Dummy(Vector2.Zero);
            Im.Separator();
            Im.Dummy(Vector2.Zero);

            var flags = config.Ephemeral.IncognitoMode ? InputTextFlags.ReadOnly | InputTextFlags.Password : InputTextFlags.None;
            Im.Item.SetNextWidthScaled(330);
            if (ImEx.InputOnDeactivation.Text("重命名执行集##Name"u8, _selection.Name, out string newName, default, flags))
                manager.Rename(_selection.Index, newName);


            DrawIdentifierSelection(_selection.Index);
        }

        Im.Dummy(Vector2.Zero);
        Im.Separator();
        Im.Dummy(Vector2.Zero);

        DrawDesignTable();
        randomDrawer.Draw();
    }

    private void DrawDesignTable()
    {
        var singleRow = IsSingleRowLayout();
        var numRows = (singleRow, config.ShowUnlockedItemWarnings) switch
        {
            (true, true)   => 6,
            (true, false)  => 5,
            (false, true)  => 5,
            (false, false) => 4,
        };

        using var table = Im.Table.Begin("SetTable"u8, numRows, TableFlags.RowBackground | TableFlags.ScrollX | TableFlags.ScrollY);
        if (!table)
            return;

        table.SetupScrollFreeze(0, 3);
        table.SetupColumn("##del"u8,   TableColumnFlags.WidthFixed, Im.Style.FrameHeight);
        table.SetupColumn("##Index"u8, TableColumnFlags.WidthFixed, 30 * Im.Style.GlobalScale);

        if (singleRow)
        {
            table.SetupColumn("角色设计"u8, TableColumnFlags.WidthFixed, 220 * Im.Style.GlobalScale);
            if (config.ShowAllAutomatedApplicationRules)
                table.SetupColumn("执行规则"u8, TableColumnFlags.WidthFixed,
                    6 * Im.Style.FrameHeight + 10 * Im.Style.GlobalScale);
            else
                table.SetupColumn("使用"u8, TableColumnFlags.WidthFixed, Im.Font.CalculateSize("使用"u8).X);
        }
        else
        {
            table.SetupColumn("角色设计/职业限制"u8, TableColumnFlags.WidthFixed,
                250 * Im.Style.GlobalScale - (Im.Scroll.MaximumY > 0 ? Im.Style.ScrollbarSize : 0));
            if (config.ShowAllAutomatedApplicationRules)
                table.SetupColumn("执行规则"u8, TableColumnFlags.WidthFixed,
                    3 * Im.Style.FrameHeight + 4 * Im.Style.GlobalScale);
            else
                table.SetupColumn("使用"u8, TableColumnFlags.WidthFixed, Im.Font.CalculateSize("使用"u8).X);
        }

        if (singleRow)
            table.SetupColumn("职业限制"u8, TableColumnFlags.WidthStretch);

        if (config.ShowUnlockedItemWarnings)
            table.SetupColumn(""u8, TableColumnFlags.WidthFixed, 2 * Im.Style.FrameHeight + 4 * Im.Style.GlobalScale);

        table.HeaderRow();
        Im.Table.NextColumn();
        Im.Table.NextColumn();
        Im.Table.NextColumn();
        _nameFilter.DrawFilter("筛选设计..."u8, Im.ContentRegion.Available with { Y = Im.Style.FrameHeight });
        if (singleRow)
        {
            Im.Table.NextColumn();
            _enabledFilter.DrawCheckboxFilter();
            Im.Table.NextColumn();
            _jobFilter.DrawFilter("筛选职业..."u8, Im.ContentRegion.Available with { Y = Im.Style.FrameHeight });
        }
        else
        {
            _jobFilter.DrawFilter("筛选职业..."u8, Im.ContentRegion.Available with { Y = Im.Style.FrameHeight });
            Im.Table.NextColumn();
            _enabledFilter.DrawCheckboxFilter();
        }

        Im.Table.NextRow();

        table.NextColumn();
        table.DrawFrameColumn($"#{_selection.Set!.Designs.Count + 1}"); 
        table.NextColumn();
        designCombo.Draw(_selection.Set!, null, -1);
        table.DrawFrameColumn("添加新设计"u8);

        var       cache = CacheManager.Instance.GetOrCreateCache(Im.Id.Current, () => new AutoDesignCache(this));
        using var clip  = new Im.ListClipper(cache.Count, singleRow ? Im.Style.FrameHeightWithSpacing : 2 * Im.Style.FrameHeightWithSpacing);
        foreach (var cacheItem in clip.Iterate(cache))
        {
            using var id = Im.Id.Push(cacheItem.Index);
            table.NextColumn();
            var keyValid = config.DeleteDesignModifier.IsActive();
            if (ImEx.Icon.Button(LunaStyle.DeleteIcon, "从执行集中移除此设计。"u8, !keyValid))
                _endAction = () => manager.DeleteDesign(cacheItem.Set, cacheItem.Index);
            if (!keyValid)
                Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"按住 {config.DeleteDesignModifier} 来移除此设计。");
            table.NextColumn();
            DrawSelectable(cacheItem);

            table.NextColumn();
            DrawRandomEditing(cacheItem.Set, cacheItem.Design, cacheItem.Index);
            designCombo.Draw(cacheItem.Set, cacheItem.Design, cacheItem.Index);
            DrawDragDrop(cacheItem.Set, cacheItem.Index);
            if (singleRow)
            {
                table.NextColumn();
                DrawApplicationTypeBoxes(cacheItem.Set, cacheItem.Design, cacheItem.Index, singleRow);
                table.NextColumn();
                DrawConditions(cacheItem);
            }
            else
            {
                DrawConditions(cacheItem);
                table.NextColumn();
                DrawApplicationTypeBoxes(cacheItem.Set, cacheItem.Design, cacheItem.Index, singleRow);
            }

            if (config.ShowUnlockedItemWarnings)
            {
                table.NextColumn();
                DrawWarnings(cacheItem);
            }
        }

        _endAction?.Invoke();
        _endAction = null;
    }

    private bool IsSingleRowLayout()
    {
        var (numCheckboxes, numSpacing) = (config.ShowAllAutomatedApplicationRules, config.ShowUnlockedItemWarnings) switch
        {
            (true, true)   => (9, 14),
            (true, false)  => (7, 10),
            (false, true)  => (4, 4),
            (false, false) => (2, 0),
        };

        var requiredSizeOneLine = numCheckboxes * Im.Style.FrameHeight
          + (30 + 220 + numSpacing) * Im.Style.GlobalScale
          + 5 * Im.Style.CellPadding.X
          + 150 * Im.Style.GlobalScale;

        return Im.ContentRegion.Available.X >= requiredSizeOneLine || numSpacing is 0;
    }

    private void DrawSelectable(in AutoDesignCacheItem cacheItem)
    {
        var highlight = ColorParameter.Default;
        var sb        = new StringBuilder();
        if (cacheItem.Design.Design is Design d)
        {
            var count = d.AllLinks(true).Count();
            if (count > 1)
            {
                sb.AppendLine($"此设计包含 {count - 1} 个指向其他设计的链接。");
                highlight = ColorId.HeaderButtons.Value();
            }

            count = d.AssociatedMods.Count;
            if (count > 0)
            {
                sb.AppendLine($"此设计包含 {count} 个模组关联。");
                highlight = ColorId.ModdedItemMarker.Value();
            }

            count = d.GetMaterialData().Count(p => p.Item2.Enabled);
            if (count > 0)
            {
                sb.AppendLine($"此设计包含 {count} 个已启用的高级染色。");
                highlight = ColorId.AdvancedDyeActive.Value();
            }
        }

        using (ImGuiColor.Text.Push(highlight))
        {
            Im.Selectable(cacheItem.IndexU8);
        }

        Im.Tooltip.OnHover($"{sb}");

        DrawDragDrop(cacheItem.Set, cacheItem.Index);
    }

    private void DrawConditions(in AutoDesignCacheItem item)
    {
        var usingGearset = item.Design.GearsetIndex >= 0;
        if (Im.Button(usingGearset ? "套装:##usingGearset"u8 : "职业:##usingGearset"u8))
        {
            usingGearset = !usingGearset;
            manager.ChangeGearsetCondition(item.Set, item.Index, (short)(usingGearset ? 0 : -1));
        }

        Im.Tooltip.OnHover("单击可在职业和套装之间切换限制。"u8);

        Im.Line.SameInner();
        if (usingGearset)
        {
            Im.Item.SetNextWidthFull();
            if (ImEx.InputOnDeactivation.Scalar("##whichGearset"u8, item.Design.GearsetIndex + 1, out var newIndex))
                manager.ChangeGearsetCondition(item.Set, item.Index, (short)(Math.Clamp(newIndex, 1, 100) - 1));
        }
        else
        {
            _jobGroupCombo.Draw(item.Set, item.Design, item.Index);
        }
    }

    private void DrawRandomEditing(AutoDesignSet set, AutoDesign design, int designIdx)
    {
        if (design.Design is not RandomDesign)
            return;

        randomDrawer.DrawButton(set, designIdx);
        Im.Line.SameInner();
    }

    private void DrawWarnings(in AutoDesignCacheItem item)
    {
        if (item.Design.Design is not DesignBase)
            return;

        var size = new Vector2(Im.Style.FrameHeight);
        size.X += Im.Style.GlobalScale;

        var collection = item.Design.ApplyWhat();
        var sb         = new StringBuilder();
        var designData = item.Design.Design.GetDesignData(default);
        foreach (var slot in EquipSlotExtensions.EqdpSlots.Append(EquipSlot.MainHand).Append(EquipSlot.OffHand))
        {
            var flag = slot.ToFlag();
            if (!collection.Equip.HasFlag(flag))
                continue;

            var equip = designData.Item(slot);
            if (!itemUnlocks.IsUnlocked(equip.Id, out _))
                sb.AppendLine($"在{slot.ToName()}部位的{equip.Name}还没有获取过。请考虑在游戏中去获取它！");
        }

        using var style = ImStyleDouble.ItemSpacing.Push(new Vector2(2 * Im.Style.GlobalScale, 0));

        var tt = config.UnlockedItemMode
            ? "\n这些物品将在自动应用时被跳过。\n\n要更改此设置，请禁用“已获取物品模式”。"
            : string.Empty;
        DrawWarning(sb, config.UnlockedItemMode ? 0xA03030F0 : 0x0, size, tt, "所有待应用的物品均已解锁。"u8);

        sb.Clear();
        var sb2       = new StringBuilder();
        var customize = designData.Customize;
        if (!designData.IsHuman)
            sb.AppendLine("基础模型id不能自动应用于某些非人类角色。");

        var set = customizations.Manager.GetSet(customize.Clan, customize.Gender);
        foreach (var type in CustomizationExtensions.All)
        {
            var flag = type.ToFlag();
            if (!collection.Customize.HasFlag(flag))
                continue;

            if (flag.RequiresRedraw())
                sb.AppendLine($"{type.ToName()} 外貌不应自动更改。");
            else if (type is CustomizeIndex.Hairstyle or CustomizeIndex.FacePaint
                  && set.DataByValue(type, customize[type], out var data, customize.Face) >= 0
                  && !customizeUnlocks.IsUnlocked(data!.Value, out _))
                sb2.AppendLine(
                    $"{type.ToName()} 外貌 {customizeUnlocks.Unlockable[data.Value].Name} 未解锁但应被应用。");
        }

        Im.Line.Same();
        tt = config.UnlockedItemMode
            ? "\n这些外貌将在自动应用时被跳过。\n\n要更改此设置，请禁用“已获取物品模式”。"
            : string.Empty;
        DrawWarning(sb2, config.UnlockedItemMode ? 0xA03030F0 : 0x0, size, tt, "所有待应用的外貌均已解锁。"u8);
        Im.Line.Same();
        return;

        static void DrawWarning(StringBuilder sb, Rgba32 color, Vector2 size, string suffix, ReadOnlySpan<byte> good)
        {
            using var style = ImStyleSingle.FrameBorderThickness.Push(Im.Style.GlobalScale);
            if (sb.Length > 0)
            {
                sb.Append(suffix);
                using (AwesomeIcon.Font.Push())
                {
                    ImEx.TextFramed(LunaStyle.WarningIcon.Span, size, color);
                }

                Im.Tooltip.OnHover($"{sb}");
            }
            else
            {
                ImEx.TextFramed(StringU8.Empty, size, Rgba32.Transparent);
                Im.Tooltip.OnHover(good);
            }
        }
    }

    private void DrawDragDrop(AutoDesignSet set, int index)
    {
        using (var target = Im.DragDrop.Target())
        {
            if (target.IsDropping("DesignDragDrop"u8))
            {
                if (_dragIndex >= 0)
                {
                    var idx = _dragIndex;
                    _endAction = () => manager.MoveDesign(set, idx, index);
                }

                _dragIndex = -1;
            }
        }

        using (var source = Im.DragDrop.Source())
        {
            if (source)
            {
                Im.Text($"移动角色设计 #{index + 1:D2}...");
                if (source.SetPayload("DesignDragDrop"u8))
                {
                    _dragIndex                    = index;
                    _selection.DraggedDesignIndex = index;
                }
            }
        }
    }

    private void DrawApplicationTypeBoxes(AutoDesignSet set, AutoDesign design, int autoDesignIndex, bool singleLine)
    {
        using var style   = ImStyleDouble.ItemSpacing.PushX(2 * Im.Style.GlobalScale);
        var       newType = design.Type;
        using (ImStyleBorder.Frame.Push(ColorId.FolderLine.Value()))
        {
            Im.Checkbox("##all"u8, ref newType, ApplicationType.All);
        }

        style.Pop();
        Im.Tooltip.OnHover("一键开关"u8);
        style.PushX(ImStyleDouble.ItemSpacing, 2 * Im.Style.GlobalScale);
        if (config.ShowAllAutomatedApplicationRules)
        {
            void Box(int idx)
            {
                var type = ApplicationTypeExtensions.Types[idx];
                using var id    = Im.Id.Push((uint)type);
                var       value = design.Type.HasFlag(type);
                if (Im.Checkbox(StringU8.Empty, ref value))
                    newType = value ? newType | type : newType & ~type;
                Im.Tooltip.OnHover(type.Tooltip());
            }

            Im.Line.Same();
            Box(0);
            Im.Line.Same();
            Box(1);
            if (singleLine)
                Im.Line.Same();

            Box(2);
            Im.Line.Same();
            Box(3);
            Im.Line.Same();
            Box(4);
        }

        manager.ChangeApplicationType(set, autoDesignIndex, newType);
    }

    private void DrawIdentifierSelection(int setIndex)
    {
        using var id = Im.Id.Push("Identifiers"u8);
        var       singleRow = IsSingleRowLayout();
        identifierDrawer.DrawWorld(130);
        Im.Line.Same();
        identifierDrawer.DrawName(200 - Im.Style.ItemSpacing.X);
        identifierDrawer.DrawNpcs(330);
        var buttonWidth = new Vector2(100 * Im.Style.GlobalScale - Im.Style.ItemSpacing.X / 2, 0);
        if (ImEx.Button("分配给玩家"u8, buttonWidth, StringU8.Empty, !identifierDrawer.CanSetPlayer))
            manager.ChangeIdentifier(setIndex, identifierDrawer.PlayerIdentifier);
        Im.Line.Same();
        if (ImEx.Button("分配给NPC"u8, buttonWidth, StringU8.Empty, !identifierDrawer.CanSetNpc))
            manager.ChangeIdentifier(setIndex, identifierDrawer.NpcIdentifier);
        Im.Line.Same();
        if (ImEx.Button("分配给雇员"u8, buttonWidth, StringU8.Empty, !identifierDrawer.CanSetRetainer))
            manager.ChangeIdentifier(setIndex, identifierDrawer.RetainerIdentifier);

        if (singleRow)
            Im.Line.Same();
        if (ImEx.Button("分配给服装模特"u8, buttonWidth, StringU8.Empty, !identifierDrawer.CanSetRetainer))
            manager.ChangeIdentifier(setIndex, identifierDrawer.MannequinIdentifier);
        Im.Line.Same();
        if (ImEx.Button("分配给所属NPC"u8, buttonWidth, StringU8.Empty, !identifierDrawer.CanSetOwned))
            manager.ChangeIdentifier(setIndex, identifierDrawer.OwnedIdentifier);
    }

    private sealed class JobGroupCombo(AutoDesignManager manager, JobService jobs)
        : SimpleFilterCombo<JobGroup>(SimpleFilterType.Partwise)
    {
        public void Draw(AutoDesignSet set, AutoDesign design, int autoDesignIndex)
        {
            if (Draw("##jobGroups"u8, design.Jobs,
                    "选择应该将此设计应用于哪些职业。\n按住键盘Ctrl键+鼠标右键点击此处设置为所有职业。"u8,
                    Im.ContentRegion.Available.X, out var newGroup))
                manager.ChangeJobCondition(set, autoDesignIndex, newGroup);
            else if (Im.Io.KeyControl && Im.Item.RightClicked())
                manager.ChangeJobCondition(set, autoDesignIndex, jobs.JobGroups[1]);
        }

        public override StringU8 DisplayString(in JobGroup value)
            => value.Name;

        public override string FilterString(in JobGroup value)
            => value.Name.ToString();

        public override IEnumerable<JobGroup> GetBaseItems()
            => jobs.JobGroups.Values;
    }


    public sealed class AutoDesignCache : BasicFilterCache<AutoDesignCacheItem>
    {
        private readonly AutomationSelection _selection;
        private readonly AutomationChanged   _automationChanged;

        public AutoDesignCache(SetPanel parent)
            : base(new MultiFilter<AutoDesignCacheItem>(parent._nameFilter, parent._jobFilter, parent._enabledFilter))
        {
            _selection         = parent._selection;
            _automationChanged = parent._automationChanged;

            _selection.SelectionChanged += OnSelectionChanged;
            _automationChanged.Subscribe(OnAutomationChanged, AutomationChanged.Priority.AutomationSelection);
        }

        private void OnAutomationChanged(in AutomationChanged.Arguments arguments)
            => Dirty |= IManagedCache.DirtyFlags.Custom;

        private void OnSelectionChanged()
            => Dirty |= IManagedCache.DirtyFlags.Custom;

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            _selection.SelectionChanged -= OnSelectionChanged;
            _automationChanged.Unsubscribe(OnAutomationChanged);
        }

        protected override IEnumerable<AutoDesignCacheItem> GetItems()
            => _selection.Set?.Designs.Select((d, i) => new AutoDesignCacheItem(_selection.Set!, d, i)) ?? [];
    }

    private sealed class AutoDesignNameFilter : Utf8FilterBase<AutoDesignCacheItem>
    {
        public AutoDesignNameFilter(Configuration config)
        { }

        public override bool WouldBeVisible(in AutoDesignCacheItem item, int globalIndex)
            => base.WouldBeVisible(in item, globalIndex) || WouldBeVisible(item.Incognito);

        protected override ReadOnlySpan<byte> ToFilterString(in AutoDesignCacheItem item, int globalIndex)
            => item.Name;
    }

    private sealed class AutoDesignJobFilter : Utf8FilterBase<AutoDesignCacheItem>
    {
        public AutoDesignJobFilter(Configuration config)
        { }

        protected override ReadOnlySpan<byte> ToFilterString(in AutoDesignCacheItem item, int globalIndex)
            => item.JobRestrictions;
    }

    private sealed class AutoDesignEnabledFilter : YesNoFilter<AutoDesignCacheItem>
    {
        public AutoDesignEnabledFilter(Configuration config)
        { }

        public override bool GetValue(in AutoDesignCacheItem item, int globalIndex, int triEnumIndex)
            => !item.Disabled;
    }
}
