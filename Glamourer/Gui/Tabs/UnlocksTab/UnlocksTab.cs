using ImSharp;
using Luna;

namespace Glamourer.Gui.Tabs.UnlocksTab;

public sealed class UnlocksTab : Window, ITab<MainTabType>
{
    private readonly Config.EphemeralConfig _config;
    private readonly UnlockOverview                _overview;
    private readonly UnlockTable                   _table;

    public UnlocksTab(Config.EphemeralConfig config, UnlockOverview overview, UnlockTable table)
        : base("已解锁装备")
    {
        _config   = config;
        _overview = overview;
        _table    = table;

        Flags  |= WindowFlags.NoDocking;
        IsOpen =  false;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(700,  675),
            MaximumSize = new Vector2(3840, 2160),
        };
    }

    private bool DetailMode
    {
        get => _config.UnlockDetailMode;
        set
        {
            _config.UnlockDetailMode = value;
            _config.Save();
        }
    }

    public ReadOnlySpan<byte> Label
        => "解锁物品"u8;

    public MainTabType Identifier
        => MainTabType.Unlocks;

    public void DrawContent()
    {
        DrawTypeSelection();
        if (DetailMode)
            _table.Draw();
        else
            _overview.Draw();
        _table.Flags |= TableFlags.Resizable;
    }

    public override void Draw()
    {
        DrawContent();
    }

    private void DrawTypeSelection()
    {
        using var style = ImStyleDouble.ItemSpacing.Push(Vector2.Zero)
            .Push(ImStyleSingle.FrameRounding, 0);
        var buttonSize = new Vector2(Im.ContentRegion.Available.X / 2, Im.Style.FrameHeight);
        if (!IsOpen)
            buttonSize.X -= Im.Style.FrameHeight / 2;
        if (DetailMode)
            buttonSize.X -= Im.Style.FrameHeight / 2;

        if (ImEx.Button("总览模式"u8, buttonSize, "显示已解锁物品的图标。"u8, !DetailMode))
            DetailMode = false;

        Im.Line.Same();
        if (ImEx.Button("详情模式"u8, buttonSize, "显示所有解锁数据为可筛选和排序的组合表格。"u8,
                DetailMode))
            DetailMode = true;

        if (DetailMode)
        {
            Im.Line.Same();
            if (ImEx.Icon.Button(LunaStyle.AutoResizeIcon, "将所有列恢复到其原始大小。"u8))
                _table.Flags &= ~TableFlags.Resizable;
        }

        if (!IsOpen)
        {
            Im.Line.Same();
            if (ImEx.Icon.Button(LunaStyle.PopOutIcon, "打开“解锁物品”独立窗口。"u8))
                IsOpen = true;
        }
    }
}
