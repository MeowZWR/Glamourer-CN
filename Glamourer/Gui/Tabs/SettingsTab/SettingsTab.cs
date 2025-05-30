using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;
using Glamourer.Automation;
using Glamourer.Designs;
using Glamourer.Gui.Tabs.DesignTab;
using Glamourer.Interop;
using Glamourer.Interop.PalettePlus;
using ImGuiNET;
using OtterGui.Raii;
using OtterGui.Text;
using OtterGui.Widgets;

namespace Glamourer.Gui.Tabs.SettingsTab;

public class SettingsTab(
    Configuration config,
    DesignFileSystemSelector selector,
    ContextMenuService contextMenuService,
    IUiBuilder uiBuilder,
    GlamourerChangelog changelog,
    IKeyState keys,
    DesignColorUi designColorUi,
    PaletteImport paletteImport,
    CollectionOverrideDrawer overrides,
    CodeDrawer codeDrawer,
    Glamourer glamourer,
    AutoDesignApplier autoDesignApplier)
    : ITab
{
    private readonly VirtualKey[] _validKeys = keys.GetValidVirtualKeys().Prepend(VirtualKey.NO_KEY).ToArray();

    public ReadOnlySpan<byte> Label
        => "插件设置"u8;

    public void DrawContent()
    {
        using var child = ImUtf8.Child("MainWindowChild"u8, default);
        if (!child)
            return;

        Checkbox("启用自动执行"u8,
            "启用后，自动使用与角色关联的设计，这些设计可以在自动执行标签页中配置。"u8,
            config.EnableAutoDesigns, v =>
            {
                config.EnableAutoDesigns = v;
                autoDesignApplier.OnEnableAutoDesignsChanged(v);
            });
        ImGui.NewLine();
        ImGui.NewLine();
        ImGui.NewLine();
        ImGui.NewLine();
        ImGui.NewLine();

        using (ImUtf8.Child("SettingsChild"u8, default))
        {
            DrawBehaviorSettings();
            DrawDesignDefaultSettings();
            DrawInterfaceSettings();
            DrawColorSettings();
            overrides.Draw();
            codeDrawer.Draw();
        }

        MainWindow.DrawSupportButtons(glamourer, changelog.Changelog);
    }

    private void DrawBehaviorSettings()
    {
        if (!ImUtf8.CollapsingHeader("行为设置"u8))
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
        Checkbox("自动重新加载装备"u8,
            "在更改Penumbra模组选项时，自动在自己的角色身上重新加载装备部件。"u8,
            config.AutoRedrawEquipOnChanges, v => config.AutoRedrawEquipOnChanges = v);
        Checkbox("在更换区域时撤销手动更改"u8,
            "当你更换区域时，撤销你对角色进行的手动更改，恢复到游戏基础状态或自动执行状态。"u8,
            config.RevertManualChangesOnZoneChange, v => config.RevertManualChangesOnZoneChange = v);
        PaletteImportButton();
        Checkbox("始终应用关联的模组"u8,
            "无论何时将设计应用于角色（包括自动执行）时，Glamourer都会尝试将设计相关的模组设置应用于当前与该角色相关的合集（如果可用）。\n\n"u8
          + "Glamourer不会自动还原这些应用的设置。这可能会打乱你的合集和配置。\n\n"u8
          + "如果你启用此设置，你应意识到任何由此产生的配置错误都是你自己造成的。"u8,
            config.AlwaysApplyAssociatedMods, v => config.AlwaysApplyAssociatedMods = v);
        Checkbox("使用临时模组设置"u8,
            "将所有设置应用为临时设置，以便在 Glamourer 或游戏关闭时重置。"u8,
            config.UseTemporarySettings,
            v => config.UseTemporarySettings = v);
        Checkbox("Prevent Random Design Repeats"u8,
            "When using random designs, prevent the same design from being chosen twice in a row."u8,
            config.PreventRandomRepeats, v => config.PreventRandomRepeats = v);
        ImGui.NewLine();
    }

    private void DrawDesignDefaultSettings()
    {
        if (!ImUtf8.CollapsingHeader("设计默认设置"))
            return;

        Checkbox("在快速设计栏中显示"u8, "新创建的设计将默认显示在快速设计栏中。"u8,
            config.DefaultDesignSettings.ShowQuickDesignBar, v => config.DefaultDesignSettings.ShowQuickDesignBar = v);
        Checkbox("重置高级染色"u8, "新创建的设计将在应用时默认配置为重置高级染色。"u8,
            config.DefaultDesignSettings.ResetAdvancedDyes, v => config.DefaultDesignSettings.ResetAdvancedDyes = v);
        Checkbox("始终强制重绘"u8, "新创建的设计将在应用时默认配置为强制角色重绘。"u8,
            config.DefaultDesignSettings.AlwaysForceRedrawing, v => config.DefaultDesignSettings.AlwaysForceRedrawing = v);
        Checkbox("重置临时设置"u8,
            "新创建的设计将在应用时默认配置为清除 Glamourer 应用到合集的所有高级设置。"u8,
            config.DefaultDesignSettings.ResetTemporarySettings, v => config.DefaultDesignSettings.ResetTemporarySettings = v);
    }

    private void DrawInterfaceSettings()
    {
        if (!ImUtf8.CollapsingHeader("界面设置"u8))
            return;

        EphemeralCheckbox("显示快速设计栏"u8,
            "显示一个与主窗口分离的工具栏，允许您快速应用设计、恢复角色和目标。"u8,
            config.Ephemeral.ShowDesignQuickBar, v => config.Ephemeral.ShowDesignQuickBar = v);
        EphemeralCheckbox("锁定快速设计栏"u8, "防止快速设计栏被移动，将其锁定在当前位置。"u8,
            config.Ephemeral.LockDesignQuickBar,
            v => config.Ephemeral.LockDesignQuickBar = v);
        if (Widget.ModifiableKeySelector("快速设计栏开关热键", "设置一个用于打开或关闭快速设计栏的热键。",
                100 * ImGuiHelpers.GlobalScale,
                config.ToggleQuickDesignBar, v => config.ToggleQuickDesignBar = v, _validKeys))
            config.Save();

        Checkbox("在主窗口中显示快速设计栏"u8,
            "也在主窗口的选项卡的选择区域显示快速设计栏。"u8,
            config.ShowQuickBarInTabs, v => config.ShowQuickBarInTabs = v);
        DrawQuickDesignBoxes();

        ImGui.Dummy(Vector2.Zero);
        ImGui.Separator();
        ImGui.Dummy(Vector2.Zero);

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
        ImGui.Dummy(Vector2.Zero);
        ImGui.Separator();
        ImGui.Dummy(Vector2.Zero);

        Checkbox("装备面板紧凑显示"u8, "使用不显示装备图标、有小染色按钮的单行视图，取代两行视图。"u8,
            config.SmallEquip,              v => config.SmallEquip = v);
        DrawHeightUnitSettings();
        Checkbox("显示应用复选框"u8,
            "显示“角色设计”选项卡下外貌和装备面板中的应用生效复选框，而不是仅在“应用规则”面板中显示。"u8,
            !config.HideApplyCheckmarks, v => config.HideApplyCheckmarks = !v);
        if (Widget.DoubleModifierSelector("删除组合键",
                "在所有的删除按钮上生效所需要的组合键。", 100 * ImGuiHelpers.GlobalScale,
                config.DeleteDesignModifier, v => config.DeleteDesignModifier = v))
            config.Save();
        if (Widget.DoubleModifierSelector("Incognito Modifier",
                "A modifier you need to hold while clicking the Incognito button for it to take effect.", 100 * ImGuiHelpers.GlobalScale,
                config.IncognitoModifier, v => config.IncognitoModifier = v))
            config.Save();
        DrawRenameSettings();
        Checkbox("自动展开角色设计折叠组"u8,
            "登录游戏后，角色设计折叠组默认状态是打开还是关闭。"u8, config.OpenFoldersByDefault,
            v => config.OpenFoldersByDefault = v);
        DrawFolderSortType();

        ImGui.NewLine();
        ImUtf8.Text("在各自的标签页中显示以下面板："u8);
        ImGui.Dummy(Vector2.Zero);
        DesignPanelFlagExtensions.DrawTable("##panelTable"u8, config.HideDesignPanel, config.AutoExpandDesignPanel, v =>
        {
            config.HideDesignPanel = v;
            config.Save();
        }, v =>
        {
            config.AutoExpandDesignPanel = v;
            config.Save();
        });


        ImGui.Dummy(Vector2.Zero);
        ImGui.Separator();
        ImGui.Dummy(Vector2.Zero);

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
            config.ShowColorConfig,             v => config.ShowColorConfig = v);
        Checkbox("显示 Palette+ 导入按钮"u8,
            "在高级外貌选项部分显示导入按钮，允许您将 Palette+ 调色板导入到设计中。"u8,
            config.ShowPalettePlusImport, v => config.ShowPalettePlusImport = v);
        using (ImRaii.PushId(1))
        {
            PaletteImportButton();
        }

        Checkbox("高级染色窗口吸附"u8,
            "保持高级染色窗口与主窗口吸附，取消勾选使其可自由移动。"u8,
            config.KeepAdvancedDyesAttached, v => config.KeepAdvancedDyesAttached = v);

        Checkbox("调试模式"u8, "显示调试选项卡，仅对调试和进阶用法有帮助。一般不建议使用。"u8,
            config.DebugMode,
            v => config.DebugMode = v);
        ImGui.NewLine();
    }

    private void DrawQuickDesignBoxes()
    {
        var showAuto   = config.EnableAutoDesigns;
        var numColumns = 9 - (showAuto ? 0 : 2) - (config.UseTemporarySettings ? 0 : 1);
        ImGui.NewLine();
        ImUtf8.Text("在快速设计栏中显示以下按钮："u8);
        ImGui.Dummy(Vector2.Zero);
        using var table = ImUtf8.Table("##tableQdb"u8, numColumns,
            ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.Borders | ImGuiTableFlags.NoHostExtendX);
        if (!table)
            return;

        ReadOnlySpan<(string, bool, QdbButtons)> columns =
        [
            (" 应用设计 ", true, QdbButtons.ApplyDesign),
            (" 全部还原 ", true, QdbButtons.RevertAll),
            (" 恢复自动 ", showAuto, QdbButtons.RevertAutomation),
            (" 重新应用自动 ", showAuto, QdbButtons.ReapplyAutomation),
            (" 还原装备 ", true, QdbButtons.RevertEquip),
            (" 还原外貌 ", true, QdbButtons.RevertCustomize),
            (" 还原高级外貌 ", true, QdbButtons.RevertAdvancedCustomization),
            (" 还原高级染色 ", true, QdbButtons.RevertAdvancedDyes),
            (" 重置设置 ", config.UseTemporarySettings, QdbButtons.ResetSettings),
        ];

        for (var i = 0; i < columns.Length; ++i)
        {
            if (!columns[i].Item2)
                continue;

            ImGui.TableNextColumn();
            ImUtf8.TableHeader(columns[i].Item1);
        }

        for (var i = 0; i < columns.Length; ++i)
        {
            if (!columns[i].Item2)
                continue;

            var       flag = columns[i].Item3;
            using var id   = ImUtf8.PushId((int)flag);
            ImGui.TableNextColumn();
            var offset = (ImGui.GetContentRegionAvail().X - ImGui.GetFrameHeight()) / 2;
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);
            var value = config.QdbButtons.HasFlag(flag);
            if (!ImUtf8.Checkbox(""u8, ref value))
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

        ImGui.SameLine();
        if (ImUtf8.Button("导入调色板插件 Palette+ 的设计"u8))
            paletteImport.ImportDesigns();
        ImUtf8.HoverTooltip(
            $"从你的调色板插件Palette+的配置中导入所有存在的数据到角色设计选项卡下，目录结构为PalettePlus/[名称]（如果还无同名设计存在）。现有调色板为：\n\n\t - {string.Join("\n\t - ", paletteImport.Data.Keys)}");
    }

    /// <summary> Draw the entire Color subsection. </summary>
    private void DrawColorSettings()
    {
        if (!ImUtf8.CollapsingHeader("配色设置"u8))
            return;

        using (var tree = ImUtf8.TreeNode("自定义设计颜色"u8))
        {
            if (tree)
                designColorUi.Draw();
        }

        using (var tree = ImUtf8.TreeNode("配色"u8))
        {
            if (tree)
                foreach (var color in Enum.GetValues<ColorId>())
                {
                    var (defaultColor, name, description) = color.Data();
                    var currentColor = config.Colors.GetValueOrDefault(color, defaultColor);
                    if (Widget.ColorPicker(name, description, currentColor, c => config.Colors[color] = c, defaultColor))
                        config.Save();
                }
        }

        ImGui.NewLine();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void Checkbox(ReadOnlySpan<byte> label, ReadOnlySpan<byte> tooltip, bool current, Action<bool> setter)
    {
        using var id  = ImUtf8.PushId(label);
        var       tmp = current;
        if (ImUtf8.Checkbox(""u8, ref tmp) && tmp != current)
        {
            setter(tmp);
            config.Save();
        }

        ImGui.SameLine();
        ImUtf8.LabeledHelpMarker(label, tooltip);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void EphemeralCheckbox(ReadOnlySpan<byte> label, ReadOnlySpan<byte> tooltip, bool current, Action<bool> setter)
    {
        using var id  = ImUtf8.PushId(label);
        var       tmp = current;
        if (ImUtf8.Checkbox(""u8, ref tmp) && tmp != current)
        {
            setter(tmp);
            config.Ephemeral.Save();
        }

        ImGui.SameLine();
        ImUtf8.LabeledHelpMarker(label, tooltip);
    }

    /// <summary> Different supported sort modes as a combo. </summary>
    private void DrawFolderSortType()
    {
        var sortMode = config.SortMode;
        ImGui.SetNextItemWidth(300 * ImGuiHelpers.GlobalScale);
        using (var combo = ImUtf8.Combo("##sortMode"u8, sortMode.Name))
        {
            if (combo)
                foreach (var val in Configuration.Constants.ValidSortModes)
                {
                    if (ImUtf8.Selectable(val.Name, val.GetType() == sortMode.GetType()) && val.GetType() != sortMode.GetType())
                    {
                        config.SortMode = val;
                        selector.SetFilterDirty();
                        config.Save();
                    }

                    ImUtf8.HoverTooltip(val.Description);
                }
        }

        ImUtf8.LabeledHelpMarker("排序模式"u8, "为角色设计选项卡下的设计选择器选择一个排序方式。"u8);
    }

    private void DrawRenameSettings()
    {
        ImGui.SetNextItemWidth(300 * ImGuiHelpers.GlobalScale);
        using (var combo = ImUtf8.Combo("##renameSettings"u8, config.ShowRename.GetData().Name))
        {
            if (combo)
                foreach (var value in Enum.GetValues<RenameField>())
                {
                    var (name, desc) = value.GetData();
                    if (ImGui.Selectable(name, config.ShowRename == value))
                    {
                        config.ShowRename = value;
                        selector.SetRenameSearchPath(value);
                        config.Save();
                    }

                    ImUtf8.HoverTooltip(desc);
                }
        }

        ImGui.SameLine();
        const string tt =
            "选择在打开设计选择器中设计右键上下文菜单时可见的两个重命名输入字段中的哪一个。";
        ImGuiComponents.HelpMarker(tt);
        ImGui.SameLine();
        ImUtf8.Text("设计上下文菜单中的重命名字段"u8);
        ImUtf8.HoverTooltip(tt);
    }

    private void DrawHeightUnitSettings()
    {
        ImGui.SetNextItemWidth(300 * ImGuiHelpers.GlobalScale);
        using (var combo = ImUtf8.Combo("##heightUnit"u8, HeightDisplayTypeName(config.HeightDisplayType)))
        {
            if (combo)
                foreach (var type in Enum.GetValues<HeightDisplayType>())
                {
                    if (ImUtf8.Selectable(HeightDisplayTypeName(type), type == config.HeightDisplayType) && type != config.HeightDisplayType)
                    {
                        config.HeightDisplayType = type;
                        config.Save();
                    }
                }
        }

        ImGui.SameLine();
        const string tt = "选择如何以真实世界单位显示角色的身高。";
        ImGuiComponents.HelpMarker(tt);
        ImGui.SameLine();
        ImUtf8.Text("人物身高显示单位"u8);
        ImUtf8.HoverTooltip(tt);
    }

    private static ReadOnlySpan<byte> HeightDisplayTypeName(HeightDisplayType type)
        => type switch
        {
            HeightDisplayType.None        => "不显示"u8,
            HeightDisplayType.Centimetre  => "厘米（000.0 cm）"u8,
            HeightDisplayType.Metre       => "米（0 (0.00 m)"u8,
            HeightDisplayType.Wrong       => "英寸 (00.0 in)"u8,
            HeightDisplayType.WrongFoot   => "英尺 (0'00'')"u8,
            HeightDisplayType.Corgi       => "柯基 (0.0 柯基)"u8,
            HeightDisplayType.OlympicPool => "奥林匹克标准游泳池（0.000个游泳池）"u8,
            _                             => ""u8,
        };
}
