using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface;
using Dalamud.Plugin.Services;
using Glamourer.Automation;
using Glamourer.Config;
using Glamourer.Designs;
using Glamourer.Interop.Penumbra;
using Glamourer.State;
using ImSharp;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Interop;
using Luna;

namespace Glamourer.Gui;

[Flags]
public enum QdbButtons
{
    ApplyDesign                 = 0x01,
    RevertAll                   = 0x02,
    RevertAutomation            = 0x04,
    RevertAdvancedDyes          = 0x08,
    RevertEquip                 = 0x10,
    RevertCustomize             = 0x20,
    ReapplyAutomation           = 0x40,
    ResetSettings               = 0x80,
    RevertAdvancedCustomization = 0x100,
    ToggleMainWindow            = 0x200,
}

public sealed class DesignQuickBar : OverlayWindow, IDisposable
{
    private WindowFlags GetFlags
        => _config.Ephemeral.LockDesignQuickBar
            ? WindowFlags.NoDecoration | WindowFlags.NoDocking | WindowFlags.NoFocusOnAppearing | WindowFlags.NoMove
            : WindowFlags.NoDecoration | WindowFlags.NoDocking | WindowFlags.NoFocusOnAppearing;

    private readonly Configuration           _config;
    private readonly QuickDesignCombo        _designCombo;
    private readonly StateManager            _stateManager;
    private readonly AutoDesignApplier       _autoDesignApplier;
    private readonly ActorObjectManager      _objects;
    private readonly PenumbraService         _penumbra;
    private readonly IKeyState               _keyState;
    private readonly NavigationService       _navigation;
    private readonly Im.ColorStyleDisposable _style          = new();
    private          DateTime                _keyboardToggle = DateTime.UnixEpoch;
    private          int                     _numButtons;
    private readonly StringBuilder           _tooltipBuilder = new(512);

    public DesignQuickBar(Configuration config, QuickDesignCombo designCombo, StateManager stateManager, IKeyState keyState,
        ActorObjectManager objects, AutoDesignApplier autoDesignApplier, PenumbraService penumbra, NavigationService navigation)
        : base("Glamourer Quick Bar", WindowFlags.NoDecoration | WindowFlags.NoDocking | WindowFlags.NoFocusOnAppearing)
    {
        _config                          =  config;
        _designCombo                     =  designCombo;
        _stateManager                    =  stateManager;
        _keyState                        =  keyState;
        _objects                         =  objects;
        _autoDesignApplier               =  autoDesignApplier;
        _penumbra                        =  penumbra;
        _navigation                      =  navigation;
        IsOpen                           =  _config.Ephemeral.ShowDesignQuickBar;
        DisableWindowSounds              =  true;
        Size                             =  Vector2.Zero;
        RespectCloseHotkey               =  false;
        _navigation.ToggleQuickDesignBar += SetState;
    }

    public void Dispose()
    {
        _navigation.ToggleQuickDesignBar -= SetState;
        _style.Dispose();
    }

    public override void PreOpenCheck()
    {
        CheckHotkeys();
        IsOpen = _config.Ephemeral.ShowDesignQuickBar && _config.QdbButtons is not 0;
    }

    public override bool DrawConditions()
        => _objects.Player.Valid;

    public override void PreDraw()
    {
        Flags = GetFlags;

        _style.Push(ImStyleDouble.WindowPadding, new Vector2(Im.Style.GlobalScale * 4))
            .Push(ImStyleSingle.WindowBorderThickness, 0);
        _style.Push(ImGuiColor.WindowBackground, ColorId.QuickDesignBg.Vector)
            .Push(ImGuiColor.Button,          ColorId.QuickDesignButton.Vector)
            .Push(ImGuiColor.FrameBackground, ColorId.QuickDesignFrame.Vector);

        UpdateWidth();
    }

    public override void PostDraw()
        => _style.Dispose();

    public void DrawAtEnd(float yPos, bool mainWindow)
    {
        var numButtons = CalculateButtonCount(mainWindow);
        var width      = CalculateWidth(numButtons, mainWindow);
        Im.Cursor.Position = new Vector2(Im.ContentRegion.Maximum.X - width, yPos - Im.Style.GlobalScale);
        Draw(Im.ContentRegion.Available.X, numButtons, mainWindow);
    }

    public override void Draw()
        => Draw(Im.ContentRegion.Available.X, _numButtons, false);

