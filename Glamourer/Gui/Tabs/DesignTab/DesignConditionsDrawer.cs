using Glamourer.Designs;
using Glamourer.Interop;
using ImSharp;
using Luna;
using Penumbra.GameData.Structs;

namespace Glamourer.Gui.Tabs.DesignTab;

public class DesignConditionsDrawer(JobService jobs) : IService
{
    private readonly JobGroupCombo _jobGroupCombo = new(jobs);

    public bool Draw(in DesignConditions conditions, out DesignConditions newConditions)
    {
        newConditions = conditions;
        var changed      = false;
        var usingGearset = conditions.GearsetIndex >= 0;
        if (Im.Button(usingGearset ? "套装:##usingGearset"u8 : "职业:##usingGearset"u8))
        {
            usingGearset  = !usingGearset;
            newConditions = conditions with { GearsetIndex = (short)(usingGearset ? 0 : -1) };
            changed       = true;
        }

        Im.Tooltip.OnHover("点击切换职业和套装限制."u8);

        Im.Line.SameInner();
        if (usingGearset)
        {
            Im.Item.SetNextWidthFull();
            if (ImEx.InputOnDeactivation.Scalar("##whichGearset"u8, conditions.GearsetIndex + 1, out var newIndex))
            {
                newConditions = conditions with { GearsetIndex = (short)(Math.Clamp(newIndex, 1, 100) - 1) };
                changed       = true;
            }
        }
        else
        {
            if (_jobGroupCombo.Draw(conditions.Jobs, out var newJobs))
            {
                newConditions = conditions with { Jobs = newJobs };
                changed       = true;
            }
        }

        return changed;
    }

    private sealed class JobGroupCombo(JobService jobs)
        : SimpleFilterCombo<JobGroup>(SimpleFilterType.Partwise)
    {
        public bool Draw(in JobGroup jobGroup, out JobGroup newGroup)
        {
            if (Draw("##jobGroups"u8, in jobGroup,
                    "选择此设计应该应用的职业组。\nCtrl + 右键点击设置为所有职业。"u8,
                    Im.ContentRegion.Available.X, out newGroup))
                return true;

            if (Im.Io.KeyControl && Im.Item.RightClicked())
            {
                newGroup = jobs.JobGroups[1];
                return true;
            }

            newGroup = jobGroup;
            return false;
        }

        public override StringU8 DisplayString(in JobGroup value)
            => value.Name;

        public override string FilterString(in JobGroup value)
            => value.Name.ToString();

        public override IEnumerable<JobGroup> GetBaseItems()
            => jobs.JobGroups.Values;
    }
}
