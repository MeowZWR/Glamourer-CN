﻿using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface;
using Dalamud.Interface.Internal.Notifications;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Glamourer.Automation;
using Glamourer.Designs;
using Glamourer.Gui.Customization;
using Glamourer.Gui.Equipment;
using Glamourer.Gui.Materials;
using Glamourer.Interop;
using Glamourer.Interop.Structs;
using Glamourer.State;
using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Raii;
using Penumbra.GameData.Actors;
using Penumbra.GameData.DataContainers;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Interop;
using ObjectManager = Glamourer.Interop.ObjectManager;

namespace Glamourer.Gui.Tabs.ActorTab;

public class ActorPanel
{
    private readonly ActorSelector            _selector;
    private readonly StateManager             _stateManager;
    private readonly CustomizationDrawer      _customizationDrawer;
    private readonly EquipmentDrawer          _equipmentDrawer;
    private readonly AutoDesignApplier        _autoDesignApplier;
    private readonly Configuration            _config;
    private readonly DesignConverter          _converter;
    private readonly ObjectManager            _objects;
    private readonly DesignManager            _designManager;
    private readonly ImportService            _importService;
    private readonly ICondition               _conditions;
    private readonly DictModelChara           _modelChara;
    private readonly CustomizeParameterDrawer _parameterDrawer;
    private readonly AdvancedDyePopup         _advancedDyes;
    private readonly HeaderDrawer.Button[]   _leftButtons;
    private readonly HeaderDrawer.Button[]   _rightButtons;

    public ActorPanel(ActorSelector selector,
        StateManager stateManager,
        CustomizationDrawer customizationDrawer,
        EquipmentDrawer equipmentDrawer,
        AutoDesignApplier autoDesignApplier,
        Configuration config,
        DesignConverter converter,
        ObjectManager objects,
        DesignManager designManager,
        ImportService importService,
        ICondition conditions,
        DictModelChara modelChara,
        CustomizeParameterDrawer parameterDrawer,
        AdvancedDyePopup advancedDyes)
    {
        _selector            = selector;
        _stateManager        = stateManager;
        _customizationDrawer = customizationDrawer;
        _equipmentDrawer     = equipmentDrawer;
        _autoDesignApplier   = autoDesignApplier;
        _config              = config;
        _converter           = converter;
        _objects             = objects;
        _designManager       = designManager;
        _importService       = importService;
        _conditions          = conditions;
        _modelChara          = modelChara;
        _parameterDrawer     = parameterDrawer;
        _advancedDyes        = advancedDyes;
        _leftButtons =
        [
            new SetFromClipboardButton(this),
            new ExportToClipboardButton(this),
            new SaveAsDesignButton(this),
        ];
        _rightButtons =
        [
            new LockedButton(this),
            new HeaderDrawer.IncognitoButton(_config.Ephemeral),
        ];
    }


    private ActorIdentifier _identifier;
    private string          _actorName = string.Empty;
    private Actor           _actor     = Actor.Null;
    private ActorData       _data;
    private ActorState?     _state;
    private bool            _lockedRedraw;

    private CustomizeFlag CustomizeApplicationFlags
        => _lockedRedraw ? CustomizeFlagExtensions.AllRelevant & ~CustomizeFlagExtensions.RedrawRequired : CustomizeFlagExtensions.AllRelevant;

    public void Draw()
    {
        using var group = ImRaii.Group();
        (_identifier, _data) = _selector.Selection;
        _lockedRedraw = _identifier.Type is IdentifierType.Special
         || _conditions[ConditionFlag.OccupiedInCutSceneEvent];
        (_actorName, _actor) = GetHeaderName();
        DrawHeader();
        DrawPanel();

        if (_state is not { IsLocked: false })
            return;

        if (_importService.CreateDatTarget(out var dat))
        {
            _stateManager.ChangeEntireCustomize(_state!, dat.Customize, CustomizeApplicationFlags, ApplySettings.Manual);
            Glamourer.Messager.NotificationMessage($"Applied games .dat file {dat.Description} customizations to {_state.Identifier}.",
                NotificationType.Success, false);
        }
        else if (_importService.CreateCharaTarget(out var designBase, out var name))
        {
            _stateManager.ApplyDesign(_state!, designBase, ApplySettings.Manual);
            Glamourer.Messager.NotificationMessage($"Applied Anamnesis .chara file {name} to {_state.Identifier}.", NotificationType.Success,
                false);
        }

        _importService.CreateDatSource();
        _importService.CreateCharaSource();
    }

