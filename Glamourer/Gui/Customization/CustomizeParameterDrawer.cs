using Glamourer.Config;
using Glamourer.Designs;
using Glamourer.GameData;
using Glamourer.Interop.PalettePlus;
using Glamourer.State;
using ImSharp;
using Luna;

namespace Glamourer.Gui.Customization;

public class CustomizeParameterDrawer(Configuration config, PaletteImport import) : IService
{
    private readonly Dictionary<Design, CustomizeParameterData> _lastData    = [];
    private          StringU8                                   _paletteName = StringU8.Empty;
    private          CustomizeParameterData                     _data;
    private          CustomizeParameterFlag                     _flags;
    private          float                                      _width;
    private          CustomizeParameterValue?                   _copy;

    public void Draw(DesignManager designManager, Design design)
    {
        using var generalSize = EnsureSize();
        DrawPaletteImport(designManager, design);
        DrawConfig(true);

        using (Im.Item.PushWidth(_width - 2 * Im.Style.FrameHeight - 2 * Im.Style.ItemInnerSpacing.X))
        {
            foreach (var flag in CustomizeParameterExtensions.RgbFlags)
                DrawColorInput3(CustomizeParameterDrawData.FromDesign(designManager, design, flag), true);

            foreach (var flag in CustomizeParameterExtensions.RgbaFlags)
                DrawColorInput4(CustomizeParameterDrawData.FromDesign(designManager, design, flag));
        }

        foreach (var flag in CustomizeParameterExtensions.PercentageFlags)
            DrawPercentageInput(CustomizeParameterDrawData.FromDesign(designManager, design, flag));

        foreach (var flag in CustomizeParameterExtensions.ValueFlags)
            DrawValueInput(CustomizeParameterDrawData.FromDesign(designManager, design, flag));
    }

    public void Draw(StateManager stateManager, ActorState state)
    {
        using var generalSize = EnsureSize();
        DrawConfig(false);
        using (Im.Item.PushWidth(_width - 2 * Im.Style.FrameHeight - 2 * Im.Style.ItemInnerSpacing.X))
        {
            foreach (var flag in CustomizeParameterExtensions.RgbFlags)
                DrawColorInput3(CustomizeParameterDrawData.FromState(stateManager, state, flag), state.ModelData.Customize.Highlights);

            foreach (var flag in CustomizeParameterExtensions.RgbaFlags)
                DrawColorInput4(CustomizeParameterDrawData.FromState(stateManager, state, flag));
        }

        foreach (var flag in CustomizeParameterExtensions.PercentageFlags)
            DrawPercentageInput(CustomizeParameterDrawData.FromState(stateManager, state, flag));

        foreach (var flag in CustomizeParameterExtensions.ValueFlags)
            DrawValueInput(CustomizeParameterDrawData.FromState(stateManager, state, flag));
    }

    private void DrawPaletteCombo()
    {
        using var id    = Im.Id.Push("Palettes"u8);
        using var combo = Im.Combo.Begin("##import"u8, _paletteName.Length > 0 ? _paletteName : "选择Palette设置..."u8);
        if (!combo)
            return;

        foreach (var (name, (palette, flags)) in import.Data)
        {
            if (!Im.Selectable(name, _paletteName == name))
                continue;

            _paletteName = name;
            _data        = palette;
            _flags       = flags;
        }
    }

