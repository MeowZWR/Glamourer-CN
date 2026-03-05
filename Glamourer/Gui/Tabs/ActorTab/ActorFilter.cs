using Dalamud.Plugin.Services;
using Glamourer.Config;
using ImSharp;
using Luna;
using Penumbra.GameData.Enums;

namespace Glamourer.Gui.Tabs.ActorTab;

public sealed class ActorFilter : TextFilterBase<ActorCacheItem>, IUiService
{
    private readonly IPlayerState _playerState;

    private enum FilterMethod
    {
        Player,
        Owned,
        Npc,
        Retainer,
        Special,
        Homeworld,
        Text,
        Empty,
    };

    private FilterMethod _method = FilterMethod.Empty;

    public ActorFilter(IPlayerState playerState, Configuration config)
    {
        _playerState = playerState;
        FilterChanged += () =>
        {
            _method = Text switch
            {
                ""             => FilterMethod.Empty,
                "<p>" or "<P>" => FilterMethod.Player,
                "<o>" or "<O>" => FilterMethod.Owned,
                "<n>" or "<N>" => FilterMethod.Npc,
                "<r>" or "<R>" => FilterMethod.Retainer,
                "<s>" or "<S>" => FilterMethod.Special,
                "<w>" or "<W>" => FilterMethod.Homeworld,
                _              => FilterMethod.Text,
            };
            config.Filters.ActorFilter = Text;
        };
        if (config.RememberActorFilter)
            Set(config.Filters.ActorFilter);
    }

    public override bool DrawFilter(ReadOnlySpan<byte> label, Vector2 availableRegion)
    {
        var ret = base.DrawFilter(label, availableRegion);

        if (!Im.Item.Hovered())
            return ret;

        using var tt = Im.Tooltip.Begin();
        Im.Text("筛选包含输入名称的角色。"u8);
        Im.Dummy(new Vector2(0, Im.Style.TextHeight / 2));
        Im.Text("可按类型筛选："u8);
        var color = ColorId.HeaderButtons.Value();
        Im.Text("<p>"u8, color);
        Im.Line.NoSpacing();
        Im.Text(": 仅显示玩家角色。"u8);


        Im.Text("<o>"u8, color);
        Im.Line.NoSpacing();
        Im.Text(": 仅显示所属游戏对象。"u8);

        Im.Text("<n>"u8, color);
        Im.Line.NoSpacing();
        Im.Text(": 仅显示NPC。"u8);

        Im.Text("<r>"u8, color);
        Im.Line.NoSpacing();
        Im.Text(": 仅显示雇员。"u8);

        Im.Text("<s>"u8, color);
        Im.Line.NoSpacing();
        Im.Text(": 仅显示特殊屏幕角色。"u8);

        Im.Text("<w>"u8, color);
        Im.Line.NoSpacing();
        Im.Text(": 仅显示你的服务器的玩家。"u8);

        if (Text.Length > 0)
            Im.Text("\n中键点击清除筛选。"u8);
        return ret;
    }

    protected override bool OnMiddleClick()
    {
        if (!Im.Item.MiddleClicked())
            return false;

        Im.Id.ClearActive();
        return Clear();
    }

    protected override string ToFilterString(in ActorCacheItem item, int globalIndex)
        => item.DisplayText.Utf16;

    public override bool WouldBeVisible(in ActorCacheItem item, int globalIndex)
        => _method switch
        {
            FilterMethod.Player   => item.Identifier.Type is IdentifierType.Player,
            FilterMethod.Owned    => item.Identifier.Type is IdentifierType.Owned,
            FilterMethod.Npc      => item.Identifier.Type is IdentifierType.Npc,
            FilterMethod.Retainer => item.Identifier.Type is IdentifierType.Retainer,
            FilterMethod.Special  => item.Identifier.Type is IdentifierType.Special,
            FilterMethod.Homeworld => item.Identifier.Type is IdentifierType.Player
             && item.Identifier.HomeWorld == _playerState.HomeWorld.RowId,
            FilterMethod.Text => base.WouldBeVisible(item, globalIndex),
            _                 => true,
        };
}
