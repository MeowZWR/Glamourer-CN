using Luna.Generators;

namespace Glamourer.Config;

[NamedEnum(Utf16: false)]
public enum RoughnessSetting
{
    [Name("保持原样")]
    AsIs,

    [Name("始终作为粗糙度")]
    AlwaysRoughness,

    [Name("始终作为光泽强度")]
    AlwaysGloss,
}

public static partial class RoughnessSettingExtensions
{
    public static bool Get(this RoughnessSetting setting, bool roughness)
        => setting switch
        {
            RoughnessSetting.AlwaysRoughness => true,
            RoughnessSetting.AlwaysGloss     => false,
            _                                => roughness,
        };
}