    private void DrawPaletteImport(DesignManager manager, Design design)
    {
        if (!config.ShowPalettePlusImport)
            return;

        DrawPaletteCombo();

        Im.Line.SameInner();
        var value = true;
        if (Im.Checkbox("显示导入选项"u8, ref value))
        {
            config.ShowPalettePlusImport = false;
            config.Save();
        }

        Im.Tooltip.OnHover("在所有设计中隐藏Palette+导入栏。关闭后可以在Glamourer界面设置中重新启用。"u8);

        var buttonWidth = new Vector2((_width - Im.Style.ItemInnerSpacing.X) / 2, 0);
        if (ImEx.Button("应用导入"u8, buttonWidth, _paletteName.Length > 0
                ? $"将Palette+调色板[{_paletteName}]中的数据导入到此设计。"
                : "请先选择一个Palette+调色板。", _paletteName.Length is 0 || design.WriteProtected()))
        {
            _lastData[design] = design.DesignData.Parameters;
            foreach (var parameter in _flags.Iterate())
                manager.ChangeCustomizeParameter(design, parameter, _data[parameter]);
        }

        Im.Line.SameInner();
        var enabled = _lastData.TryGetValue(design, out var oldData);
        if (ImEx.Button("还原导入"u8, buttonWidth, enabled
                ? $"还原[{design.Name}]到导入前的最后一组高级（外貌）参数。"
                : $"你尚未导入任何可以供[{design.Name}]还原的数据。", !enabled || design.WriteProtected()))
        {
            _lastData.Remove(design);
            foreach (var parameter in CustomizeParameterExtensions.AllFlags)
                manager.ChangeCustomizeParameter(design, parameter, oldData[parameter]);
        }
    }


    private void DrawConfig(bool withApply)
    {
        if (!config.ShowColorConfig)
            return;

        DrawColorDisplayOptions();
        DrawColorFormatOptions(withApply);
        var value = config.ShowColorConfig;
        Im.Line.Same();
        if (Im.Checkbox("显示设置"u8, ref value))
        {
            config.ShowColorConfig = value;
            config.Save();
        }

        Im.Tooltip.OnHover(
            "隐藏“外貌（高级）”面板中的颜色配置选项。可以在Glamourer界面设置中重新启用。"u8);
    }

    private void DrawColorDisplayOptions()
    {
        using var group = Im.Group();
        if (Im.RadioButton("RGB"u8, config.UseRgbForColors) && !config.UseRgbForColors)
        {
            config.UseRgbForColors = true;
            config.Save();
        }

        Im.Line.Same();
        if (Im.RadioButton("HSV"u8, !config.UseRgbForColors) && config.UseRgbForColors)
        {
            config.UseRgbForColors = false;
            config.Save();
        }
    }

    private void DrawColorFormatOptions(bool withApply)
    {
        var width = _width
          - (Im.Font.CalculateSize("浮点数"u8).X
              + Im.Font.CalculateButtonSize("整数"u8).X
              + 2 * Im.Style.ItemSpacing.X)
          + Im.Style.ItemInnerSpacing.X
          + Im.Item.Size.X;
        if (!withApply)
            width -= Im.Style.FrameHeight + Im.Style.ItemInnerSpacing.X;

        Im.Line.Same(0, width);
        if (Im.RadioButton("浮点数"u8, config.UseFloatForColors) && !config.UseFloatForColors)
        {
            config.UseFloatForColors = true;
            config.Save();
        }

        Im.Line.Same();
        if (Im.RadioButton("整数"u8, !config.UseFloatForColors) && config.UseFloatForColors)
        {
            config.UseFloatForColors = false;
            config.Save();
        }
    }

    private void DrawColorInput3(in CustomizeParameterDrawData data, bool allowHighlights)
    {
        using var id           = Im.Id.Push((int)data.Flag);
        var       value        = data.CurrentValue.InternalTriple;
        var       noHighlights = !allowHighlights && data.Flag is CustomizeParameterFlag.HairHighlight;
        DrawCopyPasteButtons(data, data.Locked || noHighlights);
        Im.Line.SameInner();
        using (Im.Disabled(data.Locked || noHighlights))
        {
            if (Im.Color.Editor("##value"u8, ref value, GetFlags()))
                data.ChangeParameter(new CustomizeParameterValue(value));
        }

        if (noHighlights)
            Im.Tooltip.OnHover(HoveredFlags.AllowWhenDisabled, "挑染在“外貌”选项中被禁用，需要使用请去“外貌”中启用。"u8);

        DrawRevert(data);

        DrawApplyAndLabel(data);
    }