    private void Draw(float width, int numButtons, bool mainWindow)
    {
        using var group      = Im.Group();
        var       spacing    = Im.Style.ItemInnerSpacing;
        using var style      = ImStyleDouble.ItemSpacing.Push(spacing);
        var       buttonSize = new Vector2(Im.Style.FrameHeight);
        PrepareButtons();
        DrawToggleMainWindowButton(buttonSize, mainWindow);
        if (_config.QdbButtons.HasFlag(QdbButtons.ApplyDesign))
        {
            var comboSize = width - numButtons * (buttonSize.X + spacing.X);
            _designCombo.Draw("##c"u8, comboSize);
            Im.Line.Same();
            DrawApplyButton(buttonSize);
        }

        DrawRevertButton(buttonSize);
        DrawRevertEquipButton(buttonSize);
        DrawRevertCustomizeButton(buttonSize);
        DrawRevertAdvancedCustomization(buttonSize);
        DrawRevertAdvancedDyes(buttonSize);
        DrawRevertAutomationButton(buttonSize);
        DrawReapplyAutomationButton(buttonSize);
        DrawResetSettingsButton(buttonSize);
    }

    private ActorIdentifier _playerIdentifier;
    private ActorData       _playerData;
    private ActorState?     _playerState;

    private ActorData       _targetData;
    private ActorIdentifier _targetIdentifier;
    private ActorState?     _targetState;

    private void PrepareButtons()
    {
        (_playerIdentifier, _playerData) = _objects.PlayerData;
        (_targetIdentifier, _targetData) = _objects.TargetData;
        _playerState                     = _stateManager.GetValueOrDefault(_playerIdentifier);
        _targetState                     = _stateManager.GetValueOrDefault(_targetIdentifier);
    }

    private void DrawApplyButton(Vector2 size)
    {
        var design    = _designCombo.QuickDesign;
        var available = 0;
        _tooltipBuilder.Clear();

        if (design is null)
        {
            _tooltipBuilder.Append("未选择任何设计。");
        }
        else
        {
            if (_playerIdentifier.IsValid && _playerData.Valid)
            {
                available |= 1;
                _tooltipBuilder.Append("左键单击：应用")
                    .Append(design.ResolveName(_config.Ephemeral.IncognitoMode))
                    .Append("到你自己。");
            }

            if (_targetIdentifier.IsValid && _targetData.Valid)
            {
                if (available is not 0)
                    _tooltipBuilder.Append('\n');
                available |= 2;
                _tooltipBuilder.Append("右键单击：应用")
                    .Append(design.ResolveName(_config.Ephemeral.IncognitoMode))
                    .Append("到").Append(_config.Ephemeral.IncognitoMode ? _targetIdentifier.Incognito(null) : _targetIdentifier.ToName());
            }

            if (available is 0)
                _tooltipBuilder.Append("玩家和目标都不可用。");
        }


        var (clicked, id, data, state) = ResolveTarget(FontAwesomeIcon.PlayCircle, size, available);
        Im.Line.Same();
        if (!clicked)
            return;

        if (state == null && !_stateManager.GetOrCreate(id, data.Objects[0], out state))
        {
            Glamourer.Messager.NotificationMessage(
                $"C无法应用{design!.ResolveName(true)}到{id.Incognito(null)}：无法创建状态。");
            return;
        }

        using var _ = design!.TemporarilyRestrictApplication(ApplicationCollection.FromKeys());
        _stateManager.ApplyDesign(state, design, ApplySettings.ManualWithLinks with { IsFinal = true });
    }

    private void DrawRevertButton(Vector2 buttonSize)
    {
        if (!_config.QdbButtons.HasFlag(QdbButtons.RevertAll))
            return;

        var available = 0;
        _tooltipBuilder.Clear();

        if (_playerIdentifier.IsValid && _playerState is { IsLocked: false })
        {
            available |= 1;
            _tooltipBuilder.Append("左键单击：将玩家角色恢复到游戏状态。");
        }

        if (_targetIdentifier.IsValid && _targetState is { IsLocked: false })
        {
            if (available is not 0)
                _tooltipBuilder.Append('\n');
            available |= 2;
            _tooltipBuilder.Append("右键单击：将")
                .Append(_targetIdentifier)
                .Append("恢复到游戏状态。");
        }

        if (available is 0)
            _tooltipBuilder.Append(
                "玩家角色和目标都不可用，被Glamourer修改了状态，或者他们的状态被锁定。");

        var (clicked, _, _, state) = ResolveTarget(LunaStyle.UndoIcon, buttonSize, available);
        Im.Line.Same();
        if (clicked)
            _stateManager.ResetState(state!, StateSource.Manual, isFinal: true);
    }

