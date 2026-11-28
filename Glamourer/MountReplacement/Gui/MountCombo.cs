using Glamourer.Gui;
using ImSharp;
using Luna;

namespace Glamourer.MountReplacement.Gui;

public sealed class MountCombo(MountReplacementManager manager, string firstLabel)
    : FilterComboBase<MountCombo.MountCacheItem>(new MountFilter(), ConfigData.Default with { DirtyCacheOnClose = true })
{
    public const string AnyLabel       = "任意坐骑";
    public const string UnsetLabel     = "请选择";
    public const string NewSourceLabel = "在此处选择坐骑";

    private uint                                        _current;
    private Func<uint, bool>?                           _hide;
    private readonly Dictionary<ImGuiId, HashSet<uint>> _openFavorites = [];

    public readonly struct MountCacheItem(uint id, string name)
    {
        public readonly uint       Id   = id;
        public readonly StringPair Name = new(name);
    }

    private sealed class MountFilter : TextFilterBase<MountCacheItem>
    {
        protected override string ToFilterString(in MountCacheItem item, int globalIndex)
            => item.Name.Utf16;
    }

    public string ToDisplayName(uint id)
        => id is 0 ? firstLabel : manager.Mounts.TryGetValue(id, out var mount) ? ToDisplayName(mount) : $"未知坐骑 (#{id})";

    private static string ToDisplayName(MountInfo mount)
        => $"{mount.Name} (#{mount.Id})";

    protected override IEnumerable<MountCacheItem> GetItems()
    {
        if (_hide?.Invoke(0) is not true)
            yield return new MountCacheItem(0, firstLabel);
        if (!_openFavorites.TryGetValue(CurrentId, out var favorites))
            _openFavorites[CurrentId] = favorites = [..manager.Favorites];
        foreach (var mount in manager.Mounts.Values
                     .OrderByDescending(m => favorites.Contains(m.Id))
                     .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (_hide?.Invoke(mount.Id) is true)
                continue;

            yield return new MountCacheItem(mount.Id, ToDisplayName(mount));
        }
    }

    protected override float ItemHeight
        => Im.Style.TextHeightWithSpacing;

    protected override bool DrawItem(in MountCacheItem item, int globalIndex, bool selected)
    {
        if (item.Id != 0 && DrawFavorite(item.Id))
            return false;

        if (item.Id != 0)
            Im.Line.Same();
        return Im.Selectable(item.Name.Utf8, selected);
    }

    private bool DrawFavorite(uint id)
    {
        var favorite = manager.IsFavorite(id);
        var hovering = Im.Mouse.IsHoveringRectangle(Rectangle.FromSize(Im.Cursor.ScreenPosition, new Vector2(Im.Style.TextHeight)));
        ImEx.Icon.DrawAligned(LunaStyle.FavoriteIcon,
            hovering ? ColorId.FavoriteStarHovered.Value : favorite ? ColorId.FavoriteStarOn.Value : ColorId.FavoriteStarOff.Value);
        if (!Im.Item.Clicked())
            return false;

        manager.ToggleFavorite(id);
        return true;
    }

    public bool Draw(Utf8StringHandler<LabelStringHandlerBuffer> label, uint current, float width, out uint newId,
        Func<uint, bool>? hide = null, string? preview = null)
    {
        _current = current;
        _hide    = hide;
        try
        {
            if (!base.Draw(label, preview ?? ToDisplayName(current), StringU8.Empty, width, out var item))
            {
                newId = current;
                return false;
            }

            newId = item.Id;
            return true;
        }
        finally
        {
            _hide = null;
        }
    }

    protected override bool HandleComboPopup(float width, ImGuiId id, ImSharp.Rectangle boundingBox, ImSharp.ComboFlags flags, out bool exit,
        out MountCacheItem ret)
    {
        if (!Im.Popup.IsOpen(id))
            _openFavorites.Remove(CurrentId);

        return base.HandleComboPopup(width, id, boundingBox, flags, out exit, out ret);
    }

    protected override bool IsSelected(MountCacheItem item, int globalIndex)
        => item.Id == _current;
}
