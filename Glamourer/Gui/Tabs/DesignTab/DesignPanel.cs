using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.ImGuiNotification;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Glamourer.Api.Enums;
using Glamourer.Automation;
using Glamourer.Config;
using Glamourer.Designs;
using Glamourer.GameData;
using Glamourer.Gui.Customization;
using Glamourer.Gui.Equipment;
using Glamourer.Gui.Materials;
using Glamourer.Interop;
using Glamourer.Interop.Material;
using Glamourer.Services;
using ImSharp;
using Luna;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Interop;

namespace Glamourer.Gui.Tabs.DesignTab;

public class DesignPanel : IPanel
{
    public ReadOnlySpan<byte> Id
        => "DesignPanel"u8;

    private readonly FileDialogManager        _fileDialog = new();
    private readonly CustomizationDrawer      _customizationDrawer;
    private readonly DesignFileSystem         _fileSystem;
    private readonly DesignManager            _manager;
    private readonly ActorObjectManager       _objects;
    private readonly EquipmentDrawer          _equipmentDrawer;
    private readonly ModAssociationsTab       _modAssociations;
    private readonly CustomizePlusAssociationsTab _customizePlusAssociations;
    private readonly Configuration            _config;
    private readonly DesignDetailTab          _designDetails;
    private readonly ImportService            _importService;
    private readonly MultiDesignPanel         _multiDesignPanel;
    private readonly CustomizeParameterDrawer _parameterDrawer;
    private readonly DesignLinkDrawer         _designLinkDrawer;
    private readonly MaterialDrawer           _materials;
    private readonly DesignApplier            _designApplier;

    public DesignPanel(CustomizationDrawer customizationDrawer,
        DesignManager manager,
        ActorObjectManager objects,
        EquipmentDrawer equipmentDrawer,
        ModAssociationsTab modAssociations,
        CustomizePlusAssociationsTab customizePlusAssociations,
        Configuration config,
        DesignDetailTab designDetails,
        DesignConverter converter,
        ImportService importService,
        MultiDesignPanel multiDesignPanel,
        CustomizeParameterDrawer parameterDrawer,
        DesignLinkDrawer designLinkDrawer,
        MaterialDrawer materials,
        DesignFileSystem fileSystem,
        DesignApplier designApplier)
    {
        _customizationDrawer = customizationDrawer;
        _manager             = manager;
        _objects             = objects;
        _equipmentDrawer     = equipmentDrawer;
        _modAssociations     = modAssociations;
        _customizePlusAssociations = customizePlusAssociations;
        _config              = config;
        _designDetails       = designDetails;
        _importService       = importService;
        _multiDesignPanel    = multiDesignPanel;
        _parameterDrawer     = parameterDrawer;
        _designLinkDrawer    = designLinkDrawer;
        _materials           = materials;
        _fileSystem          = fileSystem;
        _designApplier       = designApplier;
    }

    private Design Selection
        => (Design)_fileSystem.Selection.Selection!.Value;

    private void DrawEquipment()
    {
        using var h = DesignPanelFlag.Equipment.Header(_config);
        if (!h)
            return;

        _equipmentDrawer.Prepare(false);

        var usedAllStain = _equipmentDrawer.DrawAllStain(out var newAllStain, Selection.WriteProtected());
        Im.Line.Same();
        EquipmentDrawer.DrawKeepItemFilter(_config);
        foreach (var slot in EquipSlotExtensions.EqdpSlots)
        {
            var data = EquipDrawData.FromDesign(_manager, Selection, slot);
            _equipmentDrawer.DrawEquip(data);
            if (usedAllStain)
                _manager.ChangeStains(Selection, slot, newAllStain);
        }

        var mainhand = EquipDrawData.FromDesign(_manager, Selection, EquipSlot.MainHand);
        var offhand  = EquipDrawData.FromDesign(_manager, Selection, EquipSlot.OffHand);
        _equipmentDrawer.DrawWeapons(mainhand, offhand, true);

        foreach (var slot in BonusExtensions.AllFlags)
        {
            var data = BonusDrawData.FromDesign(_manager, Selection, slot);
            _equipmentDrawer.DrawBonusItem(data);
        }

        Im.Dummy(new Vector2(Im.Style.TextHeight / 2));
        DrawEquipmentMetaToggles();
        Im.Dummy(new Vector2(Im.Style.TextHeight / 2));
        _equipmentDrawer.DrawDragDropTooltip();
    }

