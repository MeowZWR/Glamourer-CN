namespace Glamourer.MountReplacement;

internal sealed class PapLoadTable
{
    private const uint Wildcard = 0x400;

    private readonly List<(int At, string Name)> _dirs;
    private readonly Pack[]                      _packs;
    private readonly Body[]                      _bodies;
    private readonly Redirect[]                  _redirects;

    private PapLoadTable(List<(int, string)> dirs, Pack[] packs, Body[] bodies, Redirect[] redirects)
    {
        _dirs      = dirs;
        _packs     = packs;
        _bodies    = bodies;
        _redirects = redirects;
    }

    public static PapLoadTable? Read(byte[]? bytes)
    {
        if (bytes == null || bytes.Length < 16)
            return null;

        ushort Half(int at) => BitConverter.ToUInt16(bytes, at);
        uint Word(int at) => BitConverter.ToUInt32(bytes, at);
        ulong Long(int at) => BitConverter.ToUInt64(bytes, at);
        var spelled = (int)Word(8);
        var named   = (int)Word(12);
        if (named < spelled || named > bytes.Length)
            return null;

        var dirs      = Names(bytes, spelled, named - spelled);
        var tails     = bytes.AsSpan(named);
        var packCount = Half(0);
        var packs     = new Pack[packCount];
        for (var i = 0; i < packCount; ++i)
        {
            var at = 16 + i * 12;
            packs[i] = new Pack(Word(at), (int)Word(at + 4), Name(tails, (int)Word(at + 8)) ?? "");
        }

        var after     = 16 + packs.Length * 12;
        var bodyCount = Half(2);
        var bodies    = new Body[bodyCount];
        for (var i = 0; i < bodyCount; ++i)
        {
            var at         = after + i * 168;
            var redirected = new ulong[20];
            for (var w = 0; w < 20; ++w)
                redirected[w] = Long(at + 8 + w * 8);
            bodies[i] = new Body(Word(at), Half(at + 4), Half(at + 6), redirected);
        }

        after += bodies.Length * 168;
        if (spelled < after)
            return null;

        var redirectCount = (spelled - after) / 8;
        var redirects     = new Redirect[redirectCount];
        for (var i = 0; i < redirectCount; ++i)
        {
            var at = after + i * 8;
            redirects[i] = new Redirect(Word(at), Half(at + 4), Half(at + 6));
        }

        return new PapLoadTable(dirs, packs, bodies, redirects);
    }

    public (ushort Skeleton, string Directory) Resolve(ushort skeleton, string directory, string papName)
    {
        var file = papName.EndsWith(".pap", StringComparison.Ordinal) ? papName[..^4] : papName;
        var asked = (skeleton, directory);
        var spelled = Spelled(directory);
        var body = BodyAt(skeleton, 1);
        if (spelled == null || body == null)
            return asked;

        var at = -1;
        for (var i = 0; i < _packs.Length; ++i)
        {
            if (_packs[i].Dir == spelled && _packs[i].Holds("resident/" + file))
            {
                at = i;
                break;
            }
        }

        if (at < 0)
            return asked;

        var redirectAt = body.Redirect(at);
        if (redirectAt == null || redirectAt.Value < 0 || redirectAt.Value >= _redirects.Length)
            return asked;

        var read = _redirects[redirectAt.Value];
        var resolvedSkeleton = (ushort)(read.Model & 0xFFFF);
        if (resolvedSkeleton == 0)
            resolvedSkeleton = skeleton;
        return (resolvedSkeleton, Spells(read.Dir) ?? directory);
    }

    private Body? BodyAt(ushort model, ushort set)
    {
        foreach (var body in _bodies)
        {
            if (body.Model == model && body.Set == set)
                return body;
        }

        return null;
    }

    private int? Spelled(string name)
    {
        foreach (var (at, held) in _dirs)
        {
            if (held == name)
                return at;
        }

        return null;
    }

    private string? Spells(int at)
    {
        var index = _dirs.BinarySearch((at, ""), Comparer<(int At, string Name)>.Create((a, b) => a.At.CompareTo(b.At)));
        return index >= 0 ? _dirs[index].Name : null;
    }

    private static List<(int At, string Name)> Names(byte[] bytes, int start, int length)
    {
        var found = new List<(int, string)>();
        var at    = 0;
        var block = bytes.AsSpan(start, length);
        while (Name(block, at) is { } held)
        {
            found.Add((at, held));
            at += held.Length + 1;
        }

        return found;
    }

    private static string? Name(ReadOnlySpan<byte> block, int at)
    {
        if ((uint)at >= (uint)block.Length)
            return null;

        var end = block[at..].IndexOf((byte)0);
        if (end < 0)
            return null;

        return System.Text.Encoding.UTF8.GetString(block.Slice(at, end));
    }

    private readonly record struct Pack(uint Flags, int Dir, string Name)
    {
        public bool Holds(string name)
        {
            if ((Flags & Wildcard) == 0)
                return Name == name;

            var star = Name.IndexOf('*');
            return star >= 0 && name.StartsWith(Name[..star], StringComparison.Ordinal);
        }
    }

    private readonly record struct Redirect(uint Model, ushort Set, int Dir);

    private sealed class Body(uint model, ushort set, ushort @base, ulong[] redirected)
    {
        public uint   Model { get; } = model;
        public ushort Set   { get; } = set;
        public ushort Base  { get; } = @base;
        private readonly ulong[] _redirected = redirected;

        public int? Redirect(int at)
        {
            var word = at >> 8;
            var bit  = (at >> 2) & 63;
            if ((uint)word >= (uint)_redirected.Length)
                return null;

            var held = _redirected[word];
            if ((held >> bit & 1) == 0)
                return null;

            var rank = 0;
            for (var i = 0; i < word; ++i)
                rank += System.Numerics.BitOperations.PopCount(_redirected[i]);
            rank += System.Numerics.BitOperations.PopCount(held & ((1UL << bit) - 1));
            return 4 * (Base + rank) + (at & 3);
        }
    }
}
