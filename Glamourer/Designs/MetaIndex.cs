using Glamourer.Api.Enums;
using Glamourer.State;
using Luna.Generators;

namespace Glamourer.Designs;

[NamedEnum]
[TooltipEnum]
public enum MetaIndex
{
    [Name("强制湿身")]
    [Tooltip("强制角色湿身或不湿身。")]
    Wetness = StateIndex.MetaWetness,

    [Name("头部装备可见")]
    [Tooltip("隐藏或显示角色的头部装备。")]
    HatState = StateIndex.MetaHatState,

    [Name("头部装备状态")]
    [Tooltip("切换角色头部装备的面罩状态。")]
    VisorState = StateIndex.MetaVisorState,

    [Name("武器可见")]
    [Tooltip("未手持时隐藏或显示角色武器。")]
    WeaponState = StateIndex.MetaWeaponState,
    ModelId = StateIndex.MetaModelId,

    [Name("耳朵可见")]
    [Tooltip("控制角色耳朵是否透过头部装备显示。（仅限维埃拉族）")]
    EarState = StateIndex.MetaEarState,
}

public static partial class MetaExtensions
{
    public static readonly IReadOnlyList<MetaIndex> AllRelevant =
        [MetaIndex.Wetness, MetaIndex.HatState, MetaIndex.VisorState, MetaIndex.WeaponState, MetaIndex.EarState];

    public const MetaFlag All = MetaFlag.Wetness | MetaFlag.HatState | MetaFlag.VisorState | MetaFlag.WeaponState | MetaFlag.EarState;

    public static MetaFlag ToFlag(this MetaIndex index)
        => index switch
        {
            MetaIndex.Wetness     => MetaFlag.Wetness,
            MetaIndex.HatState    => MetaFlag.HatState,
            MetaIndex.VisorState  => MetaFlag.VisorState,
            MetaIndex.WeaponState => MetaFlag.WeaponState,
            MetaIndex.EarState    => MetaFlag.EarState,
            _                     => (MetaFlag)byte.MaxValue,
        };

    public static MetaIndex ToIndex(this MetaFlag index)
        => index switch
        {
            MetaFlag.Wetness     => MetaIndex.Wetness,
            MetaFlag.HatState    => MetaIndex.HatState,
            MetaFlag.VisorState  => MetaIndex.VisorState,
            MetaFlag.WeaponState => MetaIndex.WeaponState,
            MetaFlag.EarState    => MetaIndex.EarState,
            _                    => (MetaIndex)byte.MaxValue,
        };

    public static IEnumerable<MetaIndex> ToIndices(this MetaFlag index)
    {
        if (index.HasFlag(MetaFlag.Wetness))
            yield return MetaIndex.Wetness;
        if (index.HasFlag(MetaFlag.HatState))
            yield return MetaIndex.HatState;
        if (index.HasFlag(MetaFlag.VisorState))
            yield return MetaIndex.VisorState;
        if (index.HasFlag(MetaFlag.WeaponState))
            yield return MetaIndex.WeaponState;
        if (index.HasFlag(MetaFlag.EarState))
            yield return MetaIndex.EarState;
    }
}
