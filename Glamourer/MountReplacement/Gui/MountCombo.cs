using ImSharp;
using Luna;

namespace Glamourer.MountReplacement.Gui;

public sealed class MountCombo(MountReplacementManager manager, string firstLabel)
    : FilterComboBase<MountCombo.MountCacheItem>(new MountFilter())
{
    public const string AnyLabel   = "任意坐骑";
    public const string UnsetLabel = "请选择";

    private uint _current;

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
        yield return new MountCacheItem(0, firstLabel);
        foreach (var mount in manager.Mounts.Values.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase))
            yield return new MountCacheItem(mount.Id, ToDisplayName(mount));
    }

    protected override float ItemHeight
        => Im.Style.TextHeightWithSpacing;

    protected override bool DrawItem(in MountCacheItem item, int globalIndex, bool selected)
        => Im.Selectable(item.Name.Utf8, selected);

    public bool Draw(Utf8StringHandler<LabelStringHandlerBuffer> label, uint current, float width, out uint newId)
    {
        _current = current;
        if (!base.Draw(label, ToDisplayName(current), StringU8.Empty, width, out var item))
        {
            newId = current;
            return false;
        }

        newId = item.Id;
        return true;
    }

    protected override bool IsSelected(MountCacheItem item, int globalIndex)
        => item.Id == _current;
}
