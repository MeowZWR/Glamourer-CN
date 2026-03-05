using Luna.Generators;

namespace Glamourer.Config;

[TooltipEnum]
public enum HeightDisplayType
{
    [Tooltip("不显示")]
    None,

    [Tooltip("厘米 (000.0 cm)")]
    Centimetre,

    [Tooltip("米 (0.00 m)")]
    Metre,

    [Tooltip("英寸 (00.0 in)")]
    Wrong,

    [Tooltip("英尺 (0'00'')")]
    WrongFoot,

    [Tooltip("柯基 (0.0 Corgis)")]
    Corgi,

    [Tooltip("奥运会泳池 (0.000 Pools)")]
    OlympicPool,
}
