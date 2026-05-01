using ImSharp;
using Luna.Generators;

namespace Glamourer.GameData;

[Flags]
[NamedEnum(Utf16: false)]
public enum CustomizeParameterFlag : ushort
{
    [Name("皮肤颜色")]
    SkinDiffuse = 0x0001,

    [Name("肌肉强度")]
    MuscleTone = 0x0002,

    [Name("嘴唇颜色")]
    LipDiffuse = 0x0008,

    [Name("头发颜色")]
    HairDiffuse = 0x0010,

    [Name("头发挑染")]
    HairHighlight = 0x0040,

    [Name("左眼瞳色")]
    LeftEye = 0x0080,

    [Name("右眼瞳色")]
    RightEye = 0x0100,

    [Name("纹身颜色")]
    FeatureColor = 0x0200,

    [Name("面妆倍增器")]
    FacePaintUvMultiplier = 0x0400,

    [Name("面妆偏移")]
    FacePaintUvOffset = 0x0800,

    [Name("面妆颜色")]
    DecalColor = 0x1000,

    [Name("左瞳轮廓强度")]
    LeftLimbalIntensity = 0x2000,

    [Name("右瞳轮廓强度")]
    RightLimbalIntensity = 0x4000,
}

public static partial class CustomizeParameterExtensions
{
    // Speculars are not available anymore.
    public const CustomizeParameterFlag All = (CustomizeParameterFlag)0x7FDB;

    public const CustomizeParameterFlag RgbTriples = All
      & ~(RgbaQuadruples | Percentages | Values);

    public const CustomizeParameterFlag RgbaQuadruples = CustomizeParameterFlag.DecalColor | CustomizeParameterFlag.LipDiffuse;

    public const CustomizeParameterFlag Percentages = CustomizeParameterFlag.MuscleTone
      | CustomizeParameterFlag.LeftLimbalIntensity
      | CustomizeParameterFlag.RightLimbalIntensity;

    public const CustomizeParameterFlag Values = CustomizeParameterFlag.FacePaintUvOffset | CustomizeParameterFlag.FacePaintUvMultiplier;

    public static readonly IReadOnlyList<CustomizeParameterFlag> AllFlags =
        [.. CustomizeParameterFlag.Values.Where(f => All.HasFlag(f))];

    public static readonly IReadOnlyList<CustomizeParameterFlag> RgbaFlags       = AllFlags.Where(f => RgbaQuadruples.HasFlag(f)).ToArray();
    public static readonly IReadOnlyList<CustomizeParameterFlag> RgbFlags        = AllFlags.Where(f => RgbTriples.HasFlag(f)).ToArray();
    public static readonly IReadOnlyList<CustomizeParameterFlag> PercentageFlags = AllFlags.Where(f => Percentages.HasFlag(f)).ToArray();
    public static readonly IReadOnlyList<CustomizeParameterFlag> ValueFlags      = AllFlags.Where(f => Values.HasFlag(f)).ToArray();

    public static int Count(this CustomizeParameterFlag flag)
        => RgbaQuadruples.HasFlag(flag) ? 4 : RgbTriples.HasFlag(flag) ? 3 : 1;

    public static IEnumerable<CustomizeParameterFlag> Iterate(this CustomizeParameterFlag flags)
        => AllFlags.Where(f => flags.HasFlag(f));

    public static int ToInternalIndex(this CustomizeParameterFlag flag)
        => BitOperations.TrailingZeroCount((uint)flag);
}