    private void DrawEquipmentMetaToggles()
    {
        using (Im.Group())
        {
            EquipmentDrawer.DrawMetaToggle(ToggleDrawData.FromDesign(MetaIndex.HatState, _manager, Selection));
            EquipmentDrawer.DrawMetaToggle(ToggleDrawData.CrestFromDesign(CrestFlag.Head, _manager, Selection));
        }

        Im.Line.Same();
        using (Im.Group())
        {
            EquipmentDrawer.DrawMetaToggle(ToggleDrawData.FromDesign(MetaIndex.VisorState, _manager, Selection));
            EquipmentDrawer.DrawMetaToggle(ToggleDrawData.CrestFromDesign(CrestFlag.Body, _manager, Selection));
        }

        Im.Line.Same();
        using (Im.Group())
        {
            EquipmentDrawer.DrawMetaToggle(ToggleDrawData.FromDesign(MetaIndex.WeaponState, _manager, Selection));
            EquipmentDrawer.DrawMetaToggle(ToggleDrawData.CrestFromDesign(CrestFlag.OffHand, _manager, Selection));
        }

        Im.Line.Same();
        using (Im.Group())
        {
            EquipmentDrawer.DrawMetaToggle(ToggleDrawData.FromDesign(MetaIndex.EarState, _manager, Selection));
        }
    }

    private void DrawCustomize()
    {
        if (_config.HideDesignPanel.HasFlag(DesignPanelFlag.Customization))
            return;

        var expand = _config.AutoExpandDesignPanel.HasFlag(DesignPanelFlag.Customization);
        using var h = Im.Tree.HeaderId(Selection.DesignData.ModelId is 0
                ? "外貌###Customization"u8
                : $"外貌（模型ID #{Selection.DesignData.ModelId})###Customization",
            expand ? TreeNodeFlags.DefaultOpen : TreeNodeFlags.None);
        if (!h)
            return;

        if (_customizationDrawer.Draw(Selection.DesignData.Customize, Selection.Application.Customize,
                Selection.WriteProtected(), false, false,
                Selection.GetMaterialDataRef().CheckExistenceSlots(ModelCombinedSlotsExtensions.AllCustomization)))
            foreach (var idx in CustomizeIndex.Values)
            {
                var flag     = idx.ToFlag();
                var newValue = _customizationDrawer.ChangeApply.HasFlag(flag);
                _manager.ChangeApplyCustomize(Selection, idx, newValue);
                if (_customizationDrawer.Changed.HasFlag(flag))
                    _manager.ChangeCustomize(Selection, idx, _customizationDrawer.Customize[idx]);
            }

        EquipmentDrawer.DrawMetaToggle(ToggleDrawData.FromDesign(MetaIndex.Wetness, _manager, Selection));
        Im.Dummy(new Vector2(Im.Style.TextHeight / 2));
    }

    private void DrawCustomizeParameters()
    {
        using var h = DesignPanelFlag.AdvancedCustomizations.Header(_config);
        if (!h)
            return;

        _parameterDrawer.Draw(_manager, Selection);
    }

    private void DrawMaterialValues()
    {
        using var h = DesignPanelFlag.AdvancedDyes.Header(_config);
        if (!h)
            return;

        _materials.Draw(Selection);
    }

