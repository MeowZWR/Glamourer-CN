using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface;
using Dalamud.Plugin.Services;
using Glamourer.Automation;
using Glamourer.Config;
using Glamourer.Designs;
using Glamourer.Events;
using Glamourer.Gui.Equipment;
using Glamourer.Gui.Tabs.DesignTab;
using Glamourer.Interop;
using Glamourer.Interop.PalettePlus;
using Glamourer.Services;
using ImSharp;
using Luna;

namespace Glamourer.Gui.Tabs.SettingsTab;

public sealed class SettingsTab(
    Configuration config,
    DesignFileSystemDrawer drawer,
    ContextMenuService contextMenuService,
    IUiBuilder uiBuilder,
    GlamourerChangelog changelog,
    IKeyState keys,
    DesignColorUi designColorUi,
    PaletteImport paletteImport,
    CollectionOverrideDrawer overrides,
    CodeDrawer codeDrawer,
    Glamourer glamourer,
    AutoDesignApplier autoDesignApplier,
    AutoRedrawChanged autoRedraw,
    PredefinedTagManager predefinedTags,
    PcpService pcpService,
    IgnoredMods ignoredMods)
    : ITab<MainTabType>
{
    private readonly VirtualKey[] _validKeys = keys.GetValidVirtualKeys().Prepend(VirtualKey.NO_KEY).ToArray();

    public ReadOnlySpan<byte> Label
        => "插件设置"u8;

    public MainTabType Identifier
        => MainTabType.Settings;

    public void DrawContent()
    {
        using var child = Im.Child.Begin("MainWindowChild"u8);
        if (!child)
            return;

        Checkbox("启用自动执行"u8,
            "启用后，自动使用与角色关联的设计，这些设计可以在自动执行标签页中配置。"u8,
            config.EnableAutoDesigns, v =>
            {
                config.EnableAutoDesigns = v;
                autoDesignApplier.OnEnableAutoDesignsChanged(v);
            });
        Im.Cursor.Y += Im.Style.FrameHeightWithSpacing * 4;

        using (Im.Child.Begin("SettingsChild"u8))
        {
            DrawBehaviorSettings();
            DrawDesignDefaultSettings();
            DrawInterfaceSettings();
            DrawColorSettings();
            DrawPredefinedTags();
            overrides.Draw();
            DrawIgnoredMods();
            codeDrawer.Draw();
        }

        MainWindow.DrawSupportButtons(glamourer, changelog.Changelog);
    }

    public void DrawPenumbraIntegrationSettings()
    {
        DrawPenumbraIntegrationSettings1();
        DrawPenumbraIntegrationSettings2();
    }

    private void DrawBehaviorSettings()
    {
        if (!Im.Tree.Header("行为设置"u8))
            return;

        Checkbox("总是为主手应用整套武器"u8,
            "当手动应用主手武器时，自动应用对应的副手武器。"u8,
            config.ChangeEntireItem, v => config.ChangeEntireItem = v);
        Checkbox("自动替换不兼容种族和性别的装备"u8,
            "当检测到某些项目不适用角色当前的种族和性别时，使用匹配种族和性别的模型。"u8,
            config.UseRestrictedGearProtection, v => config.UseRestrictedGearProtection = v);
        Checkbox("不在自动执行中使用未获得过的物品"u8,
            "如果你希望“自动执行”中只使用你已经获取过一次的物品，不使用那些从未获取过的物品，就启用这个选项。"u8,
            config.UnlockedItemMode, v => config.UnlockedItemMode = v);
        Checkbox("编辑自动化时尊重手动更改"u8,
            "对当前任何处于活动状态的自动执行组进行更改，在重新应用修改后的自动执行时是否保留手动作出的更改。"u8,
            config.RespectManualOnAutomationUpdate, v => config.RespectManualOnAutomationUpdate = v);
        Checkbox("启动节日彩蛋"u8,
            "Glamourer也许会在一些特别的日子做一些有趣的事情。如果你觉得这会影响你的体验，请禁用此选项。"u8,
            config.DisableFestivals == 0, v => config.DisableFestivals = v ? (byte)0 : (byte)2);
        DrawPenumbraIntegrationSettings1();
        Checkbox("在更换区域时撤销手动更改"u8,
            "当你更换区域时，撤销你对角色进行的手动更改，恢复到游戏基础状态或自动执行状态。"u8,
            config.RevertManualChangesOnZoneChange, v => config.RevertManualChangesOnZoneChange = v);
        PaletteImportButton();
        DrawPenumbraIntegrationSettings2();
        Checkbox("防止随机设计重复"u8,
            "在使用随机设计时，防止连续两次选择相同的设计。"u8,
            config.PreventRandomRepeats, v => config.PreventRandomRepeats = v);
        Im.Line.New();
    }

    private void DrawPenumbraIntegrationSettings1()
    {
        Checkbox("自动重新加载装备"u8,
            "在更改角色关联合集的Penumbra模组选项时，自动在自己的角色身上重新加载装备部件。"u8,
            config.AutoRedrawEquipOnChanges, v =>
            {
                config.AutoRedrawEquipOnChanges = v;
                autoRedraw.Invoke(v);
            });
        Checkbox("关联至PCP处理"u8,
            "当Penumbra创建PCP时添加角色的Glamourer状态，并在Penumbra安装PCP时尽可能创建设计并应用"u8,
            config.AttachToPcp, pcpService.Set);
        var active = config.DeleteDesignModifier.IsActive();
        Im.Line.Same();
        if (ImEx.Button("删除所有PCP设计"u8, default, "从设计列表中删除所有带有'PCP'标签的设计。"u8, !active))
            pcpService.CleanPcpDesigns();
        if (!active)
            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"\nHold {config.DeleteDesignModifier} while clicking.");
    }

    private void DrawPenumbraIntegrationSettings2()
    {
        Checkbox("始终应用关联的模组"u8,
            "无论何时将设计应用于角色（包括自动执行）时，Glamourer都会尝试将设计相关的模组设置应用于当前与该角色相关的合集（如果可用）。\n\n"u8
          + "Glamourer不会自动还原这些应用的设置。这可能会打乱你的合集和配置。\n\n"u8
          + "如果你启用此设置，你应意识到任何由此产生的配置错误都是你自己造成的。"u8,
            config.AlwaysApplyAssociatedMods, v => config.AlwaysApplyAssociatedMods = v);
        Checkbox("使用临时模组设置"u8,
            "将所有设置应用为临时设置，以便在 Glamourer 或游戏关闭时重置。"u8,
            config.UseTemporarySettings,
            v => config.UseTemporarySettings = v);
    }

    private void DrawDesignDefaultSettings()
    {
        if (!Im.Tree.Header("设计默认设置"u8))
            return;

        Checkbox("锁定设计"u8, "新创建的设计将被锁定以防止意外修改。"u8,
            config.DefaultDesignSettings.Locked, v => config.DefaultDesignSettings.Locked = v);
        Checkbox("在快速设计栏中显示"u8, "新创建的设计将默认显示在快速设计栏中。"u8,
            config.DefaultDesignSettings.ShowQuickDesignBar, v => config.DefaultDesignSettings.ShowQuickDesignBar = v);
        Checkbox("重置高级染色"u8, "新创建的设计将在应用时默认配置为重置高级染色。"u8,
            config.DefaultDesignSettings.ResetAdvancedDyes, v => config.DefaultDesignSettings.ResetAdvancedDyes = v);
        Checkbox("始终强制重绘"u8, "新创建的设计将在应用时默认配置为强制角色重绘。"u8,
            config.DefaultDesignSettings.AlwaysForceRedrawing, v => config.DefaultDesignSettings.AlwaysForceRedrawing = v);
        Checkbox("重置临时设置"u8,
            "新创建的设计将在应用时默认配置为清除 Glamourer 应用到合集的所有高级设置。"u8,
            config.DefaultDesignSettings.ResetTemporarySettings, v => config.DefaultDesignSettings.ResetTemporarySettings = v);

        Im.Item.SetNextWidth(0.4f * Im.ContentRegion.Available.X);
        if (ImEx.InputOnDeactivation.Text("##pcpFolder"u8, config.PcpFolder, out string newPcpFolder))
        {
            config.PcpFolder = newPcpFolder;
            config.Save();
        }

        LunaStyle.DrawAlignedHelpMarkerLabel("默认PCP组织折叠组"u8,
            "任何因Penumbra角色包而创建的设计在创建时将被移动到的折叠组。\n留空则导入至根目录。"u8);

        Im.Item.SetNextWidth(0.4f * Im.ContentRegion.Available.X);
        if (ImEx.InputOnDeactivation.Text("##pcpColor"u8, config.PcpColor, out string newPcpColor))
        {
            config.PcpColor = newPcpColor;
            config.Save();
        }

        LunaStyle.DrawAlignedHelpMarkerLabel("默认PCP设计颜色"u8,
            "所有因Penumbra角色包而创建的设计将被分配的颜色组名称。\n留空则不指定特定颜色分配。"u8);
    }

    private void DrawInterfaceSettings()
    {
        if (!Im.Tree.Header("界面设置"u8))
            return;

        EphemeralCheckbox("显示快速设计栏"u8,
            "显示一个与主窗口分离的工具栏，允许您快速应用设计、恢复角色和目标。"u8,
            config.Ephemeral.ShowDesignQuickBar, v => config.Ephemeral.ShowDesignQuickBar = v);
        EphemeralCheckbox("锁定快速设计栏"u8, "防止快速设计栏被移动，将其锁定在当前位置。"u8,
            config.Ephemeral.LockDesignQuickBar,
            v => config.Ephemeral.LockDesignQuickBar = v);
        if (KeySelector.ModifiableKeySelector("快速设计栏开关热键"u8,
                "设置一个用于打开或关闭快速设计栏的热键。"u8,
                100 * Im.Style.GlobalScale, config.ToggleQuickDesignBar, v => config.ToggleQuickDesignBar = v, _validKeys))
            config.Save();

        Checkbox("在主窗口中显示快速设计栏"u8,
            "也在主窗口的选项卡的选择区域显示快速设计栏。"u8,
            config.ShowQuickBarInTabs, v => config.ShowQuickBarInTabs = v);
        DrawQuickDesignBoxes();

        Im.Dummy(Vector2.Zero);
        Im.Separator();
        Im.Dummy(Vector2.Zero);

        Checkbox("启用游戏右键菜单"u8, "在可装备物品的游戏右键菜单中增加一个Glamourer试穿按钮。"u8,
            config.EnableGameContextMenu,       v =>
            {
                config.EnableGameContextMenu = v;
                if (v)
                    contextMenuService.Enable();
                else
                    contextMenuService.Disable();
            });
        Checkbox("在用户界面隐藏时显示窗口"u8, "即使游戏UI隐藏，也可以显示Glamourer窗口。"u8,
            config.ShowWindowWhenUiHidden,          v =>
            {
                config.ShowWindowWhenUiHidden = v;
                uiBuilder.DisableUserUiHide   = v;
            });
        Checkbox("在过场动画中隐藏窗口"u8,
            "在进入过场动画时，Glamourer主窗口是否应该自动隐藏。"u8,
            config.HideWindowInCutscene,
            v =>
            {
                config.HideWindowInCutscene     = v;
                uiBuilder.DisableCutsceneUiHide = !v;
            });
        EphemeralCheckbox("锁定主窗口"u8, "防止主窗口被移动，将其锁定在当前位置。"u8,
            config.Ephemeral.LockMainWindow,
            v => config.Ephemeral.LockMainWindow = v);
        Checkbox("在游戏开始时打开主窗口"u8, "启动游戏后，Glamourer主窗口是打开还是关闭状态。"u8,
            config.OpenWindowAtStart,                v => config.OpenWindowAtStart = v);
        Im.Dummy(Vector2.Zero);
        Im.Separator();
        Im.Dummy(Vector2.Zero);

        Checkbox("装备面板紧凑显示"u8, "使用不显示装备图标、有小染色按钮的单行视图，取代两行视图。"u8,
            config.SmallEquip,              v => config.SmallEquip = v);
        DrawHeightUnitSettings();
        DrawRoughnessSettings();
        Checkbox("显示应用复选框"u8,
            "在“角色设计”选项卡下的“外貌”和“装备”面板中显示应用生效复选框，而不是仅在“应用规则”面板中显示。"u8,
            !config.HideApplyCheckmarks, v => config.HideApplyCheckmarks = !v);
        if (KeySelector.DoubleModifier("设计删除组合键"u8,
                "在点击删除设计按钮时，需要按住这些组合键，才能使删除操作生效。"u8, 100 * Im.Style.GlobalScale,
                config.DeleteDesignModifier, v => config.DeleteDesignModifier = v))
            config.Save();
        if (KeySelector.DoubleModifier("隐身模式组合键"u8,
                "在点击隐身模式按钮时需要按住这些组合键，才能使隐身模式生效。"u8, 100 * Im.Style.GlobalScale,
                config.IncognitoModifier, v => config.IncognitoModifier = v))
            config.Save();
        DrawRenameSettings();
        Checkbox("自动展开角色设计折叠组"u8,
            "登录游戏后，角色设计折叠组默认状态是打开还是关闭。"u8, config.OpenFoldersByDefault,
            v => config.OpenFoldersByDefault = v);
        DrawFolderSortType();

        Im.Line.New();
        Im.Text("在各自的标签页中显示以下面板："u8);
        Im.Dummy(Vector2.Zero);
        DesignPanelFlagExtensions.DrawTable("##panelTable"u8, config.HideDesignPanel, config.AutoExpandDesignPanel, v =>
        {
            config.HideDesignPanel = v;
            config.Save();
        }, v =>
        {
            config.AutoExpandDesignPanel = v;
            config.Save();
        });


        Im.Dummy(Vector2.Zero);
        Im.Separator();
        Im.Dummy(Vector2.Zero);

        Checkbox("允许双击应用设计"u8,
            "在设计选择其中双击角色设计条目时，尝试将该设计应用于玩家的角色。"u8,
            config.AllowDoubleClickToApply, v => config.AllowDoubleClickToApply = v);
        Checkbox("在执行集的“执行规则”中显示所有复选框"u8,
            "在执行规则中显示多个单独规则的复选框，而不是只显示一个一键开关的复选框。"u8,
            config.ShowAllAutomatedApplicationRules, v => config.ShowAllAutomatedApplicationRules = v);
        Checkbox("对获取物品显示警告"u8,
            "在执行规则中显示多个单独规则的复选框，而不是只显示一个一键开关的复选框。"u8,
            config.ShowUnlockedItemWarnings, v => config.ShowUnlockedItemWarnings = v);
        Checkbox("显示颜色显示配置"u8, "在高级外貌面板中显示颜色显示配置选项。"u8,
            config.ShowColorConfig,                    v => config.ShowColorConfig = v);
        Checkbox("显示 Palette+ 导入按钮"u8,
            "在高级外貌选项部分显示导入按钮，允许您将 Palette+ 调色板导入到设计中。"u8,
            config.ShowPalettePlusImport, v => config.ShowPalettePlusImport = v);
        using (Im.Id.Push(1))
        {
            PaletteImportButton();
        }

        Checkbox("高级染色窗口吸附"u8,
            "保持高级染色窗口与主窗口吸附，取消勾选使其可自由移动。"u8,
            config.KeepAdvancedDyesAttached, v => config.KeepAdvancedDyesAttached = v);

        Checkbox("调试模式"u8, "显示调试选项卡，仅对调试和进阶用法有帮助。一般不建议使用。"u8,
            config.DebugMode,
            v => config.DebugMode = v);

        Im.Dummy(Vector2.Zero);
        Im.Separator();
        Im.Dummy(Vector2.Zero);

        EquipmentDrawer.DrawKeepItemFilter(config);

        Checkbox("Remember Design Filter Across Sessions"u8,
            "Whether the filter in the Designs tab should remember its input and start with its list filtered identically to the last session."u8,
            config.RememberDesignFilter, v => config.RememberDesignFilter = v);

        Checkbox("Remember Actor Filter Across Sessions"u8,
            "Whether the filter in the Actors tab should remember its input and start with its list filtered identically to the last session."u8,
            config.RememberActorFilter, v => config.RememberActorFilter = v);

        Checkbox("Remember Automation Filters Across Sessions"u8,
            "Whether the filters in the Automation tab should remember their respective inputs and start with their list filtered identically to the last session."u8,
            config.RememberAutomationFilter, v => config.RememberAutomationFilter = v);

        Checkbox("Remember NPC Filter Across Sessions"u8,
            "Whether the filter in the NPCs tab should remember its input and start with its list filtered identically to the last session."u8,
            config.RememberNpcFilter, v => config.RememberNpcFilter = v);

        Checkbox("Remember Unlocks Filters Across Sessions"u8,
            "Whether the filters in the Unlocks tab should remember their respective inputs and start with its table filtered identically to the last session."u8,
            config.RememberUnlocksFilters, v => config.RememberUnlocksFilters = v);

        Im.Line.New();
    }

    private readonly (StringU8, QdbButtons)[] _columns =
    [
        (new StringU8("Toggle Main Window"u8), QdbButtons.ToggleMainWindow),
        (new StringU8("Apply Design"u8), QdbButtons.ApplyDesign),
        (new StringU8("Revert All"u8), QdbButtons.RevertAll),
        (new StringU8("Revert to Auto"u8), QdbButtons.RevertAutomation),
        (new StringU8("Reapply Auto"u8), QdbButtons.ReapplyAutomation),
        (new StringU8("Revert Equip"u8), QdbButtons.RevertEquip),
        (new StringU8("Revert Customize"u8), QdbButtons.RevertCustomize),
        (new StringU8("Revert Advanced Customization"u8), QdbButtons.RevertAdvancedCustomization),
        (new StringU8("Revert Advanced Dyes"u8), QdbButtons.RevertAdvancedDyes),
        (new StringU8("Reset Settings"u8), QdbButtons.ResetSettings),
    ];

    private static bool DisplayButton(QdbButtons button, bool showAuto, bool useTemporarySettings)
        => button switch
        {
            QdbButtons.RevertAutomation  => showAuto,
            QdbButtons.ReapplyAutomation => showAuto,
            QdbButtons.ResetSettings     => useTemporarySettings,
            _                            => true,
        };

    private void DrawQuickDesignBoxes()
    {
        var showAuto   = config.EnableAutoDesigns;
        var numColumns = 10 - (showAuto ? 0 : 2) - (config.UseTemporarySettings ? 0 : 1);
        Im.Line.New();
        Im.Text("在快速设计栏中显示以下按钮："u8);
        Im.Dummy(Vector2.Zero);
        using var table = Im.Table.Begin("##tableQdb"u8, numColumns, TableFlags.SizingFixedFit | TableFlags.Borders | TableFlags.NoHostExtendX);
        if (!table)
            return;


        // ReSharper disable once PossibleMultipleEnumeration
        foreach (var (text, flag) in _columns)
        {
            if (!DisplayButton(flag, showAuto, config.UseTemporarySettings))
                continue;

            table.NextColumn();
            table.Header(text);
        }

        // ReSharper disable once PossibleMultipleEnumeration
        foreach (var (_, flag) in _columns)
        {
            if (!DisplayButton(flag, showAuto, config.UseTemporarySettings))
                continue;

            using var id = Im.Id.Push((int)flag);
            table.NextColumn();
            var offset = (Im.ContentRegion.Available.X - Im.Style.FrameHeight) / 2;
            Im.Cursor.X += offset;
            var value = config.QdbButtons.HasFlag(flag);
            if (!Im.Checkbox(""u8, ref value))
                continue;

            var buttons = value ? config.QdbButtons | flag : config.QdbButtons & ~flag;
            if (buttons == config.QdbButtons)
                continue;

            config.QdbButtons = buttons;
            config.Save();
        }
    }

    private void PaletteImportButton()
    {
        if (!config.ShowPalettePlusImport)
            return;

        Im.Line.Same();
        if (Im.Button("导入调色板插件 Palette+ 的设计"u8))
            paletteImport.ImportDesigns();
        Im.Tooltip.OnHover(
            $"从你的调色板插件Palette+的配置中导入所有存在的数据到角色设计选项卡下，目录结构为PalettePlus/[名称]（如果还无同名设计存在）。现有调色板为：\n\n\t - {string.Join("\n\t - ", paletteImport.Data.Keys)}");
    }

    private void DrawPredefinedTags()
    {
        if (!Im.Tree.Header("标签设置"u8))
            return;

        var tagIdx = TagButtons.Draw("预定义标签: "u8,
            "预定义标签，可以单击添加或删除设计。"u8, predefinedTags,
            out var editedTag);

        if (tagIdx >= 0)
            predefinedTags.ChangeSharedTag(tagIdx, editedTag);
    }

    /// <summary> Draw the entire Color subsection. </summary>
    private void DrawColorSettings()
    {
        if (!Im.Tree.Header("配色设置"u8))
            return;

        using (var tree = Im.Tree.Node("自定义设计颜色"u8))
        {
            if (tree)
                designColorUi.Draw();
        }

        using (var tree = Im.Tree.Node("配色设置"u8))
        {
            if (tree)
                foreach (var color in ColorId.Values)
                {
                    var (defaultColor, name, description) = color.Data();
                    var currentColor = config.Colors.GetValueOrDefault(color, defaultColor);
                    if (!ImEx.ColorPicker(name, description, currentColor, out var newColor, defaultColor))
                        continue;

                    config.Colors[color] = newColor.Color;
                    CacheManager.Instance.SetColorsDirty();
                    config.Save();
                }
        }

        Im.Line.New();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void Checkbox(ReadOnlySpan<byte> label, ReadOnlySpan<byte> tooltip, bool current, Action<bool> setter)
    {
        using var id  = Im.Id.Push(label);
        var       tmp = current;
        if (Im.Checkbox(""u8, ref tmp) && tmp != current)
        {
            setter(tmp);
            config.Save();
        }

        LunaStyle.DrawAlignedHelpMarkerLabel(label, tooltip);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void EphemeralCheckbox(ReadOnlySpan<byte> label, ReadOnlySpan<byte> tooltip, bool current, Action<bool> setter)
    {
        using var id  = Im.Id.Push(label);
        var       tmp = current;
        if (Im.Checkbox(""u8, ref tmp) && tmp != current)
        {
            setter(tmp);
            config.Ephemeral.Save();
        }

        LunaStyle.DrawAlignedHelpMarkerLabel(label, tooltip);
    }

    /// <summary> Different supported sort modes as a combo. </summary>
    private void DrawFolderSortType()
    {
        var sortMode = config.SortMode;
        Im.Item.SetNextWidthScaled(300);
        using (var combo = Im.Combo.Begin("##sortMode"u8, sortMode.Name))
        {
            if (combo)
                foreach (var (_, value) in ISortMode.Valid)
                {
                    if (Im.Selectable(value.Name, value.GetType() == sortMode.GetType()) && value.GetType() != sortMode.GetType())
                    {
                        config.SortMode = value;
                        drawer.SortMode = value;
                        config.Save();
                    }

                    Im.Tooltip.OnHover(value.Description);
                }
        }

        LunaStyle.DrawAlignedHelpMarkerLabel("排序模式"u8, "为角色设计选项卡下的设计选择器选择一个排序方式。"u8);
    }

    private void DrawRenameSettings()
    {
        Im.Item.SetNextWidthScaled(300);
        using (var combo = Im.Combo.Begin("##renameSettings"u8, config.ShowRename.ToNameU8()))
        {
            if (combo)
                foreach (var value in RenameField.Values)
                {
                    if (Im.Selectable(value.ToNameU8(), config.ShowRename == value))
                    {
                        config.ShowRename = value;
                        config.Save();
                    }

                    Im.Tooltip.OnHover(value.Tooltip());
                }
        }

        LunaStyle.DrawAlignedHelpMarkerLabel("设计上下文菜单中的重命名字段"u8,
            "选择在打开设计选择器中设计右键上下文菜单时可见的两个重命名输入字段中的哪一个。"u8);
    }

    private void DrawHeightUnitSettings()
    {
        Im.Item.SetNextWidthScaled(300);
        using (var combo = Im.Combo.Begin("##heightUnit"u8, config.HeightDisplayType.Tooltip()))
        {
            if (combo)
                foreach (var type in HeightDisplayType.Values)
                {
                    if (Im.Selectable(type.Tooltip(), type == config.HeightDisplayType) && type != config.HeightDisplayType)
                    {
                        config.HeightDisplayType = type;
                        config.Save();
                    }
                }
        }

        LunaStyle.DrawAlignedHelpMarkerLabel("人物身高显示单位"u8,
            "选择如何以真实世界单位显示角色的身高。"u8);
    }

    private string _newIgnoredMod = string.Empty;

    private void DrawIgnoredMods()
    {
        using var header = Im.Tree.HeaderId("忽略的模组"u8);
        Im.Tooltip.OnHover("在解锁选项卡中为“modded”列添加忽略的模组。"u8);
        if (!header)
            return;

        using var listBox = Im.ListBox.Begin("##box"u8, new Vector2(0.4f * Im.ContentRegion.Available.X, Im.Style.FrameHeightWithSpacing * 10));
        if (!listBox)
            return;

        var       delete    = string.Empty;
        using var alignment = ImStyleDouble.ButtonTextAlign.PushX(0);
        foreach (var (idx, mod) in ignoredMods.Index())
        {
            using var id = Im.Id.Push(idx);
            if (ImEx.Icon.Button(LunaStyle.DeleteIcon, "删除这个忽略的模组。"u8))
                delete = mod;

            Im.Line.SameInner();
            ImEx.TextFramed(mod, Im.ContentRegion.Available with { Y = Im.Style.FrameHeight });
        }

        if (delete.Length > 0)
            ignoredMods.Remove(delete);

        var tt = _newIgnoredMod.Length is 0      ? "请输入一个新的模组名称或模组目录来忽略。"u8 :
            ignoredMods.Contains(_newIgnoredMod) ? "这个模组已经被忽略了。"u8 :
                                                   "在解锁选项卡中忽略所有具有此名称或目录的模组。"u8;
        if (ImEx.Icon.Button(LunaStyle.AddObjectIcon, tt, tt[0] is not (byte)'I'))
        {
            ignoredMods.Add(_newIgnoredMod);
            _newIgnoredMod = string.Empty;
        }

        Im.Line.SameInner();
        Im.Item.SetNextWidthFull();
        Im.Input.Text("##newMod"u8, ref _newIgnoredMod, "忽略这个模组..."u8);
    }

    private void DrawRoughnessSettings()
    {
        Im.Item.SetNextWidthScaled(300);
        using (var combo = Im.Combo.Begin("##alwaysEditAsRoughness"u8, config.RoughnessSetting.ToNameU8()))
        {
            if (combo)
                foreach (var type in RoughnessSetting.Values)
                {
                    if (Im.Selectable(type.ToNameU8(), config.RoughnessSetting == type))
                    {
                        config.RoughnessSetting = type;
                        config.Save();
                    }
                }
        }

        LunaStyle.DrawAlignedHelpMarkerLabel("光泽强度和粗糙度显示类型"u8,
            "选择如何显示和编辑光泽强度和粗糙度值。\n使用的转换公式是一个近似值，并未考虑到传统着色器与PBR着色器之间的所有细微差别。"u8);
    }
}
