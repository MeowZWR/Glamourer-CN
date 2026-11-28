using Glamourer.Api.Enums;
using Glamourer.Config;
using Glamourer.Gui;
using ImSharp;
using Luna;

namespace Glamourer.MountReplacement.Gui;

public sealed class MountReplacementTab : ITab<MainTabType>
{
    private readonly MountReplacementManager _manager;
    private readonly Configuration           _config;
    private readonly MountCombo              _sourceCombo;
    private readonly MountCombo              _targetCombo;
    private          Action?                 _endAction;

    public MountReplacementTab(MountReplacementManager manager, Configuration config)
    {
        _manager     = manager;
        _config      = config;
        _sourceCombo = new MountCombo(manager, MountCombo.AnyLabel);
        _targetCombo = new MountCombo(manager, MountCombo.UnsetLabel);
    }

    public bool IsVisible
        => _config.ShowMountReplacementPanel;

    public ReadOnlySpan<byte> Label
        => "坐骑替换"u8;

    public MainTabType Identifier
        => MainTabType.MountReplacement;

    public void DrawContent()
    {
        var enabled = _manager.Enabled;
        if (Im.Checkbox("启用坐骑替换"u8, ref enabled))
            _manager.SetEnabled(enabled);
        Im.Line.Same();
        LunaStyle.DrawAlignedHelpMarker(
            "召唤原坐骑时，在本地将其外观显示为另一坐骑，多座位数乘客骑乘时可能显示异常。\n所选坐骑的规则优先于「任意坐骑」规则。"u8);
        Im.Separator();

        using var table = Im.Table.Begin("MountReplacementTable"u8, 4, TableFlags.RowBackground | TableFlags.ScrollY);
        if (!table)
            return;

        table.SetupScrollFreeze(0, 2);
        table.SetupColumn("##del"u8, TableColumnFlags.WidthFixed, Im.Style.FrameHeight);
        table.SetupColumn("原坐骑"u8, TableColumnFlags.WidthStretch);
        table.SetupColumn("显示为"u8, TableColumnFlags.WidthStretch);
        table.SetupColumn("启用"u8,  TableColumnFlags.WidthFixed, Im.Font.CalculateSize("启用"u8).X);
        table.HeaderRow();

        table.NextColumn();
        table.NextColumn();
        if (_sourceCombo.Draw("##newSource"u8, 0, Im.ContentRegion.Available.X, out var newSource, _manager.HasSource,
                MountCombo.NewSourceLabel))
            _endAction = () => _manager.AddRule(newSource);
        table.DrawFrameColumn("选择原坐骑以添加新规则"u8);
        table.NextColumn();

        for (var i = 0; i < _manager.Rules.Count; ++i)
            DrawRow(table, i);

        _endAction?.Invoke();
        _endAction = null;
    }

    private void DrawRow(in Im.TableDisposable table, int index)
    {
        using var id   = Im.Id.Push(index);
        var       rule = _manager.Rules[index];

        table.NextColumn();
        var keyValid = _config.DeleteDesignModifier.IsActive();
        if (ImEx.Icon.Button(LunaStyle.DeleteIcon, "删除此规则。"u8, !keyValid))
            _endAction = () => _manager.DeleteRule(index);
        if (!keyValid)
            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, $"按住 {_config.DeleteDesignModifier} 来删除此规则。");

        table.NextColumn();
        if (_sourceCombo.Draw("##source"u8, rule.Source, Im.ContentRegion.Available.X, out var newSource,
                id => id != rule.Source && _manager.HasSource(id)))
            _manager.ChangeSource(rule, newSource);

        table.NextColumn();
        if (_targetCombo.Draw("##target"u8, rule.Target, Im.ContentRegion.Available.X, out var newTarget,
                id => id is not 0 && id == rule.Source))
            _manager.ChangeTarget(rule, newTarget);

        table.NextColumn();
        DrawEnabled(rule);
    }

    private void DrawEnabled(MountReplacementRule rule)
    {
        var warning = Warning(rule);
        using (ImStyleBorder.Frame.Push(new ColorParameter(Colors.SelectedRed), Im.Style.GlobalScale, warning.Length > 0))
        {
            var enabled = rule.Enabled;
            if (Im.Checkbox("##enabled"u8, ref enabled))
                _manager.SetRuleEnabled(rule, enabled);
        }

        if (warning.Length > 0)
            Im.Tooltip.OnHover(warning);
    }

    private ReadOnlySpan<byte> Warning(MountReplacementRule rule)
    {
        if (!_manager.Mounts.TryGetValue(rule.Source, out var source) || !_manager.Mounts.TryGetValue(rule.Target, out var target))
            return [];

        var seats  = source.ExtraSeats != target.ExtraSeats;
        var flying = source.IsFlying != target.IsFlying;
        return (seats, flying) switch
        {
            (false, false) => [],
            (true, true)   => "两个坐骑的座位数和飞行能力不同，乘客位置和飞行动作可能显示异常。"u8,
            (true, false)  => "两个坐骑的座位数不同，乘客位置可能显示异常。"u8,
            _              => "两个坐骑的飞行能力不同，飞行动作可能显示异常。"u8,
        };
    }
}