    private void DrawCustomizeApplication()
    {
        using var id        = Im.Id.Push("Customizations"u8);
        var       set       = Selection.CustomizeSet;
        var       available = set.SettingAvailable | CustomizeFlag.Clan | CustomizeFlag.Gender | CustomizeFlag.BodyType;
        var flags = Selection.ApplyCustomizeExcludingBodyType is 0 ? 0ul :
            (Selection.ApplyCustomize & available) == available    ? 3ul : 1ul;
        if (Im.Checkbox("应用全部外貌数据"u8, ref flags, 3ul))
        {
            var newFlags = flags is 3;
            _manager.ChangeApplyCustomize(Selection, CustomizeIndex.Clan,   newFlags);
            _manager.ChangeApplyCustomize(Selection, CustomizeIndex.Gender, newFlags);
            foreach (var index in CustomizationExtensions.AllBasic)
                _manager.ChangeApplyCustomize(Selection, index, newFlags);
        }

        var applyClan = Selection.DoApplyCustomize(CustomizeIndex.Clan);
        if (Im.Checkbox($"应用{CustomizeIndex.Clan.ToNameU8()}", ref applyClan))
            _manager.ChangeApplyCustomize(Selection, CustomizeIndex.Clan, applyClan);

        var applyGender = Selection.DoApplyCustomize(CustomizeIndex.Gender);
        if (Im.Checkbox($"应用{CustomizeIndex.Gender.ToNameU8()}", ref applyGender))
            _manager.ChangeApplyCustomize(Selection, CustomizeIndex.Gender, applyGender);


        foreach (var index in CustomizationExtensions.All.Where(set.IsAvailable))
        {
            var apply = Selection.DoApplyCustomize(index);
            if (Im.Checkbox($"应用{set.Option(index)}", ref apply))
                _manager.ChangeApplyCustomize(Selection, index, apply);
        }
    }

    private void DrawCrestApplication()
    {
        using var id        = Im.Id.Push("队徽"u8);
        var       flags     = (ulong)Selection.Application.Crest;
        var       bigChange = Im.Checkbox("应用全部队徽"u8, ref flags, (ulong)CrestExtensions.AllRelevant);
        foreach (var flag in CrestExtensions.AllRelevantSet)
        {
            var apply = bigChange ? ((CrestFlag)flags & flag) == flag : Selection.DoApplyCrest(flag);
            if (Im.Checkbox($"应用{flag.ToLabel()}队徽", ref apply) || bigChange)
                _manager.ChangeApplyCrest(Selection, flag, apply);
        }
    }
    private void DrawCustomizePlusApplication()
    {
        using var id = Im.Id.Push("CustomizePlusAssociation"u8);

        var apply = Selection.ApplyCustomizePlusAssociation;
        if (Im.Checkbox("应用 Customize+ "u8, ref apply))
        {
            _manager.ChangeApplyCustomizePlusAssociation(Selection, apply);
        }

        Im.Tooltip.OnHover(
            Selection.CustomizePlusAssociation.IsSet
                ? "允许此设计在手动应用和自动执行时尝试应用已关联的 Customize+ 角色配置。"u8
                : "当前设计尚未关联任何 Customize+ 配置。"u8);
    }
    private void DrawApplicationRules()
    {
        using var h = DesignPanelFlag.ApplicationRules.Header(_config);
        if (!h)
            return;

        using var disabled = Im.Disabled(Selection.WriteProtected());

        DrawAllButtons();

        using (Im.Group())
        {
            DrawCustomizePlusApplication();
            Im.FrameDummy();
            DrawCustomizeApplication();
            Im.FrameDummy();
            DrawCrestApplication();
            Im.FrameDummy();
            DrawMetaApplication();
        }

        Im.Line.Same(210 * Im.Style.GlobalScale + Im.Style.ItemSpacing.X);
        using (Im.Group())
        {
            void ApplyEquip(string label, EquipFlag allFlags, bool stain, IEnumerable<EquipSlot> slots)
            {
                var       flags     = (ulong)(allFlags & Selection.Application.Equip);
                using var id        = Im.Id.Push(label);
                var       bigChange = Im.Checkbox($"应用全部{label}", ref flags, (ulong)allFlags);
                if (stain)
                    foreach (var slot in slots)
                    {
                        var apply = bigChange ? ((EquipFlag)flags).HasFlag(slot.ToStainFlag()) : Selection.DoApplyStain(slot);
                        if (Im.Checkbox($"应用{slot.ToName()}染色", ref apply) || bigChange)
                            _manager.ChangeApplyStains(Selection, slot, apply);
                    }
                else
                    foreach (var slot in slots)
                    {
                        var apply = bigChange ? ((EquipFlag)flags).HasFlag(slot.ToFlag()) : Selection.DoApplyEquip(slot);
                        if (Im.Checkbox($"应用{slot.ToName()}", ref apply) || bigChange)
                            _manager.ChangeApplyItem(Selection, slot, apply);
                    }
            }

            ApplyEquip("武器", ApplicationTypeExtensions.WeaponFlags, false, [EquipSlot.MainHand, EquipSlot.OffHand]);

            Im.FrameDummy();
            ApplyEquip("服装", ApplicationTypeExtensions.ArmorFlags, false, EquipSlotExtensions.EquipmentSlots);

            Im.FrameDummy();
            ApplyEquip("饰品", ApplicationTypeExtensions.AccessoryFlags, false, EquipSlotExtensions.AccessorySlots);

            Im.FrameDummy();
            ApplyEquip("染色", ApplicationTypeExtensions.StainFlags, true,
                EquipSlotExtensions.FullSlots);

            Im.FrameDummy();
            DrawParameterApplication();

            Im.FrameDummy();
            DrawBonusSlotApplication();
        }
    }

