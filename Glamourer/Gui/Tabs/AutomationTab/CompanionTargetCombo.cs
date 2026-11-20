using Dalamud.Game.ClientState.Objects.Enums;
using Glamourer.Automation;
using ImSharp;
using Luna;

namespace Glamourer.Gui.Tabs.AutomationTab;

public sealed class CompanionTargetCombo(CompanionAutoManager manager)
    : FilterComboBase<CompanionTargetCombo.TargetCacheItem>(new TargetFilter()), IUiService
{
    public const string UnsetLabel   = "请选择";
    public const string AllPetsLabel = "所有宠物（不包含召唤物）";

    private CompanionLimit _current;

    public readonly struct TargetCacheItem(CompanionLimit limit, string name)
    {
        public readonly CompanionLimit Limit = limit;
        public readonly StringPair     Name  = new(name);
    }

    public static string ToDisplayName(CompanionTarget target)
        => target.Kind is ObjectKind.Companion ? $"{target.Name} (宠物)" : $"{target.Name} (召唤物)";

    private sealed class TargetFilter : TextFilterBase<TargetCacheItem>
    {
        protected override string ToFilterString(in TargetCacheItem item, int globalIndex)
            => item.Name.Utf16;
    }

    protected override IEnumerable<TargetCacheItem> GetItems()
    {
        yield return new TargetCacheItem(CompanionLimit.Unset, UnsetLabel);
        yield return new TargetCacheItem(CompanionLimit.AllPets, AllPetsLabel);

        foreach (var target in manager.GetAvailableTargets()
                     .OrderBy(t => t.Kind is not ObjectKind.Companion)
                     .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase))
            yield return new TargetCacheItem(new CompanionLimit(CompanionLimitKind.Target, target), ToDisplayName(target));
    }

    protected override float ItemHeight
        => Im.Style.TextHeightWithSpacing;

    protected override bool DrawItem(in TargetCacheItem item, int globalIndex, bool selected)
    {
        var ret = Im.Selectable(item.Name.Utf8, selected);
        if (item.Limit.Target?.Kind is ObjectKind.BattleNpc && Im.Item.Hovered())
        {
            using var style = Im.Style.PushDefault();
            using var tt    = Im.Tooltip.Begin();
            Im.Text("替换召唤物可能有较大隐患，风险自负。"u8);
        }

        return ret;
    }

    public bool Draw(Utf8StringHandler<LabelStringHandlerBuffer> label, CompanionLimit current, float width,
        out CompanionLimit newLimit)
    {
        _current = current;
        var preview = current.Kind switch
        {
            CompanionLimitKind.AllPets => AllPetsLabel,
            CompanionLimitKind.Target  => ToDisplayName(current.Target!),
            _                          => UnsetLabel,
        };
        var tooltip = current.Target?.Kind is ObjectKind.BattleNpc ? "替换召唤物可能有较大隐患，风险自负。"u8 : StringU8.Empty;
        if (!base.Draw(label, preview, tooltip, width, out var item))
        {
            newLimit = CompanionLimit.Unset;
            return false;
        }

        newLimit = item.Limit;
        return true;
    }

    protected override bool IsSelected(TargetCacheItem item, int globalIndex)
        => item.Limit.SameAs(_current);
}
