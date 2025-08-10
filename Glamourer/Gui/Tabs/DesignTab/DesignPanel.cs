using Dalamud.Interface;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.ImGuiNotification;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Glamourer.Api.Enums;
using Glamourer.Automation;
using Glamourer.Designs;
using Glamourer.Designs.History;
using Glamourer.GameData;
using Glamourer.Gui.Customization;
using Glamourer.Gui.Equipment;
using Glamourer.Gui.Materials;
using Glamourer.Interop;
using Glamourer.State;
using Dalamud.Bindings.ImGui;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Raii;
using OtterGui.Text;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Interop;
using static Glamourer.Gui.Tabs.HeaderDrawer;

namespace Glamourer.Gui.Tabs.DesignTab;

public class DesignPanel
{
    private readonly FileDialogManager        _fileDialog = new();
    private readonly DesignFileSystemSelector _selector;
    private readonly CustomizationDrawer      _customizationDrawer;
    private readonly DesignManager            _manager;
    private readonly ActorObjectManager       _objects;
    private readonly StateManager             _state;
    private readonly EquipmentDrawer          _equipmentDrawer;
    private readonly ModAssociationsTab       _modAssociations;
    private readonly Configuration            _config;
    private readonly DesignDetailTab          _designDetails;
    private readonly ImportService            _importService;
    private readonly DesignConverter          _converter;
    private readonly MultiDesignPanel         _multiDesignPanel;
    private readonly CustomizeParameterDrawer _parameterDrawer;
    private readonly DesignLinkDrawer         _designLinkDrawer;
    private readonly MaterialDrawer           _materials;
    private readonly EditorHistory            _history;
    private readonly Button[]                 _leftButtons;
    private readonly Button[]                 _rightButtons;


    public DesignPanel(DesignFileSystemSelector selector,
        CustomizationDrawer customizationDrawer,
        DesignManager manager,
        ActorObjectManager objects,
        StateManager state,
        EquipmentDrawer equipmentDrawer,
        ModAssociationsTab modAssociations,
        Configuration config,
        DesignDetailTab designDetails,
        DesignConverter converter,
        ImportService importService,
        MultiDesignPanel multiDesignPanel,
        CustomizeParameterDrawer parameterDrawer,
        DesignLinkDrawer designLinkDrawer,
        MaterialDrawer materials,
        EditorHistory history)
    {
        _selector            = selector;
        _customizationDrawer = customizationDrawer;
        _manager             = manager;
        _objects             = objects;
        _state               = state;
        _equipmentDrawer     = equipmentDrawer;
        _modAssociations     = modAssociations;
        _config              = config;
        _designDetails       = designDetails;
        _importService       = importService;
        _converter           = converter;
        _multiDesignPanel    = multiDesignPanel;
        _parameterDrawer     = parameterDrawer;
        _designLinkDrawer    = designLinkDrawer;
        _materials           = materials;
        _history             = history;
        _leftButtons =
        [
            new SetFromClipboardButton(this),
            new DesignUndoButton(this),
            new ExportToClipboardButton(this),
            new ApplyCharacterButton(this),
            new UndoButton(this),
        ];
        _rightButtons =
        [
            new LockButton(this),
            new IncognitoButton(_config),
        ];
    }

    private void DrawHeader()
        => HeaderDrawer.Draw(SelectionName, 0, ImGui.GetColorU32(ImGuiCol.FrameBg), _leftButtons, _rightButtons);

    private string SelectionName
        => _selector.Selected == null ? "未选择" : _selector.IncognitoMode ? _selector.Selected.Incognito : _selector.Selected.Name.Text;