    private void DrawRevertAutomationButton(Vector2 buttonSize)
    {
        if (!_config.EnableAutoDesigns)
            return;

        if (!_config.QdbButtons.HasFlag(QdbButtons.RevertAutomation))
            return;

        var available = 0;
        _tooltipBuilder.Clear();

        if (_playerIdentifier.IsValid && _playerState is { IsLocked: false } && _playerData.Valid)
        {
            available |= 1;
            _tooltipBuilder.Append("左键单击：将玩家角色恢复到自动执行状态。");
        }

        if (_targetIdentifier.IsValid && _targetState is { IsLocked: false } && _targetData.Valid)
        {
            if (available is not 0)
                _tooltipBuilder.Append('\n');
            available |= 2;
            _tooltipBuilder.Append("右键单击：将")
                .Append(_targetIdentifier)
                .Append("恢复到自动执行状态。");
        }

        if (available is 0)
            _tooltipBuilder.Append(
                "玩家角色和目标都不可用，被Glamourer修改了状态，或者他们的状态被锁定。");

        var (clicked, id, data, state) = ResolveTarget(FontAwesomeIcon.SyncAlt, buttonSize, available);
        Im.Line.Same();
        if (!clicked)
            return;

        foreach (var actor in data.Objects)
        {
            _autoDesignApplier.ReapplyAutomation(actor, id, state!, true, false, out var forcedRedraw);
            _stateManager.ReapplyAutomationState(actor, forcedRedraw, true, StateSource.Manual);
        }
    }

    private void DrawReapplyAutomationButton(Vector2 buttonSize)
    {
        if (!_config.EnableAutoDesigns)
            return;

        if (!_config.QdbButtons.HasFlag(QdbButtons.ReapplyAutomation))
            return;

        var available = 0;
        _tooltipBuilder.Clear();

        if (_playerIdentifier.IsValid && _playerState is { IsLocked: false } && _playerData.Valid)
        {
            available |= 1;
            _tooltipBuilder.Append("左键单击：在玩家角色当前状态的基础上重新应用其当前的自动执行。");
        }

        if (_targetIdentifier.IsValid && _targetState is { IsLocked: false } && _targetData.Valid)
        {
            if (available is not 0)
                _tooltipBuilder.Append('\n');
            available |= 2;
            _tooltipBuilder.Append("右键单击：在")
                .Append(_targetIdentifier)
                .Append("当前状态的基础上重新应用其当前的自动执行。");
        }

        if (available is 0)
            _tooltipBuilder.Append(
                "玩家角色和目标均不可用，由 Glamourer 修改了状态，或者它们的状态已被锁定。");

        var (clicked, id, data, state) = ResolveTarget(FontAwesomeIcon.Repeat, buttonSize, available);
        Im.Line.Same();
        if (!clicked)
            return;

        foreach (var actor in data.Objects)
        {
            _autoDesignApplier.ReapplyAutomation(actor, id, state!, false, false, out var forcedRedraw);
            _stateManager.ReapplyAutomationState(actor, forcedRedraw, false, StateSource.Manual);
        }
    }

    private void DrawRevertAdvancedCustomization(Vector2 buttonSize)
    {
        if (!_config.QdbButtons.HasFlag(QdbButtons.RevertAdvancedCustomization))
            return;

        var available = 0;
        _tooltipBuilder.Clear();

        if (_playerIdentifier.IsValid && _playerState is { IsLocked: false } && _playerData.Valid)
        {
            available |= 1;
            _tooltipBuilder.Append("左键单击：将玩家角色的高级外貌还原为游戏状态。");
        }

        if (_targetIdentifier.IsValid && _targetState is { IsLocked: false } && _targetData.Valid)
        {
            if (available is not 0)
                _tooltipBuilder.Append('\n');
            available |= 2;
            _tooltipBuilder.Append("右键单击：将")
                .Append(_targetIdentifier)
                .Append("高级外貌还原为游戏状态。");
        }

        if (available is 0)
            _tooltipBuilder.Append("玩家角色和目标都不可用，或者他们的状态被锁定。");

        var (clicked, _, _, state) = ResolveTarget(FontAwesomeIcon.PaintBrush, buttonSize, available);
        Im.Line.Same();
        if (clicked)
            _stateManager.ResetAdvancedCustomizations(state!, StateSource.Manual);
    }

