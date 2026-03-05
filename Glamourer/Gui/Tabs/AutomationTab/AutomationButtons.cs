using Glamourer.Automation;
using Glamourer.Config;
using ImSharp;
using Luna;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Interop;
using Penumbra.GameData.Structs;
using Penumbra.String;

namespace Glamourer.Gui.Tabs.AutomationTab;

public sealed class AutomationButtons : ButtonFooter
{
    public AutomationButtons(Configuration config, AutoDesignManager manager, AutomationSelection selection, ActorObjectManager objects)
    {
        Buttons.AddButton(new AddButton(objects, manager),              100);
        Buttons.AddButton(new DuplicateButton(selection, manager),      90);
        Buttons.AddButton(new HelpButton(),                             80);
        Buttons.AddButton(new DeleteButton(selection, config, manager), 70);
    }

    private sealed class AddButton(ActorObjectManager objects, AutoDesignManager manager) : BaseIconButton<AwesomeIcon>
    {
        private ActorIdentifier _identifier;

        public override AwesomeIcon Icon
            => LunaStyle.AddObjectIcon;

        public override bool HasTooltip
            => true;

        protected override void PreDraw()
        {
            _identifier = objects.Actors.GetCurrentPlayer();
            if (!_identifier.IsValid)
                _identifier = objects.Actors.CreatePlayer(ByteString.FromSpanUnsafe("新自动执行集"u8, true, false, true), WorldId.AnyWorld);
        }

        public override void DrawTooltip()
            => Im.Text($"创建一个新的自动执行集，与 {_identifier} 关联。关联的角色可以在以后更改。");

        public override bool Enabled
            => _identifier.IsValid;

        public override void OnClick()
            => manager.AddDesignSet("新自动执行集", _identifier);
    }

    private sealed class DuplicateButton(AutomationSelection selection, AutoDesignManager manager) : BaseIconButton<AwesomeIcon>
    {
        public override AwesomeIcon Icon
            => LunaStyle.DuplicateIcon;

        public override bool HasTooltip
            => true;

        public override void DrawTooltip()
            => Im.Text("复制当前的自动执行集。"u8);

        public override bool Enabled
            => selection.Set is not null;

        public override void OnClick()
            => manager.DuplicateDesignSet(selection.Set!);
    }

    private sealed class HelpButton : BaseIconButton<AwesomeIcon>
    {
        public override AwesomeIcon Icon
            => LunaStyle.InfoIcon;

        public override bool HasTooltip
            => true;

        public override void DrawTooltip()
            => Im.Text("自动执行如何工作？"u8);


        public override void OnClick()
            => Im.Popup.Open("Automation Help"u8);

        protected override void PostDraw()
        {
            var longestLine =
                Im.Font.CalculateSize(
                    "一个自动执行集可以包含多个自动执行设计，这些设计在不同的条件下和不同的设计部分中应用。"u8);
            ImEx.HelpPopup("Automation Help"u8, new Vector2(longestLine.X + 50 * Im.Style.GlobalScale, 33 * Im.Style.TextHeightWithSpacing),
                DrawHelp);
        }

        private static void DrawHelp()
        {
            var halfLine = new Vector2(Im.Style.TextHeight / 2);
            Im.Dummy(halfLine);
            Im.Text("什么是自动执行？"u8);
            Im.BulletText("自动执行可帮助你按指定条件自动应用设计到指定的角色。"u8);
            Im.Dummy(halfLine);

            Im.Text("自动执行集"u8);
            Im.BulletText("首先，你需要创建“自动执行集”。一个自动执行集可以是："u8);
            using var indent = Im.Indent();
            Im.BulletText("……已启用状态，或者"u8, ColorId.EnabledAutoSet.Value());
            Im.BulletText("……已禁用状态。"u8,   ColorId.DisabledAutoSet.Value());
            indent.Unindent();
            Im.BulletText("你可以创建新的、空的自动执行集，或复制现有的自动执行集。"u8);
            Im.BulletText("你可以为自动执行集随意命名。"u8);
            Im.BulletText("你可以通过拖拽重新排序自动执行集。"u8);
            Im.BulletText("每个自动执行集都分配给一个特定的角色。"u8);
            indent.Indent();
            Im.BulletText("在创建时，它被分配给你的当前玩家角色。"u8);
            Im.BulletText("你可以将其分配给任何玩家、雇员、模特以及大多数人类形态的 NPC。"u8);
            Im.BulletText("每个特定角色只能同时启用一个自动执行集。"u8);
            indent.Indent();
            Im.BulletText("启用另一个自动执行集会自动禁用前一个。"u8);
            indent.Unindent();
            indent.Unindent();

            Im.Dummy(halfLine);
            Im.Text("自动执行设计"u8);
            Im.BulletText(
                "单个自动执行集内可以包含多个“自动执行设计”，它们根据不同的触发条件应用各自的部分设计内容。"u8);
            Im.BulletText("这些自动执行设计的顺序也可以通过拖拽重新排序，并且与应用顺序相关。"u8);
            Im.BulletText("自动执行设计遵循自身的粗略应用规则，以及设计内的详细应用规则。"u8);
            Im.BulletText("自动执行设计可以配置为仅针对特定职业或职业组生效。"u8);
            Im.BulletText("还有一个特殊的“重置”选项，可用于将剩余的槽位重置回游戏的原始数值。"u8);
            Im.BulletText("自动执行设计遵循“自上而下”的应用原则，既可以在角色当前状态上叠加，也可以基于游戏原始状态应用。"u8);
            Im.BulletText("一个值若要生效，必须满足以下条件："u8);
            indent.Unindent();
            Im.BulletText("在设计本身中配置为应用。"u8);
            Im.BulletText("在自动执行规则中配置为应用。"u8);
            Im.BulletText("满足自动执行规则的条件。"u8);
            Im.BulletText("对于角色当前的实时状态（在其自身应用时）是一个有效的值。"u8);
            Im.BulletText("在此之前，没有被排序更靠前的其他设计修改过同一个数值。"u8);
            indent.Unindent();
        }
    }

    private sealed class DeleteButton(AutomationSelection selection, Configuration config, AutoDesignManager manager)
        : BaseIconButton<AwesomeIcon>
    {
        private bool _enabled;

        public override AwesomeIcon Icon
            => LunaStyle.DeleteIcon;

        public override bool HasTooltip
            => true;

        public override void DrawTooltip()
        {
            if (selection.Set is null)
            {
                Im.Text("未选择自动执行集。"u8);
            }
            else
            {
                Im.Text("删除当前选中的自动执行集。"u8);
                if (!_enabled)
                    Im.Text($"\n按住 {config.DeleteDesignModifier} 点击以删除。");
            }
        }

        public override bool Enabled
            => (_enabled = config.DeleteDesignModifier.IsActive()) && selection.Set is not null;

        public override void OnClick()
            => manager.DeleteDesignSet(selection.Index);
    }
}
