using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.ImGuiNotification.EventArgs;
using Dalamud.Plugin;
using Glamourer.Config;
using ImSharp;
using Luna;

namespace Glamourer.Gui;

public enum FestivalSetting
{
    Undefined,
    NeverAskNo,
    NeverAskYes,
    AskYes,
    AskNo,
}

public sealed class FestivalNotification(Configuration config, IDalamudPluginInterface pi) : INotificationAwareMessage, IUiService
{
    private bool                 _doNotAskAgain;
    private IActiveNotification? _active;

    public NotificationType NotificationType
        => NotificationType.Info;

    public string NotificationMessage
        => config.FestivalMode switch
        {
            FestivalSetting.Undefined =>
                "Glamourer 提供了一些节日限定的彩蛋功能，默认处于关闭状态。\n\n你可以随时在常规设置中开启或关闭它们，现在也可以直接选择你的偏好。",
            FestivalSetting.AskYes =>
                "新的 Glamourer 节日彩蛋已上线！\n\n要继续保持节日彩蛋功能开启吗？",
            _ => "新的 Glamourer 节日彩蛋已上线！\n\n你还是对节日彩蛋功能不感兴趣吗？",
        };

    public string NotificationTitle
        => "节日彩蛋";

    public TimeSpan NotificationDuration
        => TimeSpan.MaxValue;

    public string LogMessage
        => string.Empty;

    public SeString ChatMessage
        => SeString.Empty;

    public StringU8 StoredMessage
        => StringU8.Empty;

    public StringU8 StoredTooltip
        => StringU8.Empty;

    public void OnNotificationActions(INotificationDrawArgs args)
    {
        Im.Separator();
        Im.Cursor.Y += Im.Style.ItemInnerSpacing.Y;
        var region = Im.ContentRegion.Available;
        var width  = Im.Font.CalculateSize("不再询问"u8).X + Im.Style.ItemInnerSpacing.X + Im.Style.FrameHeight;
        Im.Cursor.X += (region.X - width) / 2;
        using (ImStyleBorder.Frame.Push(ColorParameter.Default, 1))
        {
            Im.Checkbox("不再询问"u8, ref _doNotAskAgain);
        }

        var buttonSize = new Vector2((region.X - Im.Style.ItemSpacing.X) / 2, 0);
        var (yesText, noText) = config.FestivalMode switch
        {
            FestivalSetting.Undefined => RefTuple.Create("去看看！"u8, "我没兴趣。"u8),
            FestivalSetting.AskYes    => RefTuple.Create("保留开启！"u8, "现在关闭。"u8),
            _                         => RefTuple.Create("这次试试！"u8, "依然没兴趣。"u8),
        };
        if (ImEx.Button(yesText, buttonSize, !pi.AllowSeasonalEvents && !config.DeleteDesignModifier.IsActive()))
        {
            config.FestivalMode      = _doNotAskAgain ? FestivalSetting.NeverAskYes : FestivalSetting.AskYes;
            config.LastFestivalPopup = DateOnly.FromDateTime(DateTime.Now);
            args.Notification.DismissNow();
        }

        if (!pi.AllowSeasonalEvents && Im.Item.Hovered(HoveredFlags.AllowWhenDisabled))
        {
            using var tt = Im.Tooltip.Begin();
            Im.Text(
                "你已在 Dalamud 设置中全局禁用了节日活动。\n\nGlamourer 将遵循该设置，因此除非你开启全局设置，否则此处选择的“是”将不会生效。"u8,
                Colors.SelectedRed);

            if (!config.DeleteDesignModifier.IsActive())
                Im.Text($"\n按住 {config.DeleteDesignModifier} 键强制点击。");
        }

        Im.Line.Same();
        if (Im.Button(noText, buttonSize))
        {
            config.FestivalMode      = _doNotAskAgain ? FestivalSetting.NeverAskNo : FestivalSetting.AskNo;
            config.LastFestivalPopup = DateOnly.FromDateTime(DateTime.Now);
            args.Notification.DismissNow();
        }
    }

    public void Update()
    {
        if (_active is null)
            Glamourer.Messager.AddMessage(this, false);
        else
            _active.Content = NotificationMessage;
    }

    public void OnNotificationCreated(IActiveNotification notification)
    {
        _active                             = notification;
        _active.MinimizedText               = _active.Title;
        _active.UserDismissable             = false;
        _active.RespectUiHidden             = true;
        _active.ShowIndeterminateIfNoExpiry = false;
        _active.Icon                        = INotificationIcon.From(FontAwesomeIcon.TheaterMasks);
        _active.Dismiss += args =>
        {
            if (args.Notification == _active)
                _active = null;
        };
    }
}