    private void DrawRevertAdvancedDyes(Vector2 buttonSize)
    {
        if (!_config.QdbButtons.HasFlag(QdbButtons.RevertAdvancedDyes))
            return;

        var available = 0;
        _tooltipBuilder.Clear();

        if (_playerIdentifier.IsValid && _playerState is { IsLocked: false } && _playerData.Valid)
        {
            available |= 1;
            _tooltipBuilder.Append("左键单击：将玩家角色的高级染色还原为游戏状态。");
        }

        if (_targetIdentifier.IsValid && _targetState is { IsLocked: false } && _targetData.Valid)
        {
            if (available is not 0)
                _tooltipBuilder.Append('\n');
            available |= 2;
            _tooltipBuilder.Append("右键单击：将")
                .Append(_targetIdentifier)
                .Append("高级染色还原为游戏状态。");
        }

        if (available is 0)
            _tooltipBuilder.Append("玩家角色和目标都不可用，或者他们的状态被锁定。");

        var (clicked, _, _, state) = ResolveTarget(FontAwesomeIcon.Palette, buttonSize, available);
        Im.Line.Same();
        if (clicked)
            _stateManager.ResetAdvancedDyes(state!, StateSource.Manual);
    }

    private void DrawRevertCustomizeButton(Vector2 buttonSize)
    {
        if (!_config.QdbButtons.HasFlag(QdbButtons.RevertCustomize))
            return;

        var available = 0;
        _tooltipBuilder.Clear();

        if (_playerIdentifier.IsValid && _playerState is { IsLocked: false } && _playerData.Valid)
        {
            available |= 1;
            _tooltipBuilder.Append("左键单击：将玩家角色的外貌设置恢复到游戏状态。");
        }

        if (_targetIdentifier.IsValid && _targetState is { IsLocked: false } && _targetData.Valid)
        {
            if (available is not 0)
                _tooltipBuilder.Append('\n');
            available |= 2;
            _tooltipBuilder.Append("右键单击：恢复")
                .Append(_targetIdentifier)
                .Append("的外貌设置恢复到游戏状态。");
        }

        if (available is 0)
            _tooltipBuilder.Append("玩家角色和目标都不可用，或者他们的状态被锁定。");

        var (clicked, _, _, state) = ResolveTarget(FontAwesomeIcon.User, buttonSize, available);
        Im.Line.Same();
        if (clicked)
            _stateManager.ResetCustomize(state!, StateSource.Manual);
    }

    private void DrawRevertEquipButton(Vector2 buttonSize)
    {
        if (!_config.QdbButtons.HasFlag(QdbButtons.RevertEquip))
            return;

        var available = 0;
        _tooltipBuilder.Clear();

        if (_playerIdentifier.IsValid && _playerState is { IsLocked: false } && _playerData.Valid)
        {
            available |= 1;
            _tooltipBuilder.Append("左键单击：将玩家的装备恢复到游戏状态。");
        }

        if (_targetIdentifier.IsValid && _targetState is { IsLocked: false } && _targetData.Valid)
        {
            if (available is not 0)
                _tooltipBuilder.Append('\n');
            available |= 2;
            _tooltipBuilder.Append("右键单击：将")
                .Append(_targetIdentifier)
                .Append("的装备恢复到游戏状态。");
        }

        if (available is 0)
            _tooltipBuilder.Append("玩家角色和目标都不可用，或者他们的状态被锁定。");

        var (clicked, _, _, state) = ResolveTarget(FontAwesomeIcon.Vest, buttonSize, available);
        Im.Line.Same();
        if (clicked)
            _stateManager.ResetEquip(state!, StateSource.Manual);
    }

    private void DrawResetSettingsButton(Vector2 buttonSize)
    {
        if (!_config.QdbButtons.HasFlag(QdbButtons.ResetSettings))
            return;

        var available = 0;
        _tooltipBuilder.Clear();

        if (_playerIdentifier.IsValid && _playerData.Valid)
        {
            available |= 1;
            _tooltipBuilder
                .Append(
                    "左键单击：重置所有由 Glamourer 应用的临时设置（手动或通过自动化）到影响 ")
                .Append(_playerIdentifier)
                .Append(" 的合集。");
        }

        if (_targetIdentifier.IsValid && _targetData.Valid)
        {
            if (available is not 0)
                _tooltipBuilder.Append('\n');
            available |= 2;
            _tooltipBuilder
                .Append(
                    "右键单击：重置所有由 Glamourer 应用的临时设置（手动或通过自动化）到影响 ")
                .Append(_targetIdentifier)
                .Append(" 的合集。");
        }

        if (available is 0)
            _tooltipBuilder.Append("玩家角色和目标都不可用，无法识别它们的合集。");

        var (clicked, _, data, _) = ResolveTarget(FontAwesomeIcon.Cog, buttonSize, available);
        Im.Line.Same();
        if (clicked)
        {
            _penumbra.RemoveAllTemporarySettings(data.Objects[0].Index, StateSource.Manual);
            _penumbra.RemoveAllTemporarySettings(data.Objects[0].Index, StateSource.Fixed);
        }
    }

