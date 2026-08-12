using Luna.Generators;

namespace Glamourer.Config;

[NamedEnum(Utf16: false)]
[TooltipEnum]
[Flags]
public enum ActorTypeFilter : uint
{
    None = 0,

    [Name("玩家")]
    [Tooltip("显示或隐藏所有玩家角色。")]
    Player = 1 << 0,

    [Name("战斗 NPC")]
    [Tooltip("显示或隐藏所有可以参与战斗的 NPC，且不属于玩家所有。")]
    BattleNpc = 1 << 1,

    [Name("事件 NPC")]
    [Tooltip("显示或隐藏所有无法参与战斗的 NPC，且不属于玩家所有。")]
    EventNpc = 1 << 2,

    [Name("宠物")]
    [Tooltip("显示或隐藏所有宠物。")]
    Minion = 1 << 3,

    [Name("坐骑")]
    [Tooltip("显示或隐藏所有坐骑。")]
    Mount = 1 << 4,

    [Name("配饰")]
    [Tooltip("显示或隐藏所有配饰 (翅膀, 背包, 眼镜)。")]
    Accessory = 1 << 5,

    [Name("雇员")]
    [Tooltip("显示或隐藏所有雇员。")]
    Retainer = 1 << 6,

    [Name("界面角色")]
    [Tooltip("显示或隐藏所有特殊界面角色，如角色屏幕角色。")]
    Special = 1 << 7,

    [Name("玩家所属 NPC")]
    [Tooltip("显示或隐藏所有玩家所属的 NPC。")]
    Owned = 1 << 8,

    [Name("其他世界玩家")]
    [Tooltip("显示或隐藏所有不属于玩家所属世界的玩家角色。")]
    Homeworld = 1 << 9,
}

public static partial class ActorTypeFilterExtensions
{
    public const ActorTypeFilter AllFiltered = (ActorTypeFilter)((1 << 10) - 1);

    extension(ActorTypeFilter)
    {
        public static ActorTypeFilter All
            => AllFiltered;
    }
}