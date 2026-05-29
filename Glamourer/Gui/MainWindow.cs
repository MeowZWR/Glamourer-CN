using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Glamourer.Config;
using Glamourer.Interop.Penumbra;
using ImSharp;
using Luna;
using Window = Luna.Window;

namespace Glamourer.Gui;

public sealed class MainWindow : Window, IDisposable
{
    private readonly Configuration     _config;
    private readonly PenumbraService   _penumbra;
    private readonly DesignQuickBar    _quickBar;
    private readonly MainTabBar        _mainTabBar;
    private readonly NavigationService _navigation;
    private          bool              _ignorePenumbra;

    public MainWindow(IDalamudPluginInterface pi, Configuration config, PenumbraService penumbra,
        MainTabBar mainTabBar, DesignQuickBar quickBar, NavigationService navigation)
        : base("###GlamourerMainWindow")
    {
        pi.UiBuilder.DisableGposeUiHide = true;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(700,  675),
            MaximumSize = new Vector2(3840, 2160),
        };
        _mainTabBar = mainTabBar;
        _quickBar   = quickBar;
        _navigation = navigation;
        _config     = config;
        _penumbra   = penumbra;
        _mainTabBar = mainTabBar;
        IsOpen      = _config.OpenWindowAtStart;

        _penumbra.DrawSettingsSection += _mainTabBar.Settings.DrawPenumbraIntegrationSettings;
        _navigation.ToggleMainWindow  += SetOpen;
    }

    public override void OnOpen()
        => _navigation.InvokeMainWindowOpened();

    public override void PreDraw()
    {
        Flags = _config.Ephemeral.LockMainWindow
            ? Flags | WindowFlags.NoMove | WindowFlags.NoResize
            : Flags & ~(WindowFlags.NoMove | WindowFlags.NoResize);
        WindowName = GetLabel();
    }

    private void SetOpen(bool? value)
        => IsOpen = value ?? !IsOpen;

    public void Dispose()
    {
        _penumbra.DrawSettingsSection -= _mainTabBar.Settings.DrawPenumbraIntegrationSettings;
        _navigation.ToggleMainWindow  -= SetOpen;
    }

    public override void Draw()
    {
        var yPos = Im.Cursor.Y;
        if (!_penumbra.Available && !_ignorePenumbra)
        {
            if (_penumbra.CurrentMajor is 0)
                DrawProblemWindow(
                    "无法附加到 Penumbra。请确保 Penumbra 已安装并正在运行。\n\nGlamourer 需要 Penumbra 才能正常工作。"u8);
            else if (_penumbra is
                     {
                         CurrentMajor: PenumbraService.RequiredPenumbraBreakingVersion,
                         CurrentMinor: >= PenumbraService.RequiredPenumbraFeatureVersion,
                     })
                DrawProblemWindow(
                    $"您当前未连接到 Penumbra，似乎是通过手动断开连接的。\n\nPenumbra 的最后 API 版本是 {_penumbra.CurrentMajor}.{_penumbra.CurrentMinor}。\n\nGlamourer 需要 Penumbra 才能正常工作。");
            else
                DrawProblemWindow(
                    $"连接到 Penumbra 失败。\n\nPenumbra 的 API 版本是 {_penumbra.CurrentMajor}.{_penumbra.CurrentMinor}，但 Glamourer 需要的版本是 {PenumbraService.RequiredPenumbraBreakingVersion}.{PenumbraService.RequiredPenumbraFeatureVersion}，其中主版本号必须完全匹配，次版本号必须大于或等于。\n您可能需要更新 Penumbra 或为这个版本的 Glamourer 启用测试构建。\n\nGlamourer 需要 Penumbra 才能正常工作。");
        }
        else
        {
            _mainTabBar.Draw();
            if (_config.ShowQuickBarInTabs)
                _quickBar.DrawAtEnd(yPos, true);
        }
    }

    /// <summary> The longest support button text. </summary>
    public static ReadOnlySpan<byte> SupportInfoButtonText
        => "复制支持信息到剪贴板"u8;

    /// <summary> Draw the support button group on the right-hand side of the window. </summary>
    public static void DrawSupportButtons(Glamourer glamourer, Changelog changelog)
    {
        var width = new Vector2(Im.Font.CalculateSize(SupportInfoButtonText).X + Im.Style.FramePadding.X * 2, 0);
        var xPos  = Im.Window.Width - width.X;
        Im.Cursor.Position = new Vector2(xPos, 0);
        SupportButton.DiscordSplit(Glamourer.Messager, width);

        Im.Cursor.Position = new Vector2(xPos, Im.Style.FrameHeightWithSpacing);
        DrawSupportButton(glamourer);

        Im.Cursor.Position = new Vector2(xPos, 2 * Im.Style.FrameHeightWithSpacing);
        SupportButton.ReniGuide(Glamourer.Messager, width.X);

        Im.Cursor.Position = new Vector2(xPos, 3 * Im.Style.FrameHeightWithSpacing);
        if (Im.Button("显示更新日志"u8, new Vector2(width.X, 0)))
            changelog.ForceOpen = true;

        Im.Cursor.Position = new Vector2(xPos, 4 * Im.Style.FrameHeightWithSpacing);
        SupportButton.KoFiPatreon(Glamourer.Messager, width);
    }

    /// <summary>
    /// Draw a button that copies the support info to clipboards.
    /// </summary>
    private static void DrawSupportButton(Glamourer glamourer)
    {
        if (!Im.Button(SupportInfoButtonText))
            return;

        var text = glamourer.GatherSupportInformation();
        Im.Clipboard.Set(text);
        Glamourer.Messager.NotificationMessage("复制支持信息到剪贴板。", NotificationType.Success, false);
    }

    private string GetLabel()
        => (Glamourer.Version.Length is 0, _config.Ephemeral.IncognitoMode) switch
        {
            (true, true)   => "Glamourer（匿名模式）###GlamourerMainWindow",
            (true, false)  => "Glamourer###GlamourerMainWindow",
            (false, false) => $"Glamourer v{Glamourer.Version}-cn###GlamourerMainWindow",
            (false, true)  => $"Glamourer v{Glamourer.Version}-cn（匿名模式）###GlamourerMainWindow",
        };

    private void DrawProblemWindow(Utf8StringHandler<TextStringHandlerBuffer> text)
    {
        using var color = ImGuiColor.Text.Push(Colors.SelectedRed);
        Im.Line.New();
        Im.Line.New();
        Im.TextWrapped(text);
        color.Pop();

        Im.Line.New();
        if (ImEx.Button("尝试重新连接"u8))
            _penumbra.Reattach();

        var ignoreAllowed = _config.DeleteDesignModifier.IsActive();
        Im.Line.Same();
        if (ImEx.Button("这次忽略 Penumbra"u8, default,
                $"某些功能，如自动化或保持状态，在没有 Penumbra 的情况下将无法正常工作。\n\n忽略此操作风险自负！{(ignoreAllowed ? string.Empty : $"\n\n按住 {_config.DeleteDesignModifier} 键并单击以启用此按钮。)")}",
                !ignoreAllowed))
            _ignorePenumbra = true;

        Im.Line.New();
        Im.Line.New();
        SupportButton.DiscordSplit(Glamourer.Messager, new Vector2(150, 0));
        Im.Line.Same();
        Im.Line.New();
        Im.Line.New();
    }
}