    private void DrawToggleMainWindowButton(Vector2 buttonSize, bool mainWindow)
    {
        if (mainWindow || !_config.QdbButtons.HasFlag(QdbButtons.ToggleMainWindow))
            return;

        if (ImEx.Icon.Button(FontAwesomeIcon.TheaterMasks.Icon(), "切换Glamourer的主窗口。"u8, false, buttonSize))
            _navigation.SetMainWindow(null);
        Im.Line.Same();
    }

    private (bool, ActorIdentifier, ActorData, ActorState?) ResolveTarget(AwesomeIcon icon, Vector2 buttonSize, int available)
    {
        var enumerator = _tooltipBuilder.GetChunks();
        var span       = enumerator.MoveNext() ? enumerator.Current.Span : [];
        ImEx.Icon.Button(icon, span, available is 0, buttonSize);
        if ((available & 1) is 1 && Im.Item.Clicked())
            return (true, _playerIdentifier, _playerData, _playerState);
        if ((available & 2) is 2 && Im.Item.RightClicked())
            return (true, _targetIdentifier, _targetData, _targetState);

        return (false, ActorIdentifier.Invalid, ActorData.Invalid, null);
    }

    private void CheckHotkeys()
    {
        if (_keyboardToggle > DateTime.UtcNow || !CheckKeyState(_config.ToggleQuickDesignBar, false))
            return;

        _keyboardToggle                      = DateTime.UtcNow.AddMilliseconds(500);
        _config.Ephemeral.ShowDesignQuickBar = !_config.Ephemeral.ShowDesignQuickBar;
        _config.Ephemeral.Save();
    }

    private bool CheckKeyState(ModifiableHotkey key, bool noKey)
    {
        if (key.Hotkey is VirtualKey.NO_KEY)
            return noKey;

        return _keyState[key.Hotkey] && key.Modifiers.IsActive();
    }

    private int CalculateButtonCount(bool mainWindow)
    {
        var numButtons = 0;
        if (_config.QdbButtons.HasFlag(QdbButtons.RevertAll))
            ++numButtons;
        if (_config.EnableAutoDesigns)
        {
            if (_config.QdbButtons.HasFlag(QdbButtons.RevertAutomation))
                ++numButtons;
            if (_config.QdbButtons.HasFlag(QdbButtons.ReapplyAutomation))
                ++numButtons;
        }

        if (_config.QdbButtons.HasFlag(QdbButtons.RevertAdvancedCustomization))
            ++numButtons;
        if (_config.QdbButtons.HasFlag(QdbButtons.RevertAdvancedDyes))
            ++numButtons;
        if (_config.QdbButtons.HasFlag(QdbButtons.RevertCustomize))
            ++numButtons;
        if (_config.QdbButtons.HasFlag(QdbButtons.RevertEquip))
            ++numButtons;
        if (_config.UseTemporarySettings && _config.QdbButtons.HasFlag(QdbButtons.ResetSettings))
            ++numButtons;
        if (_config.QdbButtons.HasFlag(QdbButtons.ApplyDesign))
            ++numButtons;
        if (!mainWindow && _config.QdbButtons.HasFlag(QdbButtons.ToggleMainWindow))
            ++numButtons;

        return numButtons;
    }

    private float CalculateWidth(int numButtons, bool mainWindow)
    {
        var content = _config.QdbButtons.HasFlag(QdbButtons.ApplyDesign)
            ? (7 + numButtons) * Im.Style.FrameHeight + numButtons * Im.Style.ItemInnerSpacing.X
            : numButtons * Im.Style.FrameHeight + (numButtons - 1) * Im.Style.ItemInnerSpacing.X;
        var padding = mainWindow ? 0 : Im.Style.WindowPadding.X * 2;

        return content + padding;
    }

    private void UpdateWidth()
    {
        _numButtons = CalculateButtonCount(false);
        var width = CalculateWidth(_numButtons, false);
        Size = new Vector2(width, Im.Style.FrameHeight);
    }

    private void SetState(bool? open)
    {
        _config.Ephemeral.ShowDesignQuickBar = open ?? !_config.Ephemeral.ShowDesignQuickBar;
        _config.Ephemeral.Save();
    }
}
