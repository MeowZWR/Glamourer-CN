using Glamourer.Config;
using Glamourer.Designs;
using ImSharp;
using Luna;

namespace Glamourer.Gui.Tabs.DesignTab;

public sealed class DeleteSelectionButton(DesignFileSystem fileSystem, DesignManager manager, Configuration config)
    : BaseIconButton<AwesomeIcon>
{
    /// <inheritdoc/>
    public override AwesomeIcon Icon
        => LunaStyle.DeleteIcon;

    /// <inheritdoc/>
    public override bool HasTooltip
        => true;

    /// <inheritdoc/>
    public override void DrawTooltip()
    {
        var anySelected = fileSystem.Selection.DataNodes.Count > 0;
        var modifier    = Enabled;

        Im.Text(anySelected
            ? "删除所选设计，无法撤销。"u8
            : "未选择设计。"u8);
        if (!modifier)
            Im.Text($"\n按住 {config.DeleteDesignModifier} 点击以删除设计。");
    }

    /// <inheritdoc/>
    public override bool Enabled
        => config.DeleteDesignModifier.IsActive() && fileSystem.Selection.DataNodes.Count > 0;

    /// <inheritdoc/>
    public override void OnClick()
    {
        var designs = fileSystem.Selection.DataNodes.Select(n => n.Value).OfType<Design>().ToList();
        fileSystem.Selection.UnselectAll();
        foreach (var design in designs)
            manager.Delete(design);
    }
}
