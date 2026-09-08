using ImSharp;
using Luna;
using static Glamourer.Gui.ColorId;

namespace Glamourer.Gui;

public readonly struct ColorIdData : IColorData<ColorId>
{
    private static readonly ColorData<ColorId>[] ColorData = CreateData();

    public static ColorData<ColorId> Data(in ColorId id)
    {
        if ((int)id < 0 || (int)id >= ColorData.Length)
            return ColorData<ColorId>.Invalid;

        return ColorData[(int)id];
    }

    public static StringU8 Parent { get; } = new("Glamourer"u8);

    private static ColorData<ColorId>[] CreateData()
    {
        var designs    = "设计选择器"u8;
        var metadata   = "元数据"u8;
        var automation = "自动执行"u8;
        var actors     = "角色与 NPC"u8;
        var qdb        = "快速设计栏"u8;

        var ret = new ColorData<ColorId>[ColorId.Values.Count];

        ret[(int)NormalDesign] = new ColorData<ColorId>(ImGuiColor.Text, "普通设计"u8, "没有特殊规则设置的设计。"u8, designs);
        ret[(int)CustomizationDesign] = new ColorData<ColorId>(0xFFC000C0, "外貌设计"u8,
            "仅修改角色外貌的设计。"u8, designs);
        ret[(int)StateDesign] = new ColorData<ColorId>(0xFF00C0C0, "状态设计"u8,
            "不修改角色外貌或装备的设计。"u8, designs);
        ret[(int)EquipmentDesign] =
            new ColorData<ColorId>(0xFF00C000, "装备设计"u8, "只修改角色装备的设计。"u8, designs);
        ret[(int)ActorAvailable] = new ColorData<ColorId>(DalamudColor.SuccessForeground, "角色可用"u8,
            "如果在游戏世界中此角色至少存在过一次，附近角色选项卡中的角色标题会显示为此颜色。"u8, actors);
        ret[(int)ActorUnavailable] = new ColorData<ColorId>(DalamudColor.ErrorForeground, "角色不可用"u8,
            "如果在游戏世界中此角色当前不存在，附近角色选项卡中的角色标题会显示为此颜色。"u8, actors);
        ret[(int)FolderExpanded] =
            new ColorData<ColorId>(FolderLine, "展开的设计折叠组"u8, "当前展开的设计折叠组，标题会显示为此颜色。"u8, designs);
        ret[(int)FolderCollapsed] =
            new ColorData<ColorId>(FolderLine, "收起的设计折叠组"u8, "当前收起的设计折叠组，标题会显示为此颜色。"u8, designs);
        ret[(int)FolderLine] = new ColorData<ColorId>(0xFFFFF0C0, "展开设计折叠组竖线"u8,
            "表示哪些子设计隶属于展开的折叠组，用于标识树形目录结构的竖线会显示为此颜色。"u8, designs);
        ret[(int)AlternatingFolderLine] = new ColorData<ColorId>(FolderLine, "交错折叠组竖线"u8,
            "偶数层展开折叠组用来标识子项归属的竖线颜色。"u8, designs);
        ret[(int)EnabledAutoSet] = new ColorData<ColorId>(0xFFA0F0A0, "已启用的自动执行集"u8,
            "当前已启用的自动化执行集。每个角色只能启用一个。"u8, automation);
        ret[(int)DisabledAutoSet] =
            new ColorData<ColorId>(ImGuiColor.TextDisabled, "已禁用的自动执行集"u8, "当前已禁用的自动化执行集。"u8, automation);
        ret[(int)AutomationActorAvailable] = new ColorData<ColorId>(ImGuiColor.Text, "自动执行关联角色存在"u8,
            "与自动执行集关联的角色当前存在。"u8, automation);
        ret[(int)AutomationActorUnavailable] = new ColorData<ColorId>(ImGuiColor.TextDisabled, "自动执行关联角色不存在"u8,
            "与自动执行集关联的角色当前不存在。"u8, automation);
        ret[(int)HeaderButtons] = new ColorData<ColorId>(0xFFFFF0C0, "标题按钮"u8,
            "标题处按钮的文本和边框颜色。比如匿名开关按钮。"u8, metadata);
        ret[(int)FavoriteStarOn] = new ColorData<ColorId>(0xFF40D0D0, "收藏物品"u8,
            "收藏物品的五角星和已解锁选项卡总览模式中的边框的颜色。"u8, metadata);
        ret[(int)FavoriteStarHovered] = new ColorData<ColorId>(0xFFD040D0, "收藏五角星悬停"u8,
            "鼠标在收藏物品五角星按钮上悬停时的颜色。"u8, metadata);
        ret[(int)FavoriteStarOff] = new ColorData<ColorId>(0x20808080, "收藏五角星轮廓"u8,
            "收藏品五角星的默认颜色。"u8, metadata);
        ret[(int)PredefinedTagAdd] = new ColorData<ColorId>(DalamudColor.SuccessBackground, "预定义标签：添加"u8,
            "当前设计尚未包含、可以添加的预定义标签。"u8, metadata);
        ret[(int)PredefinedTagRemove] = new ColorData<ColorId>(DalamudColor.ErrorBackground, "预定义标签：移除"u8,
            "当前设计已包含、可以移除的预定义标签。"u8, metadata);
        ret[(int)QuickDesignButton] = new ColorData<ColorId>(0x900A0A0A, "快速设计栏按钮背景"u8,
            "快速设计栏中按钮框体的颜色。"u8, qdb);
        ret[(int)QuickDesignFrame] = new ColorData<ColorId>(0x90383838, "快速设计栏选择器背景"u8,
            "快速设计栏中设计选择器的背景颜色。"u8, qdb);
        ret[(int)QuickDesignBg] = new ColorData<ColorId>(0x00F0F0F0, "快速设计栏窗口背景"u8,
            "快速设计栏中窗口的背景颜色。"u8, qdb);
        ret[(int)TriStateCheck] = new ColorData<ColorId>(0xFF00D000, "三态复选框√（打勾）"u8,
            "复选框中表示选中的符号的颜色。"u8, metadata);
        ret[(int)TriStateCross] = new ColorData<ColorId>(0xFF0000D0, "三态复选框×（打叉）"u8,
            "复选框中表示反选的符号的颜色。"u8, metadata);
        ret[(int)TriStateNeutral] = new ColorData<ColorId>(0xFFD0D0D0, "三态复选框●（点选）"u8,
            "复选框中表示保持原样的符号的颜色。"u8, metadata);
        ret[(int)BattleNpc] = new ColorData<ColorId>(ImGuiColor.Text, "NPC 选项卡中的战斗 NPC"u8,
            "NPC 选项卡中没有指定其他颜色的战斗 NPC 名称的颜色。"u8, actors);
        ret[(int)EventNpc] = new ColorData<ColorId>(ImGuiColor.Text, "NPC 选项卡中的事件 NPC"u8,
            "NPC 选项卡中没有指定其他颜色的事件 NPC 名称的颜色。"u8, actors);
        ret[(int)ModdedItemMarker] = new ColorData<ColorId>(0xFFFF20FF, "已修改物品标记"u8,
            "在解锁总览选项卡中表示该物品在当前选择的 Penumbra 合集中的颜色。"u8,
            metadata);
        ret[(int)ContainsItemsEnabled] = new ColorData<ColorId>(0xFFA0F0A0, "启用的模组包含设计物品"u8,
            "在关联模组下拉菜单中启用的模组包含此设计中使用的物品时的颜色。"u8, metadata);
        ret[(int)ContainsItemsDisabled] = new ColorData<ColorId>(0x80A0F0A0, "禁用的模组包含设计物品"u8,
            "在关联模组下拉菜单中禁用的模组包含此设计中使用的物品时的颜色。"u8, metadata);
        ret[(int)AdvancedDyeActive] = new ColorData<ColorId>(0xFF58DDFF, "高级染料激活"u8,
            "如果此槽位有任何高级染料激活，高级染料按钮和标记的高亮颜色。"u8, metadata);

        foreach (var data in ret)
        {
            if (data.Default.Value is 0)
                throw new SystemException("A color ID has no data assigned.");
        }

        return ret;
    }

    /// <summary> The old hardcoded default values used for migration. </summary>
    internal static Rgba32 OldDefault(ColorId id)
        => id switch
        {
            NormalDesign               => 0xFFFFFFFF,
            CustomizationDesign        => 0xFFC000C0,
            StateDesign                => 0xFF00C0C0,
            EquipmentDesign            => 0xFF00C000,
            ActorAvailable             => 0xFF18C018,
            ActorUnavailable           => 0xFF1818C0,
            FolderExpanded             => 0xFFFFF0C0,
            FolderCollapsed            => 0xFFFFF0C0,
            FolderLine                 => 0xFFFFF0C0,
            AlternatingFolderLine      => 0xFFFFF0C0,
            EnabledAutoSet             => 0xFFA0F0A0,
            DisabledAutoSet            => 0xFF808080,
            AutomationActorAvailable   => 0xFFFFFFFF,
            AutomationActorUnavailable => 0xFF808080,
            HeaderButtons              => 0xFFFFF0C0,
            FavoriteStarOn             => 0xFF40D0D0,
            FavoriteStarHovered        => 0xFFD040D0,
            FavoriteStarOff            => 0x20808080,
            PredefinedTagAdd           => 0xFF18C018,
            PredefinedTagRemove        => 0xFF1818C0,
            QuickDesignButton          => 0x900A0A0A,
            QuickDesignFrame           => 0x90383838,
            QuickDesignBg              => 0x00F0F0F0,
            TriStateCheck              => 0xFF00D000,
            TriStateCross              => 0xFF0000D0,
            TriStateNeutral            => 0xFFD0D0D0,
            BattleNpc                  => 0xFFFFFFFF,
            EventNpc                   => 0xFFFFFFFF,
            ModdedItemMarker           => 0xFFFF20FF,
            ContainsItemsEnabled       => 0xFFA0F0A0,
            ContainsItemsDisabled      => 0x80A0F0A0,
            AdvancedDyeActive          => 0xFF58DDFF,
            _                          => 0,
        };
}