    private void DrawHeader()
    {
        var textColor = !_identifier.IsValid ? ImGui.GetColorU32(ImGuiCol.Text) :
            _data.Valid                      ? ColorId.ActorAvailable.Value() : ColorId.ActorUnavailable.Value();
        HeaderDrawer.Draw(_actorName, textColor, ImGui.GetColorU32(ImGuiCol.FrameBg), _leftButtons, _rightButtons);

        SaveDesignDrawPopup();
    }

    private (string, Actor) GetHeaderName()
    {
        if (!_identifier.IsValid)
            return ("未选择", Actor.Null);

        if (_data.Valid)
            return (_selector.IncognitoMode ? _identifier.Incognito(_data.Label) : _data.Label, _data.Objects[0]);

        return (_selector.IncognitoMode ? _identifier.Incognito(null) : _identifier.ToString(), Actor.Null);
    }

    private unsafe void DrawPanel()
    {
        using var child = ImRaii.Child("##Panel", -Vector2.One, true);
        if (!child || !_selector.HasSelection || !_stateManager.GetOrCreate(_identifier, _actor, out _state))
            return;

        var transformationId = _actor.IsCharacter ? _actor.AsCharacter->CharacterData.TransformationId : 0;
        if (transformationId != 0)
            ImGuiUtil.DrawTextButton($"Currently transformed to Transformation {transformationId}.",
                -Vector2.UnitX, Colors.SelectedRed);

        DrawApplyToSelf();
        ImGui.SameLine();
        DrawApplyToTarget();

        RevertButtons();

        using var disabled = ImRaii.Disabled(transformationId != 0);
        if (_state.ModelData.IsHuman)
            DrawHumanPanel();
        else
            DrawMonsterPanel();
        _advancedDyes.Draw(_actor, _state);
    }

    private void DrawHumanPanel()
    {
        DrawCustomizationsHeader();
        DrawEquipmentHeader();
        DrawParameterHeader();
    }