    private void DrawAllButtons()
    {
        var   enabled   = _config.DeleteDesignModifier.IsActive();
        bool? equip     = null;
        bool? customize = null;
        var   size      = ImEx.ScaledVectorX(210);
        if (ImEx.Button("禁用所有"u8, size,
                "禁用所有应用，包括任何现有的高级染色、高级外貌、队徽和湿身效果。"u8,
                !enabled))
        {
            equip     = false;
            customize = false;
        }

        if (!enabled)
            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"点击时按住 {_config.DeleteDesignModifier}。");

        Im.Line.Same();
        if (ImEx.Button("启用所有"u8, size,
                "启用所有应用，包括任何现有的高级染色、高级外貌、队徽和湿身效果。"u8,
                !enabled))
        {
            equip     = true;
            customize = true;
        }

        if (!enabled)
            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"点击时按住 {_config.DeleteDesignModifier}。");

        if (ImEx.Button("仅装备"u8, size,
                "启用与装备相关的所有应用，禁用与装备无关的所有应用。"u8,
                !enabled))
        {
            equip     = true;
            customize = false;
        }

        if (!enabled)
            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"点击时按住 {_config.DeleteDesignModifier}。");

        Im.Line.Same();
        if (ImEx.Button("仅外貌"u8, size,
                "启用与外貌相关的所有应用，禁用与外貌无关的所有应用。"u8,
                !enabled))
        {
            equip     = false;
            customize = true;
        }

        if (!enabled)
            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"点击时按住 {_config.DeleteDesignModifier}。");

        if (ImEx.Button("默认应用"u8, size,
                "将应用规则设置为默认值，就像设计是新创建的一样，没有任何高级功能或湿身效果。"u8,
                !enabled))
        {
            _manager.ChangeApplyMulti(Selection, true, true, true, false, true, true, false, true);
            _manager.ChangeApplyMeta(Selection, MetaIndex.Wetness, false);
        }

        if (!enabled)
            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"点击时按住 {_config.DeleteDesignModifier}。");

        Im.Line.Same();
        if (ImEx.Button("禁用高级"u8, size, "禁用所有高级染色和外貌，但保留其他所有设置。"u8, !enabled))
            _manager.ChangeApplyMulti(Selection, null, null, null, false, null, null, false, null);

        if (!enabled)
            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"点击时按住 {_config.DeleteDesignModifier}。");

        if (equip is null && customize is null)
            return;

        _manager.ChangeApplyMulti(Selection, equip, customize, equip, customize.HasValue && !customize.Value ? false : null, null,
            equip, equip, equip);
        if (equip.HasValue)
        {
            _manager.ChangeApplyMeta(Selection, MetaIndex.HatState,    equip.Value);
            _manager.ChangeApplyMeta(Selection, MetaIndex.VisorState,  equip.Value);
            _manager.ChangeApplyMeta(Selection, MetaIndex.WeaponState, equip.Value);
            _manager.ChangeApplyMeta(Selection, MetaIndex.EarState,    equip.Value);
        }

        if (customize.HasValue)
            _manager.ChangeApplyMeta(Selection, MetaIndex.Wetness, customize.Value);
    }

    private static readonly IReadOnlyList<StringU8> MetaLabels =
    [
        new("应用湿身"u8),
        new("应用头部装备可见性"u8),
        new("应用头部装备状态"u8),
        new("应用武器可见性"u8),
        new("应用维埃拉族耳朵可见性"u8),
    ];

    private void DrawMetaApplication()
    {
        using var   id        = Im.Id.Push("Meta"u8);
        const ulong all       = (ulong)MetaExtensions.All;
        var         flags     = (ulong)Selection.Application.Meta;
        var         bigChange = Im.Checkbox("应用全部元数据修改"u8, ref flags, all);

        foreach (var (index, label) in MetaExtensions.AllRelevant.Zip(MetaLabels))
        {
            var apply = bigChange ? ((MetaFlag)flags).HasFlag(index.ToFlag()) : Selection.DoApplyMeta(index);
            if (Im.Checkbox(label, ref apply) || bigChange)
                _manager.ChangeApplyMeta(Selection, index, apply);
        }
    }

    private static readonly IReadOnlyList<StringU8> BonusSlotLabels =
    [
        new("应用面甲"u8),
    ];

    private void DrawBonusSlotApplication()
    {
        using var id        = Im.Id.Push("Bonus"u8);
        var       flags     = Selection.Application.BonusItem;
        var       bigChange = BonusExtensions.AllFlags.Count > 1 && Im.Checkbox("应用全部额外槽位"u8, ref flags, BonusExtensions.All);
        foreach (var (index, label) in BonusExtensions.AllFlags.Zip(BonusSlotLabels))
        {
            var apply = bigChange ? flags.HasFlag(index) : Selection.DoApplyBonusItem(index);
            if (Im.Checkbox(label, ref apply) || bigChange)
                _manager.ChangeApplyBonusItem(Selection, index, apply);
        }
    }


    private void DrawParameterApplication()
    {
        using var id        = Im.Id.Push("Parameter"u8);
        var       flags     = (ulong)Selection.Application.Parameters;
        var       bigChange = Im.Checkbox("应用全部外貌参数"u8, ref flags, (ulong)CustomizeParameterExtensions.All);
        foreach (var flag in CustomizeParameterExtensions.AllFlags)
        {
            var apply = bigChange ? ((CustomizeParameterFlag)flags).HasFlag(flag) : Selection.DoApplyParameter(flag);
            if (Im.Checkbox($"应用{flag.ToNameU8()}", ref apply) || bigChange)
                _manager.ChangeApplyParameter(Selection, flag, apply);
        }
    }


    public void Draw()
    {
        _importService.CreateDatSource();
        if (_fileSystem.Selection.OrderedNodes.Count > 1)
        {
            _multiDesignPanel.Draw();
            return;
        }

        DrawPanel();

        if (_fileSystem.Selection.Selection is null || Selection.WriteProtected())
            return;

        if (_importService.CreateDatTarget(out var dat))
        {
            _manager.ChangeCustomize(Selection, CustomizeIndex.Clan,   dat.Customize[CustomizeIndex.Clan]);
            _manager.ChangeCustomize(Selection, CustomizeIndex.Gender, dat.Customize[CustomizeIndex.Gender]);
            foreach (var idx in CustomizationExtensions.AllBasic)
                _manager.ChangeCustomize(Selection, idx, dat.Customize[idx]);
            Glamourer.Messager.NotificationMessage(
                $"应用了游戏 .dat 文件 {dat.Description} 的外貌设置到 {Selection.Name}。", NotificationType.Success, false);
        }
        else if (_importService.CreateCharaTarget(out var designBase, out var name))
        {
            _manager.ApplyDesign(Selection, designBase);
            Glamourer.Messager.NotificationMessage($"应用了 Anamnesis .chara 文件 {name} 到 {Selection.Name}。",
                NotificationType.Success, false);
        }
    }

    private void DrawPanel()
    {
        using var table = Im.Table.Begin("##Panel"u8, 1, TableFlags.ScrollY, Im.ContentRegion.Available);
        if (!table || _fileSystem.Selection.Selection is null)
            return;

        table.SetupScrollFreeze(0, 1);
        table.NextColumn();

        Im.Dummy(Vector2.Zero);
        DrawButtonRow();
        table.NextColumn();

        DrawCustomize();
        DrawEquipment();
        DrawCustomizeParameters();
        DrawMaterialValues();
        _designDetails.Draw();
        DrawApplicationRules();
        _modAssociations.Draw();
        _designLinkDrawer.Draw();
        _customizePlusAssociations.Draw();
    }

    private void DrawButtonRow()
    {
        DrawApplyToSelf();
        Im.Line.Same();
        DrawApplyToTarget();
        Im.Line.Same();
        _modAssociations.DrawApplyButton();
        Im.Line.Same();
        DrawSaveToDat();
    }


    private void DrawApplyToSelf()
    {
        var (id, data) = _objects.PlayerData;
        var canApply = _designApplier.CanApplyTo(Selection.DesignData, id, data);
        var tt = canApply switch
        {
            DeniedApplicationReason.None =>
                "将当前设计按其中设置应用到你自己的角色。\n按住CTRL仅应用装备。\n按住Shift仅应用外貌。"u8,
            DeniedApplicationReason.SourceNonHuman                                             => "无法应用非人类对象的设计。"u8,
            DeniedApplicationReason.TargetInvalid or DeniedApplicationReason.TargetUnavailable => "你的角色不可用。"u8,
            _                                                                                  => ""u8,
        };

        if (ImEx.Button("应用到自己"u8, Vector2.Zero, tt, canApply is not DeniedApplicationReason.None))
            _designApplier.ApplyTo(Selection, id, data, true);
    }

    private void DrawApplyToTarget()
    {
        var (id, data) = _objects.TargetData;
        var canApply = _designApplier.CanApplyTo(Selection.DesignData, id, data);
        var tt = canApply switch
        {
            DeniedApplicationReason.None =>
                "将当前设计按其中设置应用到你的目标。\n按住CTRL仅应用装备。\n按住Shift仅应用外貌。"u8,
            DeniedApplicationReason.TargetUnavailable => "当前目标无法操作。"u8,
            DeniedApplicationReason.TargetInvalid     => "未选择有效的目标。"u8,
            DeniedApplicationReason.SourceNonHuman    => "无法应用非人类对象的设计。"u8,
            DeniedApplicationReason.TargetNonHuman    => "无法将设计应用于非人类对象。"u8,
            _                                         => ""u8,
        };
        if (ImEx.Button("应用到目标"u8, Vector2.Zero, tt, canApply is not DeniedApplicationReason.None))
            _designApplier.ApplyTo(Selection, id, data, true);
    }

    private void DrawSaveToDat()
    {
        var verified = _importService.Verify(Selection.DesignData.Customize, out _);
        var tt = verified
            ? "将当前设计的外貌数据导出为游戏角色创建时可读取的文档。"u8
            : "当前设计包含无法在创建角色时使用的外貌数据。"u8;
        var startPath = GetUserPath();
        if (startPath.Length is 0)
            startPath = null;
        if (ImEx.Button("导出为Dat"u8, Vector2.Zero, tt, !verified))
            _fileDialog.SaveFileDialog("保存文件...", ".dat", "FFXIV_CHARA_01.dat", ".dat", (v, path) =>
            {
                if (v && _fileSystem.Selection.Selection?.GetValue<Design>() is not null)
                    _importService.SaveDesignAsDat(path, Selection.DesignData.Customize, Selection.Name);
            }, startPath);

        _fileDialog.Draw();
    }

    private static unsafe string GetUserPath()
        => Framework.Instance()->UserPathString;
}
