using System.Collections.Frozen;
using Luna;

namespace Glamourer.Designs;

public readonly struct CreationDate : ISortMode
{
    public static readonly CreationDate Instance = new();

    public ReadOnlySpan<byte> Name
        => "创建日期 (旧优先)"u8;

    public ReadOnlySpan<byte> Description
        => "在每个折叠组中，按字典顺序排序所有子组，随后按创建日期排序所有叶子节点（文件）。"u8;

    public IEnumerable<IFileSystemNode> GetChildren(IFileSystemFolder f)
        => ISortMode.GetFolderLike(f).Concat(ISortMode.GetLeaveLike(f).OrderBy(l => l switch
        {
            IFileSystemData<Design> d => d.Value.CreationDate.ToUnixTimeMilliseconds(),
            IFileSystemSeparator s => s.CreationDate,
            _ => 0L,
        }));
}

public readonly struct UpdateDate : ISortMode
{
    public static readonly UpdateDate Instance = new();

    public ReadOnlySpan<byte> Name
        => "更新日期 (旧优先)"u8;

    public ReadOnlySpan<byte> Description
        => "在每个折叠组中，按字典顺序排序所有子组，随后按更新日期排序所有叶子节点（文件）。"u8;

    public IEnumerable<IFileSystemNode> GetChildren(IFileSystemFolder f)
        => ISortMode.GetFolderLike(f).Concat(ISortMode.GetLeaveLike(f).OrderBy(l => l switch
        {
            IFileSystemData<Design> d => d.Value.LastEdit.ToUnixTimeMilliseconds(),
            IFileSystemSeparator s    => s.CreationDate,
            _                         => 0L,
        }));
}

public readonly struct InverseCreationDate : ISortMode
{
    public static readonly InverseCreationDate Instance = new();

    public ReadOnlySpan<byte> Name
        => "创建日期 (新优先)"u8;

    public ReadOnlySpan<byte> Description
        => "在每个折叠组中，按字典顺序排序所有子组，随后按创建日期倒序排序所有叶子节点（文件）。"u8;

    public IEnumerable<IFileSystemNode> GetChildren(IFileSystemFolder f)
        => ISortMode.GetFolderLike(f).Concat(ISortMode.GetLeaveLike(f).OrderByDescending(l => l switch
        {
            IFileSystemData<Design> d => d.Value.CreationDate.ToUnixTimeMilliseconds(),
            IFileSystemSeparator s    => s.CreationDate,
            _                         => 0L,
        }));
}

public readonly struct InverseUpdateDate : ISortMode
{
    public static readonly InverseUpdateDate Instance = new();

    public ReadOnlySpan<byte> Name
        => "更新日期 (新优先)"u8;

    public ReadOnlySpan<byte> Description
        => "在每个折叠组中，按字典顺序排序所有子组，随后按更新日期倒序排序所有叶子节点（文件）。"u8;

    public IEnumerable<IFileSystemNode> GetChildren(IFileSystemFolder f)
        => ISortMode.GetFolderLike(f).Concat(ISortMode.GetLeaveLike(f).OrderByDescending(l => l switch
        {
            IFileSystemData<Design> d => d.Value.LastEdit.ToUnixTimeMilliseconds(),
            IFileSystemSeparator s    => s.CreationDate,
            _                         => 0L,
        }));
}

public static class SortModeExtensions
{
    private static readonly FrozenDictionary<string, ISortMode> ValidSortModes = new Dictionary<string, ISortMode>
    {
        [nameof(ISortMode.FoldersFirst)]           = ISortMode.FoldersFirst,
        [nameof(ISortMode.Lexicographical)]        = ISortMode.Lexicographical,
        [nameof(CreationDate)]                     = ISortMode.CreationDate,
        [nameof(InverseCreationDate)]              = ISortMode.InverseCreationDate,
        [nameof(UpdateDate)]                       = ISortMode.UpdateDate,
        [nameof(InverseUpdateDate)]                = ISortMode.InverseUpdateDate,
        [nameof(ISortMode.InverseFoldersFirst)]    = ISortMode.InverseFoldersFirst,
        [nameof(ISortMode.InverseLexicographical)] = ISortMode.InverseLexicographical,
        [nameof(ISortMode.FoldersLast)]            = ISortMode.FoldersLast,
        [nameof(ISortMode.InverseFoldersLast)]     = ISortMode.InverseFoldersLast,
        [nameof(ISortMode.InternalOrder)]          = ISortMode.InternalOrder,
        [nameof(ISortMode.InverseInternalOrder)]   = ISortMode.InverseInternalOrder,
    }.ToFrozenDictionary();

    extension(ISortMode)
    {
        public static ISortMode CreationDate
            => CreationDate.Instance;

        public static ISortMode InverseCreationDate
            => InverseCreationDate.Instance;

        public static ISortMode UpdateDate
            => UpdateDate.Instance;

        public static ISortMode InverseUpdateDate
            => InverseUpdateDate.Instance;

        public static IReadOnlyDictionary<string, ISortMode> Valid
            => ValidSortModes;
    }
}