    private void DrawCustomizationsHeader()
    {
        var header = _state!.ModelData.ModelId == 0
            ? "外貌"
            : $"Customization (Model Id #{_state.ModelData.ModelId})###Customization";
        using var h = ImRaii.CollapsingHeader(header);
        if (!h)
            return;

        if (_customizationDrawer.Draw(_state!.ModelData.Customize, _state.IsLocked, _lockedRedraw))
            _stateManager.ChangeEntireCustomize(_state, _customizationDrawer.Customize, _customizationDrawer.Changed, ApplySettings.Manual);

        EquipmentDrawer.DrawMetaToggle(ToggleDrawData.FromState(MetaIndex.Wetness, _stateManager, _state));
        ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight() / 2));
    }

    private void DrawEquipmentHeader()
    {
        using var h = ImRaii.CollapsingHeader("装备");
        if (!h)
            return;

        _equipmentDrawer.Prepare();

        var usedAllStain = _equipmentDrawer.DrawAllStain(out var newAllStain, _state!.IsLocked);
        foreach (var slot in EquipSlotExtensions.EqdpSlots)
        {
            var data = EquipDrawData.FromState(_stateManager, _state!, slot);
            _equipmentDrawer.DrawEquip(data);
            if (usedAllStain)
                _stateManager.ChangeStain(_state, slot, newAllStain, ApplySettings.Manual);
        }

        var mainhand = EquipDrawData.FromState(_stateManager, _state, EquipSlot.MainHand);
        var offhand  = EquipDrawData.FromState(_stateManager, _state, EquipSlot.OffHand);
        _equipmentDrawer.DrawWeapons(mainhand, offhand, GameMain.IsInGPose());

        ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight() / 2));
        DrawEquipmentMetaToggles();
        ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight() / 2));
    }

    private void DrawParameterHeader()
    {
        if (!_config.UseAdvancedParameters)
            return;

        using var h = ImRaii.CollapsingHeader("外貌（高级）- 调色盘");
        if (!h)
            return;

        _parameterDrawer.Draw(_stateManager, _state!);
    }

    private void DrawEquipmentMetaToggles()
    {
        using (_ = ImRaii.Group())
        {
            EquipmentDrawer.DrawMetaToggle(ToggleDrawData.FromState(MetaIndex.HatState, _stateManager, _state!));
            EquipmentDrawer.DrawMetaToggle(ToggleDrawData.CrestFromState(CrestFlag.Head, _stateManager, _state!));
        }

        ImGui.SameLine();
        using (_ = ImRaii.Group())
        {
            EquipmentDrawer.DrawMetaToggle(ToggleDrawData.FromState(MetaIndex.VisorState, _stateManager, _state!));
            EquipmentDrawer.DrawMetaToggle(ToggleDrawData.CrestFromState(CrestFlag.Body, _stateManager, _state!));
        }

        ImGui.SameLine();
        using (_ = ImRaii.Group())
        {
            EquipmentDrawer.DrawMetaToggle(ToggleDrawData.FromState(MetaIndex.WeaponState, _stateManager, _state!));
            EquipmentDrawer.DrawMetaToggle(ToggleDrawData.CrestFromState(CrestFlag.OffHand, _stateManager, _state!));
        }
    }

    private void DrawMonsterPanel()
    {
        var names     = _modelChara[_state!.ModelData.ModelId];
        var turnHuman = ImGui.Button("变为人类");
        ImGui.Separator();
        using (_ = ImRaii.ListBox("##MonsterList",
                   new Vector2(ImGui.GetContentRegionAvail().X, 10 * ImGui.GetTextLineHeightWithSpacing())))
        {
            if (names.Count == 0)
                ImGui.TextUnformatted("未知怪物");
            else
                ImGuiClip.ClippedDraw(names, p => ImGui.TextUnformatted($"{p.Name} ({p.Kind.ToName()} #{p.Id})"),
                    ImGui.GetTextLineHeightWithSpacing());
        }

        ImGui.Separator();
        ImGui.TextUnformatted("外貌数据");
        using (_ = ImRaii.PushFont(UiBuilder.MonoFont))
        {
            foreach (var b in _state.ModelData.Customize)
            {
                using (_ = ImRaii.Group())
                {
                    ImGui.TextUnformatted($" {b.Value:X2}");
                    ImGui.TextUnformatted($"{b.Value,3}");
                }

                ImGui.SameLine();
                if (ImGui.GetContentRegionAvail().X < ImGui.GetStyle().ItemSpacing.X + ImGui.CalcTextSize("XXX").X)
                    ImGui.NewLine();
            }

            if (ImGui.GetCursorPosX() != 0)
                ImGui.NewLine();
        }

        ImGui.Separator();
        ImGui.TextUnformatted("装备数据");
        using (_ = ImRaii.PushFont(UiBuilder.MonoFont))
        {
            foreach (var b in _state.ModelData.GetEquipmentBytes())
            {
                using (_ = ImRaii.Group())
                {
                    ImGui.TextUnformatted($" {b:X2}");
                    ImGui.TextUnformatted($"{b,3}");
                }

                ImGui.SameLine();
                if (ImGui.GetContentRegionAvail().X < ImGui.GetStyle().ItemSpacing.X + ImGui.CalcTextSize("XXX").X)
                    ImGui.NewLine();
            }

            if (ImGui.GetCursorPosX() != 0)
                ImGui.NewLine();
        }

        if (turnHuman)
            _stateManager.TurnHuman(_state, StateSource.Manual);
    }

    private string      _newName = string.Empty;
    private DesignBase? _newDesign;

    private void SaveDesignDrawPopup()
    {
        if (!ImGuiUtil.OpenNameField("保存为设计", ref _newName))
            return;

        if (_newDesign != null && _newName.Length > 0)
            _designManager.CreateClone(_newDesign, _newName, true);
        _newDesign = null;
        _newName   = string.Empty;
    }

    private void RevertButtons()
    {
        if (ImGuiUtil.DrawDisabledButton("恢复游戏状态", Vector2.Zero, "恢复角色到游戏中的真实状态。",
                _state!.IsLocked))
            _stateManager.ResetState(_state!, StateSource.Manual);

        ImGui.SameLine();

        if (ImGuiUtil.DrawDisabledButton("重新应用自动执行", Vector2.Zero,
                "在角色的当前状态基础上重新应用角色的当前自动执行状态。",
                !_config.EnableAutoDesigns || _state!.IsLocked))
        {
            _autoDesignApplier.ReapplyAutomation(_actor, _identifier, _state!, false);
            _stateManager.ReapplyState(_actor, StateSource.Manual);
        }

        ImGui.SameLine();
        if (ImGuiUtil.DrawDisabledButton("恢复到自动执行", Vector2.Zero,
                "尝试将角色恢复到自动执行中设计的状态。",
                !_config.EnableAutoDesigns || _state!.IsLocked))
        {
            _autoDesignApplier.ReapplyAutomation(_actor, _identifier, _state!, true);
            _stateManager.ReapplyState(_actor, StateSource.Manual);
        }

        ImGui.SameLine();
        if (ImGuiUtil.DrawDisabledButton("重新应用", Vector2.Zero,
                "如果感觉有什么出现了问题，那就尝试重新应用已配置的状态。一般情况下应该不需要这样做。",
                _state!.IsLocked))
            _stateManager.ReapplyState(_actor, StateSource.Manual);
    }

    private void DrawApplyToSelf()
    {
        var (id, data) = _objects.PlayerData;
        if (!ImGuiUtil.DrawDisabledButton("应用到自己", Vector2.Zero,
                "应用当前状态到你自己的角色。\n按住CTRL仅应用装备。\n按住Shift仅应用外貌。",
                !data.Valid || id == _identifier || _state!.ModelData.ModelId != 0))
            return;

        if (_stateManager.GetOrCreate(id, data.Objects[0], out var state))
            _stateManager.ApplyDesign(state, _converter.Convert(_state!, ApplicationRules.FromModifiers(_state!)),
                ApplySettings.Manual);
    }

    private void DrawApplyToTarget()
    {
        var (id, data) = _objects.TargetData;
        var tt = id.IsValid
            ? data.Valid
                ? "应用当前状态到你选中的目标。"
                : "无法应用到当前目标。"
            : "没有选中有效的目标。";
        if (!ImGuiUtil.DrawDisabledButton("应用到目标", Vector2.Zero, tt,
                !data.Valid || id == _identifier || _state!.ModelData.ModelId != 0))
            return;

        if (_stateManager.GetOrCreate(id, data.Objects[0], out var state))
            _stateManager.ApplyDesign(state, _converter.Convert(_state!, ApplicationRules.FromModifiers(_state!)),
                ApplySettings.Manual);
    }


    private sealed class SetFromClipboardButton(ActorPanel panel)
        : HeaderDrawer.Button
    {
        protected override string Description
            => "尝试应用剪贴板中的设计。\n按住CTRL仅应用装备。\n按住Shift仅应用外貌。";

        protected override FontAwesomeIcon Icon
            => FontAwesomeIcon.Clipboard;

        public override bool Visible
            => panel._state != null;

        protected override bool Disabled
            => panel._state?.IsLocked ?? true;

        protected override void OnClick()
        {
            try
            {
                var (applyGear, applyCustomize) = UiHelpers.ConvertKeysToBool();
                var text = ImGui.GetClipboardText();
                var design = panel._converter.FromBase64(text, applyCustomize, applyGear, out _)
                 ?? throw new Exception("剪贴板不包含有效数据。");
                panel._stateManager.ApplyDesign(panel._state!, design, ApplySettings.ManualWithLinks);
            }
            catch (Exception ex)
            {
                Glamourer.Messager.NotificationMessage(ex, $"无法将剪贴板应用于 {panel._identifier}.",
                    $"无法将剪贴板应用于设计 {panel._identifier.Incognito(null)}", NotificationType.Error, false);
            }
        }
    }

    private sealed class ExportToClipboardButton(ActorPanel panel) : HeaderDrawer.Button
    {
        protected override string Description
            => "复制当前设计到剪贴板。\n按住CTRL不复制外貌。\n按住Shift不复制装备。";

        protected override FontAwesomeIcon Icon
            => FontAwesomeIcon.Copy;

        public override bool Visible
            => panel._state?.ModelData.ModelId == 0;

        protected override void OnClick()
        {
            try
            {
                var text = panel._converter.ShareBase64(panel._state!, ApplicationRules.FromModifiers(panel._state!));
                ImGui.SetClipboardText(text);
            }
            catch (Exception ex)
            {
                Glamourer.Messager.NotificationMessage(ex, $"无法复制 {panel._identifier} 的数据到剪贴板。",
                    $"无法复制设计 {panel._identifier.Incognito(null)} 的数据到剪贴板。", NotificationType.Error);
            }
        }
    }

    private sealed class SaveAsDesignButton(ActorPanel panel) : HeaderDrawer.Button
    {
        protected override string Description
            => "将当前状态保存到“角色设计”。\n按住CTRL不保存外貌。\n按住Shift不保存装备。";

        protected override FontAwesomeIcon Icon
            => FontAwesomeIcon.Save;

        public override bool Visible
            => panel._state?.ModelData.ModelId == 0;

        protected override void OnClick()
        {
            ImGui.OpenPopup("保存为设计");
            panel._newName   = panel._state!.Identifier.ToName();
            panel._newDesign = panel._converter.Convert(panel._state, ApplicationRules.FromModifiers(panel._state));
        }
    }

    private sealed class LockedButton(ActorPanel panel) : HeaderDrawer.Button
    {
        protected override string Description
            => "此角色的当前状态已被外部工具锁定。";

        protected override FontAwesomeIcon Icon
            => FontAwesomeIcon.Lock;

        public override bool Visible
            => panel._state?.IsLocked ?? false;

        protected override bool Disabled
            => true;

        protected override uint BorderColor
            => ColorId.ActorUnavailable.Value();

        protected override uint TextColor
            => ColorId.ActorUnavailable.Value();

        protected override void OnClick()
        { }
    }
}
