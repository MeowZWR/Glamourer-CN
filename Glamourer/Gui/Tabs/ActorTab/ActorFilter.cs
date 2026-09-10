using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Plugin.Services;
using Glamourer.Config;
using ImSharp;
using Luna;
using Penumbra.GameData.Enums;

namespace Glamourer.Gui.Tabs.ActorTab;

public sealed class ActorFilter : TextFilterBase<ActorCacheItem>, IUiService
{
    private readonly IPlayerState _playerState;
    private readonly FilterConfig _config;

    public ActorFilter(IPlayerState playerState, Configuration config)
    {
        _playerState  =  playerState;
        _config       =  config.Filters;
        FilterChanged += () => { _config.ActorFilter = Text; };
        if (config.RememberActorFilter)
            Set(_config.ActorFilter);
    }

    public override bool Clear()
    {
        if (_config.ActorTypeFilter is ActorTypeFilter.None)
            return base.Clear();

        _config.ActorTypeFilter = ActorTypeFilter.None;
        SetInternal(string.Empty);
        InvokeEvent();
        return true;
    }

    private bool DrawCombo()
    {
        using var color = ImGuiColor.Button.Push(LunaStyle.AttentionBackground, _config.ActorTypeFilter is not ActorTypeFilter.None);
        using var combo = Im.Combo.Begin("##combo"u8, StringU8.Empty,
            ComboFlags.NoPreview | ComboFlags.HeightLargest | ComboFlags.PopupAlignLeft);

        var changes = false;
        if (Im.Item.MiddleClicked())
        {
            // Ensure that a right-click clears the text filter if it is currently being edited.
            Im.Id.ClearActive();
            changes = Clear();
        }

        Im.Tooltip.OnHover("按类型筛选角色。\n中键点击清除所有筛选，包括文本筛选。"u8);

        if (!combo)
            return changes;

        var filter = _config.ActorTypeFilter ^ ActorTypeFilter.All;
        if (Im.Checkbox("全部"u8, ref filter, ActorTypeFilter.All))
        {
            _config.ActorTypeFilter = filter ^ ActorTypeFilter.All;
            changes                 = true;
        }

        Im.Dummy(ImEx.ScaledVectorY(8));
        using var style = ImStyleDouble.ItemSpacing.PushY(2 * Im.Style.GlobalScale);
        foreach (var flag in ActorTypeFilter.Values.Skip(1))
        {
            if (Im.Checkbox(flag.ToNameU8(), ref filter, flag))
            {
                _config.ActorTypeFilter = filter ^ ActorTypeFilter.All;
                changes                 = true;
            }

            Im.Tooltip.OnHover(flag.Tooltip());
        }

        if (changes)
            InvokeEvent();

        return changes;
    }

    public override bool DrawFilter(ReadOnlySpan<byte> label, Vector2 availableRegion)
    {
        var filterRegion = availableRegion with { X = availableRegion.X - Im.Style.FrameHeight };
        var ret          = base.DrawFilter(label, filterRegion);

        if (Im.Item.Hovered())
        {
            using var tt = Im.Tooltip.Begin();
            Im.Text("筛选包含输入名称的角色。"u8);
            Im.Dummy(new Vector2(0, Im.Style.TextHeight / 2));
            Im.Text("可按类型筛选："u8);
            var color = ColorId.HeaderButtons.Value;
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
        }

        Im.Line.NoSpacing();
        ret |= DrawCombo();

        return ret;
    }

    protected override string ToFilterString(in ActorCacheItem item, int globalIndex)
        => item.DisplayText.Utf16;

    public override bool WouldBeVisible(in ActorCacheItem item, int globalIndex)
    {
        if (!base.WouldBeVisible(item, globalIndex))
            return false;

        switch (item.Identifier.Type)
        {
            case IdentifierType.Player:
                if (_config.ActorTypeFilter.HasFlag(ActorTypeFilter.Player))
                    return false;
                if (item.Identifier.HomeWorld != _playerState.HomeWorld.RowId && _config.ActorTypeFilter.HasFlag(ActorTypeFilter.Homeworld))
                    return false;

                return true;
            case IdentifierType.Owned:
                if (_config.ActorTypeFilter.HasFlag(ActorTypeFilter.Owned))
                    return false;
                if (item.Identifier.HomeWorld != _playerState.HomeWorld.RowId && _config.ActorTypeFilter.HasFlag(ActorTypeFilter.Homeworld))
                    return false;

                return item.Identifier.Kind switch
                {
                    ObjectKind.Companion when _config.ActorTypeFilter.HasFlag(ActorTypeFilter.Minion)   => false,
                    ObjectKind.Mount when _config.ActorTypeFilter.HasFlag(ActorTypeFilter.Mount)        => false,
                    ObjectKind.Ornament when _config.ActorTypeFilter.HasFlag(ActorTypeFilter.Accessory) => false,
                    _                                                                                   => true,
                };
            case IdentifierType.Npc:
                return item.Identifier.Kind switch
                {
                    ObjectKind.BattleNpc when _config.ActorTypeFilter.HasFlag(ActorTypeFilter.BattleNpc) => false,
                    ObjectKind.EventNpc when _config.ActorTypeFilter.HasFlag(ActorTypeFilter.EventNpc)   => false,
                    ObjectKind.Companion when _config.ActorTypeFilter.HasFlag(ActorTypeFilter.Minion)    => false,
                    ObjectKind.Mount when _config.ActorTypeFilter.HasFlag(ActorTypeFilter.Mount)         => false,
                    ObjectKind.Ornament when _config.ActorTypeFilter.HasFlag(ActorTypeFilter.Accessory)  => false,
                    _                                                                                    => true,
                };
            case IdentifierType.Retainer when _config.ActorTypeFilter.HasFlag(ActorTypeFilter.Retainer):
            case IdentifierType.Special when _config.ActorTypeFilter.HasFlag(ActorTypeFilter.Special):
                return false;
            default: return true;
        }
    }
}
