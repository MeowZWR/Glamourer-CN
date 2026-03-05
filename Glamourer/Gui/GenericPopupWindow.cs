using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using Glamourer.Config;
using ImSharp;
using Luna;

namespace Glamourer.Gui;

public sealed class GenericPopupWindow : Window
{
    private readonly Configuration _config;
    private readonly ICondition    _condition;
    private readonly IClientState  _state;
    public           bool          OpenFestivalPopup { get; internal set; }

    public GenericPopupWindow(Configuration config, IClientState state, ICondition condition)
        : base("Glamourer Popups",
            WindowFlags.NoBringToFrontOnFocus
          | WindowFlags.NoDecoration
          | WindowFlags.NoInputs
          | WindowFlags.NoSavedSettings
          | WindowFlags.NoBackground
          | WindowFlags.NoMove
          | WindowFlags.NoNav
          | WindowFlags.NoTitleBar, true)
    {
        _config             = config;
        _state              = state;
        _condition          = condition;
        DisableWindowSounds = true;
        IsOpen              = true;
    }

    public override void Draw()
    {
        if (OpenFestivalPopup && CheckFestivalPopupConditions())
        {
            Im.Popup.Open("FestivalPopup"u8);
            OpenFestivalPopup = false;
        }

        DrawFestivalPopup();
    }

    private bool CheckFestivalPopupConditions()
        => !_state.IsPvPExcludingDen
         && !_condition[ConditionFlag.InCombat]
         && !_condition[ConditionFlag.BoundByDuty]
         && !_condition[ConditionFlag.WatchingCutscene]
         && !_condition[ConditionFlag.WatchingCutscene78]
         && !_condition[ConditionFlag.BoundByDuty95]
         && !_condition[ConditionFlag.BoundByDuty56]
         && !_condition[ConditionFlag.InDeepDungeon]
         && !_condition[ConditionFlag.PlayingLordOfVerminion]
         && !_condition[ConditionFlag.ChocoboRacing];


    private void DrawFestivalPopup()
    {
        var viewportSize = Im.Window.Viewport.Size;
        Im.Window.SetNextSize(new Vector2(Math.Max(viewportSize.X / 5, 400), Math.Max(viewportSize.Y / 7, 150)));
        Im.Window.SetNextPosition(viewportSize / 2, Condition.Always, new Vector2(0.5f));
        using var popup = Im.Popup.Begin("FestivalPopup"u8, WindowFlags.Modal);
        if (!popup)
            return;

        Im.TextWrapped(
            "Glamourer有一些节日彩蛋功能，默认情况下是开启的。你可以在“插件设置”-“行为设置”-“启动节日彩蛋”找到该选项，立即选择你当前的偏好吧。"u8);

        var buttonWidth = new Vector2(150 * Im.Style.GlobalScale, 0);
        var yPos        = Im.Window.Height - 2 * Im.Style.FrameHeight;
        var xPos        = (Im.Window.Width - Im.Style.ItemSpacing.X) / 2 - buttonWidth.X;
        Im.Cursor.Position = new Vector2(xPos, yPos);
        if (Im.Button("让我们来看看吧！"u8, buttonWidth))
        {
            _config.DisableFestivals = 0;
            _config.Save();
            Im.Popup.CloseCurrent();
        }

        Im.Line.Same();
        if (Im.Button("现在可不行。"u8, buttonWidth))
        {
            _config.DisableFestivals = 2;
            _config.Save();
            Im.Popup.CloseCurrent();
        }
    }
}
