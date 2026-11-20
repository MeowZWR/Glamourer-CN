using Glamourer.Automation;
using Glamourer.Config;
using Glamourer.Designs;
using Glamourer.Events;
using Glamourer.Gui;
using ImSharp;
using Luna;

namespace Glamourer.Gui.Tabs.AutomationTab;

public sealed class CompanionDesignCombo(
    EphemeralConfig config,
    DesignManager designs,
    DesignChanged designChanged,
    DesignColors designColors,
    NavigationService navigator,
    DesignFileSystem designFileSystem) : DesignComboBase(config, designs, designChanged, designColors, navigator, designFileSystem),
    IUiService
{
    public const string Requirement = "只能选择已启用全部外貌规则的人类设计。";

    protected override IEnumerable<CacheItem> GetItems()
        => Designs.Designs
            .Where(CompanionDesignRules.HasAllCustomizeRules)
            .Select(CreateItem)
            .OrderBy(CacheItem.Ordering);

    public override bool Draw(Utf8StringHandler<LabelStringHandlerBuffer> label, IDesignStandIn? currentDesign,
        out IDesignStandIn? newSelection, float width)
    {
        var ret = base.Draw(label, currentDesign, out newSelection, width);
        if (Im.Item.Hovered() && (currentDesign is null || !CompanionDesignRules.HasAllCustomizeRules(currentDesign)))
        {
            using var style = Im.Style.PushDefault();
            using var tt    = Im.Tooltip.Begin();
            Im.Text(Requirement);
        }

        if (ret && newSelection is not null && !CompanionDesignRules.HasAllCustomizeRules(newSelection))
        {
            newSelection = currentDesign;
            return false;
        }

        return ret;
    }
}
