using Glamourer.Config;
using Glamourer.Services;
using Glamourer.State;
using ImSharp;
using Luna;

namespace Glamourer.Gui.Tabs.SettingsTab;

public class CodeDrawer(Configuration config, CodeService codeService, FunModule funModule) : IUiService
{
    private static ReadOnlySpan<byte> Tooltip
        => "作弊代码实际上不是为了在游戏中作弊，而是为了在 Glamourer 中“作弊”。 "u8
          + "它们允许插件执行一些有趣的彩蛋模式，这些模式通常会以某种方式改变您看到的所有玩家（包括您自己）的外观。"u8;

    private static ReadOnlySpan<byte> DragDropLabel
        => "##CheatDrag"u8;

    private bool   _showCodeHints;
    private string _currentCode = string.Empty;
    private int    _dragCodeIdx = -1;


    public void Draw()
    {
        var show = Im.Tree.Header("作弊代码"u8);
        DrawTooltip();

        if (!show)
            return;

        DrawCodeInput();
        DrawCopyButtons();
        var knownFlags = DrawCodes();
        DrawCodeHints(knownFlags);
    }

    private void DrawCodeInput()
    {
        var       color  = codeService.CheckCode(_currentCode).Item2 is not 0 ? ColorId.ActorAvailable : ColorId.ActorUnavailable;
        using var border = ImStyleBorder.Frame.Push(color.Value(), Im.Style.GlobalScale, _currentCode.Length > 0);
        Im.Item.SetNextWidth(500 * Im.Style.GlobalScale + Im.Style.ItemSpacing.X);
        if (Im.Input.Text("##Code"u8, ref _currentCode, "输入作弊代码..."u8, InputTextFlags.EnterReturnsTrue))
        {
            codeService.AddCode(_currentCode);
            _currentCode = string.Empty;
        }

        Im.Line.Same();
        ImEx.Icon.Draw(LunaStyle.WarningIcon, ImGuiColor.TextDisabled.Get());
        DrawTooltip();
    }

    private void DrawCopyButtons()
    {
        var buttonSize = ImEx.ScaledVectorX(250);
        if (Im.Button("Who am I?!?"u8, buttonSize))
            funModule.WhoAmI();
        Im.Tooltip.OnHover("将你的角色当前的实际外观（包括作弊代码或节日活动）复制到剪贴板作为一个设计。"u8);

        Im.Line.Same();

        if (Im.Button("Who is that!?!"u8, buttonSize))
            funModule.WhoIsThat();
        Im.Tooltip.OnHover("将你的目标当前的实际外观（包括作弊代码或节日活动）复制到剪贴板作为一个设计。"u8);
    }

    private CodeService.CodeFlag DrawCodes()
    {
        var                  canDelete  = config.DeleteDesignModifier.IsActive();
        CodeService.CodeFlag knownFlags = 0;
        for (var i = 0; i < config.Codes.Count; ++i)
        {
            using var id = Im.Id.Push(i);
            var (code, state)  = config.Codes[i];
            var (action, flag) = codeService.CheckCode(code);
            if (flag is 0)
                continue;

            var data = CodeService.GetData(flag);

            if (ImEx.Icon.Button(LunaStyle.DeleteIcon, $"删除此作弊代码。{(canDelete ? StringU8.Empty : $"\n按住 {config.DeleteDesignModifier} 键并单击以删除。")}",
                    disabled: !canDelete))
            {
                action!(false);
                config.Codes.RemoveAt(i--);
                codeService.SaveState();
            }

            knownFlags |= flag;
            Im.Line.SameInner();
            if (Im.Checkbox(StringU8.Empty, ref state))
            {
                action!(state);
                codeService.SaveState();
            }

            var hovered = Im.Item.Hovered();
            Im.Line.Same();
            Im.Selectable(code);
            hovered |= Im.Item.Hovered();
            DrawSource(i, code);
            DrawTarget(i);
            if (hovered)
            {
                using var tt = Im.Tooltip.Begin();
                Im.Text(data.Effect);
            }
        }

        return knownFlags;
    }

    private void DrawSource(int idx, string code)
    {
        using var source = Im.DragDrop.Source();
        if (!source)
            return;

        if (!source.SetPayload(DragDropLabel))
            _dragCodeIdx = idx;
        Im.Text($"拖拽 {code}...");
    }

    private void DrawTarget(int idx)
    {
        using var target = Im.DragDrop.Target();
        if (!target.IsDropping(DragDropLabel) || _dragCodeIdx is -1)
            return;

        if (config.Codes.Move(_dragCodeIdx, idx))
            codeService.SaveState();
        _dragCodeIdx = -1;
    }

    private void DrawCodeHints(CodeService.CodeFlag knownFlags)
    {
        if (knownFlags.HasFlag(CodeService.AllHintCodes))
            return;

        if (Im.Button(_showCodeHints ? "隐藏提示"u8 : "显示提示"u8))
            _showCodeHints = !_showCodeHints;

        if (!_showCodeHints)
            return;

        foreach (var code in CodeService.CodeFlag.Values)
        {
            if (knownFlags.HasFlag(code))
                continue;

            var data = CodeService.GetData(code);
            if (!data.Display)
                continue;

            Im.Dummy(Vector2.Zero);
            Im.Separator();
            Im.Dummy(Vector2.Zero);
            Im.Text(data.Effect);
            using var indent = Im.Indent(2);
            using (Im.Group())
            {
                Im.Text("大写字母："u8);
                Im.Text("标点符号："u8);
            }

            Im.Line.SameInner();
            using (Im.Group())
            {
                using var mono = Im.Font.PushMono();
                Im.Text($"{data.CapitalCount}");
                Im.Text($"{data.Punctuation}");
            }

            Im.TextWrapped(data.Hint);
        }
    }


    private static void DrawTooltip()
    {
        if (!Im.Item.Hovered())
            return;

        Im.Window.SetNextSize(new Vector2(400, 0));
        using var tt = Im.Tooltip.Begin();
        Im.TextWrapped(Tooltip);
    }
}
