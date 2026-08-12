using Glamourer.Config;
using ImSharp;
using Luna;

namespace Glamourer.Gui.Tabs.DesignTab;

public sealed class DesignFilter : TokenizedFilter<DesignFilterTokenType, DesignFileSystemCache.DesignData, DesignFilterToken>,
    IFileSystemFilter<DesignFileSystemCache.DesignData>, IUiService
{
    public DesignFilter(Configuration config)
    {
        if (config.RememberDesignFilter)
            Set(config.Filters.DesignFilter);
        FilterChanged += () => config.Filters.DesignFilter = Text;
    }

    protected override void DrawTooltip()
    {
        if (!Im.Item.Hovered())
            return;

        using var tt             = Im.Tooltip.Begin();
        var       highlightColor = ColorId.EnabledAutoSet.Vector;
        Im.Text("根据路径或名称中的关键词进行筛选，多个关键词请用空格分隔。"u8);
        ImEx.TextMultiColored("输入 "u8).Then("m:[关键词]"u8, highlightColor)
            .Then(" 筛选包含指定模组关联的设计。"u8).End();
        ImEx.TextMultiColored("输入 "u8).Then("t:[string]"u8, highlightColor).Then(" 以根据特定标签 (Tag) 进行筛选。"u8).End();
        ImEx.TextMultiColored("输入 "u8).Then("c:[string]"u8, highlightColor)
            .Then(" 以根据特定的颜色设置进行筛选。"u8).End();
        ImEx.TextMultiColored("输入 "u8).Then("i:[string]"u8, highlightColor).Then(" 以筛选包含特定物品的设计。"u8)
            .End();
        ImEx.TextMultiColored("输入 "u8).Then("n:[string]"u8, highlightColor).Then(" 仅根据设计名称筛选，忽略路径。"u8)
            .End();
        ImEx.TextMultiColored("输入 "u8).Then("f:[string]"u8, highlightColor).Then(
                " 在名称、路径、描述、标签、关联模组、颜色或包含物品中全局筛选文本。"u8)
            .End();
        Im.Line.New();
        ImEx.TextMultiColored("使用 "u8).Then("None"u8, highlightColor).Then(" 作为占位符，仅筛选空列表或空名称。"u8)
            .End();
        Im.Text("通常情况下，设计必须同时满足所有提供的筛选条件。"u8);
        ImEx.TextMultiColored("在筛选关键词前加上"u8).Then("'-'"u8, highlightColor)
            .Then("以筛选不符合该条件的条目（反向筛选）。"u8).End();
        ImEx.TextMultiColored("在多个关键词前加上 "u8).Then("'?'"u8, highlightColor)
            .Then(" 以筛选符合其中至少一个条件的条目（逻辑或筛选）。"u8).End();
        ImEx.TextMultiColored("对于带空格的词组，请使用 "u8).Then("\"[带有空格的关键词]\""u8, highlightColor)
            .Then(" 进行完整匹配。"u8).End();
    }

    protected override bool Matches(in DesignFilterToken token, in DesignFileSystemCache.DesignData cacheItem)
        => token.Type switch
        {
            DesignFilterTokenType.Default => cacheItem.Node.FullPath.Contains(token.Needle, StringComparison.OrdinalIgnoreCase)
             || cacheItem.Node.Value.Name.Contains(token.Needle, StringComparison.OrdinalIgnoreCase),
            DesignFilterTokenType.Mod         => CheckMods(token.Needle, cacheItem),
            DesignFilterTokenType.Tag         => CheckTags(token.Needle, cacheItem),
            DesignFilterTokenType.Color       => cacheItem.Node.Value.Color.Contains(token.Needle, StringComparison.OrdinalIgnoreCase),
            DesignFilterTokenType.Item        => cacheItem.Node.Value.DesignData.ContainsName(token.Needle),
            DesignFilterTokenType.Name        => cacheItem.Node.Value.Name.Contains(token.Needle, StringComparison.OrdinalIgnoreCase),
            DesignFilterTokenType.FullContext => CheckFullContext(token.Needle, cacheItem),
            _                                 => true,
        };

    protected override bool MatchesNone(DesignFilterTokenType type, bool negated, in DesignFileSystemCache.DesignData cacheItem)
        => type switch
        {
            DesignFilterTokenType.Mod when negated => cacheItem.Node.Value.AssociatedMods.Count > 0,
            DesignFilterTokenType.Mod              => cacheItem.Node.Value.AssociatedMods.Count is 0,
            DesignFilterTokenType.Tag when negated => cacheItem.Node.Value.Tags.Length > 0,
            DesignFilterTokenType.Tag              => cacheItem.Node.Value.Tags.Length is 0,
            _                                      => true,
        };

    private static bool CheckMods(string needle, in DesignFileSystemCache.DesignData cacheItem)
        => cacheItem.Node.Value.AssociatedMods.Any(kvp => kvp.Key.Name.Contains(needle, StringComparison.OrdinalIgnoreCase));

    private static bool CheckTags(string needle, in DesignFileSystemCache.DesignData cacheItem)
        => cacheItem.Node.Value.Tags.Any(t => t.Contains(needle, StringComparison.OrdinalIgnoreCase));

    private static bool CheckFullContext(string needle, in DesignFileSystemCache.DesignData cacheItem)
    {
        if (needle.Length is 0)
            return true;

        if (cacheItem.Node.FullPath.Contains(needle, StringComparison.OrdinalIgnoreCase))
            return true;

        var design = cacheItem.Node.Value;
        if (design.Name.Contains(needle, StringComparison.OrdinalIgnoreCase))
            return true;

        if (design.Description.Contains(needle, StringComparison.OrdinalIgnoreCase))
            return true;

        if (CheckTags(needle, cacheItem))
            return true;

        if (design.Color.Contains(needle, StringComparison.OrdinalIgnoreCase))
            return true;

        if (CheckMods(needle, cacheItem))
            return true;

        if (design.DesignData.ContainsName(needle))
            return true;

        if (design.Identifier.ToString().Contains(needle, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    public bool WouldBeVisible(in FileSystemFolderCache folder)
    {
        switch (State)
        {
            case FilterState.NoFilters: return true;
            case FilterState.NoMatches: return false;
        }

        foreach (var token in Forced)
        {
            if (token.Type switch
                {
                    DesignFilterTokenType.Name        => !folder.Name.Contains(token.Needle, StringComparison.OrdinalIgnoreCase),
                    DesignFilterTokenType.Default     => !folder.FullPath.Contains(token.Needle, StringComparison.OrdinalIgnoreCase),
                    DesignFilterTokenType.FullContext => !folder.FullPath.Contains(token.Needle, StringComparison.OrdinalIgnoreCase),
                    _                                 => true,
                })
                return false;
        }

        foreach (var token in Negated)
        {
            if (token.Type switch
                {
                    DesignFilterTokenType.Name        => folder.Name.Contains(token.Needle, StringComparison.OrdinalIgnoreCase),
                    DesignFilterTokenType.Default     => folder.FullPath.Contains(token.Needle, StringComparison.OrdinalIgnoreCase),
                    DesignFilterTokenType.FullContext => folder.FullPath.Contains(token.Needle, StringComparison.OrdinalIgnoreCase),
                    _                                 => false,
                })
                return false;
        }

        foreach (var token in General)
        {
            if (token.Type switch
                {
                    DesignFilterTokenType.Name        => folder.Name.Contains(token.Needle, StringComparison.OrdinalIgnoreCase),
                    DesignFilterTokenType.Default     => folder.FullPath.Contains(token.Needle, StringComparison.OrdinalIgnoreCase),
                    DesignFilterTokenType.FullContext => !folder.FullPath.Contains(token.Needle, StringComparison.OrdinalIgnoreCase),
                    _                                 => false,
                })
                return true;
        }

        return General.Count is 0;
    }
}