    private void DrawColorInput4(in CustomizeParameterDrawData data)
    {
        using var id    = Im.Id.Push((int)data.Flag);
        var       value = data.CurrentValue.InternalQuadruple;
        DrawCopyPasteButtons(data, data.Locked);
        Im.Line.SameInner();
        using (Im.Disabled(data.Locked))
        {
            if (Im.Color.Editor("##value"u8, ref value, GetFlags() | ColorEditorFlags.AlphaPreviewHalf))
                data.ChangeParameter(new CustomizeParameterValue(value));
        }

        DrawRevert(data);

        DrawApplyAndLabel(data);
    }

    private void DrawValueInput(in CustomizeParameterDrawData data)
    {
        using var id    = Im.Id.Push((int)data.Flag);
        var       value = data.CurrentValue[0];

        using (Im.Disabled(data.Locked))
        {
            if (Im.Input.Scalar("##value"u8, ref value, 0.1f, 0.5f))
                data.ChangeParameter(new CustomizeParameterValue(value));
        }

        DrawRevert(data);

        DrawApplyAndLabel(data);
    }

    private void DrawPercentageInput(in CustomizeParameterDrawData data)
    {
        using var id    = Im.Id.Push((int)data.Flag);
        var       value = data.CurrentValue[0] * 100f;

        using (Im.Disabled(data.Locked))
        {
            if (Im.Slider("##value"u8, ref value, "%.2f"u8, -100f, 300))
                data.ChangeParameter(new CustomizeParameterValue(value / 100f));
            Im.Tooltip.OnHover("除了拖动滑块调整数值，还可以按住Ctrl单击此项手动输入任意值。"u8);
        }

        DrawRevert(data);

        DrawApplyAndLabel(data);
    }

    private static void DrawRevert(in CustomizeParameterDrawData data)
    {
        if (data.Locked || !data.AllowRevert)
            return;

        if (Im.Item.RightClicked() && Im.Io.KeyControl)
            data.ChangeParameter(data.GameValue);

        Im.Tooltip.OnHover("按住Ctrl并单击右键可恢复到游戏值。"u8);
    }

    private static void DrawApply(in CustomizeParameterDrawData data)
    {
        if (UiHelpers.DrawCheckbox("##apply"u8, "当应用此设计时也应用此参数。"u8, data.CurrentApply, out var enabled,
                data.Locked))
            data.ChangeApplyParameter(enabled);
    }

    private void DrawApplyAndLabel(in CustomizeParameterDrawData data)
    {
        if (data.DisplayApplication && !config.HideApplyCheckmarks)
        {
            Im.Line.SameInner();
            DrawApply(data);
        }

        Im.Line.SameInner();
        Im.Text(data.Flag.ToNameU8());
    }

    private ColorEditorFlags GetFlags()
        => Format | Display | ColorEditorFlags.Hdr | ColorEditorFlags.NoOptions;

    private ColorEditorFlags Format
        => config.UseFloatForColors ? ColorEditorFlags.Float : ColorEditorFlags.Uint8;

    private ColorEditorFlags Display
        => config.UseRgbForColors ? ColorEditorFlags.DisplayRgb : ColorEditorFlags.DisplayHsv;

    private Im.ItemWidthDisposable EnsureSize()
    {
        var iconSize = Im.Style.TextHeight * 2 + Im.Style.ItemSpacing.Y + 4 * Im.Style.FramePadding.Y;
        _width = 7 * iconSize + 4 * Im.Style.ItemInnerSpacing.X;
        return Im.Item.PushWidth(_width);
    }

    private void DrawCopyPasteButtons(in CustomizeParameterDrawData data, bool locked)
    {
        if (ImEx.Icon.Button(LunaStyle.ToClipboardIcon, "复制此颜色以备稍后使用。"u8))
            _copy = data.CurrentValue;
        Im.Line.SameInner();
        if (ImEx.Icon.Button(LunaStyle.FromClipboardIcon, _copy.HasValue ? "粘贴当前复制的值。"u8 : "尚未复制任何值。"u8,
                locked || !_copy.HasValue))
            data.ChangeParameter(_copy!.Value);
    }
}