    private void DrawEquipment()
    {
        using var h = DesignPanelFlag.Equipment.Header(_config);
        if (!h)
            return;

        _equipmentDrawer.Prepare();

        var usedAllStain = _equipmentDrawer.DrawAllStain(out var newAllStain, _selector.Selected!.WriteProtected());
        foreach (var slot in EquipSlotExtensions.EqdpSlots)
        {
            var data = EquipDrawData.FromDesign(_manager, _selector.Selected!, slot);
            _equipmentDrawer.DrawEquip(data);
            if (usedAllStain)
                _manager.ChangeStains(_selector.Selected, slot, newAllStain);
        }

        var mainhand = EquipDrawData.FromDesign(_manager, _selector.Selected!, EquipSlot.MainHand);
        var offhand  = EquipDrawData.FromDesign(_manager, _selector.Selected!, EquipSlot.OffHand);
        _equipmentDrawer.DrawWeapons(mainhand, offhand, true);

        foreach (var slot in BonusExtensions.AllFlags)
        {
            var data = BonusDrawData.FromDesign(_manager, _selector.Selected!, slot);
            _equipmentDrawer.DrawBonusItem(data);
        }

        ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight() / 2));
        DrawEquipmentMetaToggles();
        ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight() / 2));
        _equipmentDrawer.DrawDragDropTooltip();
    }

    private void DrawEquipmentMetaToggles()
    {
        using (var _ = ImRaii.Group())
        {
            EquipmentDrawer.DrawMetaToggle(ToggleDrawData.FromDesign(MetaIndex.HatState, _manager, _selector.Selected!));
            EquipmentDrawer.DrawMetaToggle(ToggleDrawData.CrestFromDesign(CrestFlag.Head, _manager, _selector.Selected!));
        }

        ImGui.SameLine();
        using (var _ = ImRaii.Group())
        {
            EquipmentDrawer.DrawMetaToggle(ToggleDrawData.FromDesign(MetaIndex.VisorState, _manager, _selector.Selected!));
            EquipmentDrawer.DrawMetaToggle(ToggleDrawData.CrestFromDesign(CrestFlag.Body, _manager, _selector.Selected!));
        }

        ImGui.SameLine();
        using (var _ = ImRaii.Group())
        {
            EquipmentDrawer.DrawMetaToggle(ToggleDrawData.FromDesign(MetaIndex.WeaponState, _manager, _selector.Selected!));
            EquipmentDrawer.DrawMetaToggle(ToggleDrawData.CrestFromDesign(CrestFlag.OffHand, _manager, _selector.Selected!));
        }
        ImGui.SameLine();
        using (var _ = ImRaii.Group())
        {
            EquipmentDrawer.DrawMetaToggle(ToggleDrawData.FromDesign(MetaIndex.EarState, _manager, _selector.Selected!));
        }
    }

    private void DrawCustomize()
    {
        if (_config.HideDesignPanel.HasFlag(DesignPanelFlag.Customization))
            return;

        var header = _selector.Selected!.DesignData.ModelId == 0
            ? "外貌"
            : $"外貌（模型ID#{_selector.Selected!.DesignData.ModelId}）###Customization";
        var       expand = _config.AutoExpandDesignPanel.HasFlag(DesignPanelFlag.Customization);
        using var h      = ImUtf8.CollapsingHeaderId(header, expand ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None);
        if (!h)
            return;

        if (_customizationDrawer.Draw(_selector.Selected!.DesignData.Customize, _selector.Selected.Application.Customize,
                _selector.Selected!.WriteProtected(), false))
            foreach (var idx in Enum.GetValues<CustomizeIndex>())
            {
                var flag     = idx.ToFlag();
                var newValue = _customizationDrawer.ChangeApply.HasFlag(flag);
                _manager.ChangeApplyCustomize(_selector.Selected, idx, newValue);
                if (_customizationDrawer.Changed.HasFlag(flag))
                    _manager.ChangeCustomize(_selector.Selected, idx, _customizationDrawer.Customize[idx]);
            }

        EquipmentDrawer.DrawMetaToggle(ToggleDrawData.FromDesign(MetaIndex.Wetness, _manager, _selector.Selected!));
        ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight() / 2));
    }

    private void DrawCustomizeParameters()
    {
        using var h = DesignPanelFlag.AdvancedCustomizations.Header(_config);
        if (!h)
            return;

        _parameterDrawer.Draw(_manager, _selector.Selected!);
    }

    private void DrawMaterialValues()
    {
        using var h = DesignPanelFlag.AdvancedDyes.Header(_config);
        if (!h)
            return;

        _materials.Draw(_selector.Selected!);
    }

    private void DrawCustomizeApplication()
    {
        using var id        = ImUtf8.PushId("Customizations"u8);
        var       set       = _selector.Selected!.CustomizeSet;
        var       available = set.SettingAvailable | CustomizeFlag.Clan | CustomizeFlag.Gender | CustomizeFlag.BodyType;
        var flags = _selector.Selected!.ApplyCustomizeExcludingBodyType == 0 ? 0 :
            (_selector.Selected!.ApplyCustomize & available) == available    ? 3 : 1;
        if (ImGui.CheckboxFlags("应用全部外貌数据", ref flags, 3))
        {
            var newFlags = flags == 3;
            _manager.ChangeApplyCustomize(_selector.Selected!, CustomizeIndex.Clan,   newFlags);
            _manager.ChangeApplyCustomize(_selector.Selected!, CustomizeIndex.Gender, newFlags);
            foreach (var index in CustomizationExtensions.AllBasic)
                _manager.ChangeApplyCustomize(_selector.Selected!, index, newFlags);
        }

        var applyClan = _selector.Selected!.DoApplyCustomize(CustomizeIndex.Clan);
        if (ImUtf8.Checkbox($"应用{CustomizeIndex.Clan.ToDefaultName()}", ref applyClan))
            _manager.ChangeApplyCustomize(_selector.Selected!, CustomizeIndex.Clan, applyClan);

        var applyGender = _selector.Selected!.DoApplyCustomize(CustomizeIndex.Gender);
        if (ImUtf8.Checkbox($"应用{CustomizeIndex.Gender.ToDefaultName()}", ref applyGender))
            _manager.ChangeApplyCustomize(_selector.Selected!, CustomizeIndex.Gender, applyGender);


        foreach (var index in CustomizationExtensions.All.Where(set.IsAvailable))
        {
            var apply = _selector.Selected!.DoApplyCustomize(index);
            if (ImUtf8.Checkbox($"应用{set.Option(index)}", ref apply))
                _manager.ChangeApplyCustomize(_selector.Selected!, index, apply);
        }
    }

    private void DrawCrestApplication()
    {
        using var id        = ImUtf8.PushId("队徽"u8);
        var       flags     = (uint)_selector.Selected!.Application.Crest;
        var       bigChange = ImGui.CheckboxFlags("应用所有队徽", ref flags, (uint)CrestExtensions.AllRelevant);
        foreach (var flag in CrestExtensions.AllRelevantSet)
        {
            var apply = bigChange ? ((CrestFlag)flags & flag) == flag : _selector.Selected!.DoApplyCrest(flag);
            if (ImUtf8.Checkbox($"应用{flag.ToLabel()}队徽", ref apply) || bigChange)
                _manager.ChangeApplyCrest(_selector.Selected!, flag, apply);
        }
    }

    private void DrawApplicationRules()
    {
        using var h = DesignPanelFlag.ApplicationRules.Header(_config);
        if (!h)
            return;

        using var disabled = ImRaii.Disabled(_selector.Selected!.WriteProtected());

        DrawAllButtons();

        using (var _ = ImUtf8.Group())
        {
            DrawCustomizeApplication();
            ImUtf8.IconDummy();
            DrawCrestApplication();
            ImUtf8.IconDummy();
            DrawMetaApplication();
            ImUtf8.IconDummy();
            DrawBonusSlotApplication();
        }

        ImGui.SameLine(ImGui.GetContentRegionAvail().X / 2);
        using (var _ = ImRaii.Group())
        {
            void ApplyEquip(string label, EquipFlag allFlags, bool stain, IEnumerable<EquipSlot> slots)
            {
                var       flags     = (uint)(allFlags & _selector.Selected!.Application.Equip);
                using var id        = ImUtf8.PushId(label);
                var       bigChange = ImGui.CheckboxFlags($"应用全部{label}", ref flags, (uint)allFlags);
                if (stain)
                    foreach (var slot in slots)
                    {
                        var apply = bigChange ? ((EquipFlag)flags).HasFlag(slot.ToStainFlag()) : _selector.Selected!.DoApplyStain(slot);
                        if (ImUtf8.Checkbox($"应用{slot.ToName()}染色", ref apply) || bigChange)
                            _manager.ChangeApplyStains(_selector.Selected!, slot, apply);
                    }
                else
                    foreach (var slot in slots)
                    {
                        var apply = bigChange ? ((EquipFlag)flags).HasFlag(slot.ToFlag()) : _selector.Selected!.DoApplyEquip(slot);
                        if (ImUtf8.Checkbox($"应用{slot.ToName()}", ref apply) || bigChange)
                            _manager.ChangeApplyItem(_selector.Selected!, slot, apply);
                    }
            }

            ApplyEquip("武器", ApplicationTypeExtensions.WeaponFlags, false, new[]
            {
                EquipSlot.MainHand,
                EquipSlot.OffHand,
            });

            ImUtf8.IconDummy();
            ApplyEquip("服装", ApplicationTypeExtensions.ArmorFlags, false, EquipSlotExtensions.EquipmentSlots);

            ImUtf8.IconDummy();
            ApplyEquip("饰品", ApplicationTypeExtensions.AccessoryFlags, false, EquipSlotExtensions.AccessorySlots);

            ImUtf8.IconDummy();
            ApplyEquip("染色", ApplicationTypeExtensions.StainFlags, true,
                EquipSlotExtensions.FullSlots);

            ImUtf8.IconDummy();
            DrawParameterApplication();
        }
    }

    private void DrawAllButtons()
    {
        var   enabled   = _config.DeleteDesignModifier.IsActive();
        bool? equip     = null;
        bool? customize = null;
        var   size      = new Vector2(200 * ImUtf8.GlobalScale, 0);
        if (ImUtf8.ButtonEx("禁用所有"u8,
            "禁用所有应用，包括任何现有的高级染色、高级外貌、队徽和湿身效果。"u8, size,
            !enabled))
        {
            equip     = false;
            customize = false;
        }

        if (!enabled)
            ImUtf8.HoverTooltip(ImGuiHoveredFlags.AllowWhenDisabled, $"点击时按住 {_config.DeleteDesignModifier}。");

        ImGui.SameLine();
        if (ImUtf8.ButtonEx("启用所有"u8,
            "启用所有应用，包括任何现有的高级染色、高级外貌、队徽和湿身效果。"u8, size,
            !enabled))
        {
            equip     = true;
            customize = true;
        }

        if (!enabled)
            ImUtf8.HoverTooltip(ImGuiHoveredFlags.AllowWhenDisabled, $"点击时按住 {_config.DeleteDesignModifier}。");

        if (ImUtf8.ButtonEx("仅装备"u8,
            "启用与装备相关的所有应用，禁用与装备无关的所有应用。"u8, size,
            !enabled))
        {
            equip     = true;
            customize = false;
        }

        if (!enabled)
            ImUtf8.HoverTooltip(ImGuiHoveredFlags.AllowWhenDisabled, $"点击时按住 {_config.DeleteDesignModifier}。");

        ImGui.SameLine();
        if (ImUtf8.ButtonEx("仅外貌"u8,
            "启用与外貌相关的所有应用，禁用与外貌无关的所有应用。"u8, size,
            !enabled))
        {
            equip     = false;
            customize = true;
        }

        if (!enabled)
            ImUtf8.HoverTooltip(ImGuiHoveredFlags.AllowWhenDisabled, $"点击时按住 {_config.DeleteDesignModifier}。");

        if (ImUtf8.ButtonEx("默认应用"u8,
            "将应用规则设置为默认值，就像设计是新创建的一样，没有任何高级功能或湿身效果。"u8,
            size,
            !enabled))
        {
            _manager.ChangeApplyMulti(_selector.Selected!, true, true, true, false, true, true, false, true);
            _manager.ChangeApplyMeta(_selector.Selected!, MetaIndex.Wetness, false);
        }

        if (!enabled)
            ImUtf8.HoverTooltip(ImGuiHoveredFlags.AllowWhenDisabled, $"点击时按住 {_config.DeleteDesignModifier}。");

        ImGui.SameLine();
        if (ImUtf8.ButtonEx("禁用高级"u8, "禁用所有高级染色和外貌，但保留其他所有设置。"u8,
            size,
            !enabled))
            _manager.ChangeApplyMulti(_selector.Selected!, null, null, null, false, null, null, false, null);

        if (!enabled)
            ImUtf8.HoverTooltip(ImGuiHoveredFlags.AllowWhenDisabled, $"点击时按住 {_config.DeleteDesignModifier}。");

        if (equip is null && customize is null)
            return;

        _manager.ChangeApplyMulti(_selector.Selected!, equip, customize, equip, customize.HasValue && !customize.Value ? false : null, null,
            equip, equip, equip);
        if (equip.HasValue)
        {
            _manager.ChangeApplyMeta(_selector.Selected!, MetaIndex.HatState,    equip.Value);
            _manager.ChangeApplyMeta(_selector.Selected!, MetaIndex.VisorState,  equip.Value);
            _manager.ChangeApplyMeta(_selector.Selected!, MetaIndex.WeaponState, equip.Value);
        }

        if (customize.HasValue)
            _manager.ChangeApplyMeta(_selector.Selected!, MetaIndex.Wetness, customize.Value);
    }

    private static readonly IReadOnlyList<string> MetaLabels =
    [
        "应用湿身",
        "应用头部装备可见性",
        "应用头部装备状态",
        "应用武器可见性",
    ];

    private void DrawMetaApplication()
    {
        using var  id        = ImUtf8.PushId("Meta");
        const uint all       = (uint)MetaExtensions.All;
        var        flags     = (uint)_selector.Selected!.Application.Meta;
        var        bigChange = ImGui.CheckboxFlags("应用全部元数据修改", ref flags, all);

        foreach (var (index, label) in MetaExtensions.AllRelevant.Zip(MetaLabels))
        {
            var apply = bigChange ? ((MetaFlag)flags).HasFlag(index.ToFlag()) : _selector.Selected!.DoApplyMeta(index);
            if (ImUtf8.Checkbox(label, ref apply) || bigChange)
                _manager.ChangeApplyMeta(_selector.Selected!, index, apply);
        }
    }

    private static readonly IReadOnlyList<string> BonusSlotLabels =
    [
        "应用面甲",
    ];

    private void DrawBonusSlotApplication()
    {
        using var id        = ImUtf8.PushId("Bonus"u8);
        var       flags     = _selector.Selected!.Application.BonusItem;
        var       bigChange = BonusExtensions.AllFlags.Count > 1 && ImUtf8.Checkbox("Apply All Bonus Slots"u8, ref flags, BonusExtensions.All);
        foreach (var (index, label) in BonusExtensions.AllFlags.Zip(BonusSlotLabels))
        {
            var apply = bigChange ? flags.HasFlag(index) : _selector.Selected!.DoApplyBonusItem(index);
            if (ImUtf8.Checkbox(label, ref apply) || bigChange)
                _manager.ChangeApplyBonusItem(_selector.Selected!, index, apply);
        }
    }


    private void DrawParameterApplication()
    {
        using var id        = ImUtf8.PushId("Parameter");
        var       flags     = (uint)_selector.Selected!.Application.Parameters;
        var       bigChange = ImGui.CheckboxFlags("应用所有外貌参数", ref flags, (uint)CustomizeParameterExtensions.All);
        foreach (var flag in CustomizeParameterExtensions.AllFlags)
        {
            var apply = bigChange ? ((CustomizeParameterFlag)flags).HasFlag(flag) : _selector.Selected!.DoApplyParameter(flag);
            if (ImUtf8.Checkbox($"应用{flag.ToName()}", ref apply) || bigChange)
                _manager.ChangeApplyParameter(_selector.Selected!, flag, apply);
        }
    }

    public void Draw()
    {
        using var group = ImUtf8.Group();
        if (_selector.SelectedPaths.Count > 1)
        {
            _multiDesignPanel.Draw();
        }
        else
        {
            DrawHeader();
            DrawPanel();

            if (_selector.Selected == null || _selector.Selected.WriteProtected())
                return;

            if (_importService.CreateDatTarget(out var dat))
            {
                _manager.ChangeCustomize(_selector.Selected!, CustomizeIndex.Clan,   dat.Customize[CustomizeIndex.Clan]);
                _manager.ChangeCustomize(_selector.Selected!, CustomizeIndex.Gender, dat.Customize[CustomizeIndex.Gender]);
                foreach (var idx in CustomizationExtensions.AllBasic)
                    _manager.ChangeCustomize(_selector.Selected!, idx, dat.Customize[idx]);
                Glamourer.Messager.NotificationMessage(
                    $"应用了游戏 .dat 文件 {dat.Description} 的自定义设置到 {_selector.Selected.Name}。", NotificationType.Success, false);
            }
            else if (_importService.CreateCharaTarget(out var designBase, out var name))
            {
                _manager.ApplyDesign(_selector.Selected!, designBase);
                Glamourer.Messager.NotificationMessage($"应用了 Anamnesis .chara 文件 {name} 到 {_selector.Selected.Name}。",
                    NotificationType.Success, false);
            }
        }

        _importService.CreateDatSource();
    }

    private void DrawPanel()
    {
        using var table = ImUtf8.Table("##Panel", 1, ImGuiTableFlags.BordersOuter | ImGuiTableFlags.ScrollY, ImGui.GetContentRegionAvail());
        if (!table || _selector.Selected == null)
            return;

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableNextColumn();
        if (_selector.Selected == null)
            return;

        ImGui.Dummy(Vector2.Zero);
        DrawButtonRow();
        ImGui.TableNextColumn();

        DrawCustomize();
        DrawEquipment();
        DrawCustomizeParameters();
        DrawMaterialValues();
        _designDetails.Draw();
        DrawApplicationRules();
        _modAssociations.Draw();
        _designLinkDrawer.Draw();
    }

    private void DrawButtonRow()
    {
        DrawApplyToSelf();
        ImGui.SameLine();
        DrawApplyToTarget();
        ImGui.SameLine();
        _modAssociations.DrawApplyButton();
        ImGui.SameLine();
        DrawSaveToDat();
    }


    private void DrawApplyToSelf()
    {
        var (id, data) = _objects.PlayerData;
        if (!ImGuiUtil.DrawDisabledButton("应用到自己", Vector2.Zero,
                "将当前设计按其中设置应用到你的角色。\n按住CTRL仅应用装备。\n按住Shift仅应用外貌。",
                !data.Valid))
            return;

        if (_state.GetOrCreate(id, data.Objects[0], out var state))
        {
            using var _ = _selector.Selected!.TemporarilyRestrictApplication(ApplicationCollection.FromKeys());
            _state.ApplyDesign(state, _selector.Selected!, ApplySettings.ManualWithLinks with { IsFinal = true });
        }
    }

    private void DrawApplyToTarget()
    {
        var (id, data) = _objects.TargetData;
        var tt = id.IsValid
            ? data.Valid
                ? "将当前设计按其中设置应用到你的目标。\n按住CTRL仅应用装备。\n按住Shift仅应用外貌。"
                : "无法应用到当前目标。"
            : "未选中有效目标。";
        if (!ImGuiUtil.DrawDisabledButton("应用到目标", Vector2.Zero, tt, !data.Valid))
            return;

        if (_state.GetOrCreate(id, data.Objects[0], out var state))
        {
            using var _ = _selector.Selected!.TemporarilyRestrictApplication(ApplicationCollection.FromKeys());
            _state.ApplyDesign(state, _selector.Selected!, ApplySettings.ManualWithLinks with { IsFinal = true });
        }
    }

    private void DrawSaveToDat()
    {
        var verified = _importService.Verify(_selector.Selected!.DesignData.Customize, out _);
        var tt = verified
            ? "将当前设计的外貌数据导出为游戏角色创建时可读取的文档。"
            : "当前设计包含无法在创建角色时使用的外貌数据。";
        var startPath = GetUserPath();
        if (startPath.Length == 0)
            startPath = null;
        if (ImGuiUtil.DrawDisabledButton("导出为Dat", Vector2.Zero, tt, !verified))
            _fileDialog.SaveFileDialog("保存文件...", ".dat", "FFXIV_CHARA_01.dat", ".dat", (v, path) =>
            {
                if (v && _selector.Selected != null)
                    _importService.SaveDesignAsDat(path, _selector.Selected!.DesignData.Customize, _selector.Selected!.Name);
            }, startPath);

        _fileDialog.Draw();
    }

    private static unsafe string GetUserPath()
        => Framework.Instance()->UserPathString;


    private sealed class LockButton(DesignPanel panel) : Button
    {
        public override bool Visible
            => panel._selector.Selected != null;

        protected override string Description
            => panel._selector.Selected!.WriteProtected()
                ? "移除写保护，使其可以被编辑。"
                : "启用写保护，使其不能被编辑。";

        protected override FontAwesomeIcon Icon
            => panel._selector.Selected!.WriteProtected()
                ? FontAwesomeIcon.Lock
                : FontAwesomeIcon.LockOpen;

        protected override void OnClick()
            => panel._manager.SetWriteProtection(panel._selector.Selected!, !panel._selector.Selected!.WriteProtected());
    }

    private sealed class SetFromClipboardButton(DesignPanel panel) : Button
    {
        public override bool Visible
            => panel._selector.Selected != null;

        protected override bool Disabled
            => panel._selector.Selected?.WriteProtected() ?? true;

        protected override string Description
            => "尝试使用剪贴板中的设计数据覆盖此设计。\n按住CTRL仅应用装备。\n按住Shift仅应用外貌。";

        protected override FontAwesomeIcon Icon
            => FontAwesomeIcon.Clipboard;

        protected override void OnClick()
        {
            try
            {
                var text = ImGui.GetClipboardText();
                var (applyEquip, applyCustomize) = UiHelpers.ConvertKeysToBool();
                var design = panel._converter.FromBase64(text, applyCustomize, applyEquip, out _)
                 ?? throw new Exception("剪贴板未包含有效数据。");
                panel._manager.ApplyDesign(panel._selector.Selected!, design);
            }
            catch (Exception ex)
            {
                Glamourer.Messager.NotificationMessage(ex, $"无法将剪贴板数据应用于{panel._selector.Selected!.Name}。",
                    $"无法将剪贴板数据应用于设计{panel._selector.Selected!.Identifier}", NotificationType.Error, false);
            }
        }
    }

    private sealed class DesignUndoButton(DesignPanel panel) : Button
    {
        public override bool Visible
            => panel._selector.Selected != null;

        protected override bool Disabled
            => !panel._manager.CanUndo(panel._selector.Selected) || (panel._selector.Selected?.WriteProtected() ?? true);

        protected override string Description
            => "如果不小心用不同的设计覆盖了您的设计，请撤消上一次更改。";

        protected override FontAwesomeIcon Icon
            => FontAwesomeIcon.SyncAlt;

        protected override void OnClick()
        {
            try
            {
                panel._manager.UndoDesignChange(panel._selector.Selected!);
            }
            catch (Exception ex)
            {
                Glamourer.Messager.NotificationMessage(ex, $"无法为{panel._selector.Selected!.Name}撤消上次更改。",
                    NotificationType.Error,
                    false);
            }
        }
    }

    private sealed class ExportToClipboardButton(DesignPanel panel) : Button
    {
        public override bool Visible
            => panel._selector.Selected != null;

        protected override string Description
            => "复制当前设计的数据到剪贴板。";

        protected override FontAwesomeIcon Icon
            => FontAwesomeIcon.Copy;

        protected override void OnClick()
        {
            try
            {
                var text = panel._converter.ShareBase64(panel._selector.Selected!);
                ImGui.SetClipboardText(text);
            }
            catch (Exception ex)
            {
                Glamourer.Messager.NotificationMessage(ex, $"无法复制{panel._selector.Selected!.Name}的数据到剪贴板。",
                    $"无法复制来自设计{panel._selector.Selected!.Identifier}的数据到剪贴板。", NotificationType.Error, false);
            }
        }
    }

    private sealed class ApplyCharacterButton(DesignPanel panel) : Button
    {
        public override bool Visible
            => panel._selector.Selected != null && panel._objects.Player.Valid;

        protected override string Description
            => "用你的角色的当前状态覆盖此设计。";

        protected override bool Disabled
            => panel._selector.Selected?.WriteProtected() ?? true;

        protected override FontAwesomeIcon Icon
            => FontAwesomeIcon.UserEdit;

        protected override void OnClick()
        {
            try
            {
                var (player, actor) = panel._objects.PlayerData;
                if (!player.IsValid || !actor.Valid || !panel._state.GetOrCreate(player, actor.Objects[0], out var state))
                    throw new Exception("没有可用的玩家状态。");

                var design = panel._converter.Convert(state, ApplicationRules.FromModifiers(state))
                 ?? throw new Exception("剪贴板中没有可用数据。");
                panel._selector.Selected!.GetMaterialDataRef().Clear();
                panel._manager.ApplyDesign(panel._selector.Selected!, design);
            }
            catch (Exception ex)
            {
                Glamourer.Messager.NotificationMessage(ex, $"无法应用玩家状态到{panel._selector.Selected!.Name}。",
                    $"无法应用玩家状态到设计：{panel._selector.Selected!.Identifier}", NotificationType.Error, false);
            }
        }
    }

    private sealed class UndoButton(DesignPanel panel) : Button
    {
        protected override string Description
            => "撤消上次更改。";

        protected override FontAwesomeIcon Icon
            => FontAwesomeIcon.Undo;

        public override bool Visible
            => panel._selector.Selected != null;

        protected override bool Disabled
            => (panel._selector.Selected?.WriteProtected() ?? true) || !panel._history.CanUndo(panel._selector.Selected);

        protected override void OnClick()
            => panel._history.Undo(panel._selector.Selected!);
    }
}
