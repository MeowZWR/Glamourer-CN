using Glamourer.Config;
using Glamourer.Designs;
using Glamourer.Interop.Material;
using ImSharp;
using Luna;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Files.MaterialStructs;
using Penumbra.GameData.Gui;

namespace Glamourer.Gui.Materials;

public unsafe class MaterialDrawer(DesignManager designManager, Configuration config, TextureArraySlicePickers textureArraySlicePickers)
    : IService
{
    public const float SliderWidth      = 90;
    public const float ModeWidth        = 45;
    public const float RowBaseWidth     = 2 * SliderWidth + ModeWidth;
    public const float SheenSliderWidth = RowBaseWidth / 3;

    private int                _newMaterialIdx;
    private int                _newRowIdx;
    private MaterialValueIndex _newKey = MaterialValueIndex.FromSlot(EquipSlot.Head);

    private Vector2 _buttonSize;
    private float   _spacing;

    public void Draw(Design design)
    {
        var available = Im.ContentRegion.Available.X;
        _spacing    = Im.Style.ItemInnerSpacing.X;
        _buttonSize = new Vector2(Im.Style.FrameHeight);
        var colorWidth = 4 * _buttonSize.X
          + (SliderWidth * 2 + ModeWidth) * Im.Style.GlobalScale
          + 7 * _spacing
          + Im.Font.CalculateSize("Revert"u8).X;
        DrawMultiButtons(design);
        Im.Dummy(0);
        Im.Separator();
        Im.Dummy(0);
        DrawRevertSlots(design);
        Im.Dummy(0);
        Im.Separator();
        Im.Dummy(0);
        if (available > 3.25f * colorWidth)
            DrawSingleRow(design);
        else
            DrawMultipleRow(design);
        DrawNew(design);
    }

    private void DrawMultiButtons(Design design)
    {
        using var _ = Im.Disabled(design.WriteProtected());

        var any      = design.Materials.Count > 0;
        var disabled = !LunaStyle.Modifier.Destructive.Active;
        var size     = ImEx.ScaledVectorX(200);
        if (ImEx.Button("启用所有高级染色"u8, size,
                any
                    ? "启用所有包含的高级染色而不删除它们。"u8
                    : "此设计不包含任何高级染色。"u8,
                !any || disabled))
            designManager.ChangeApplyMulti(design, null, null, null, null, null, null, true, null);

        if (any)
            LunaStyle.Modifier.Destructive.TooltipLineBreak("enable"u8);

        Im.Line.Same();
        if (ImEx.Button("禁用所有高级染色"u8, size,
                any
                    ? "禁用所有包含的高级染色而不删除它们。"u8
                    : "此设计不包含任何高级染色。"u8,
                !any || disabled))
            designManager.ChangeApplyMulti(design, null, null, null, null, null, null, false, null);
        if (any)
            LunaStyle.Modifier.Destructive.TooltipLineBreak("disable"u8);

        if (ImEx.Button("删除所有高级染色"u8, size, any ? StringU8.Empty : "此设计不包含任何高级染色。"u8,
                !any || disabled))
            while (design.Materials.Count > 0)
                designManager.ChangeMaterialValue(design, MaterialValueIndex.FromKey(design.Materials[0].Item1), null);

        if (any)
            LunaStyle.Modifier.Destructive.TooltipLineBreak("delete"u8);
    }

    private void DrawRevertSlots(Design design)
    {
        Im.Cursor.FrameAlign();
        Im.Text("还原所有高级染色"u8);
        Im.Line.SameInner();
        LunaStyle.DrawAlignedHelpMarker(
            "设置此设计以在应用时还原此前应用的所有高级染色。\n\n如果此设计是设计链接或自动执行设置的一部分，设置此选项与在设计详情中设置重置高级染色的行为不同：\n- 设置此选项会重置此前应用的所有高级染色，并阻止较低的设计应用高级染色。其行为与在下方未显式设置为\"还原\"的每个槽的每一行中填入\"还原\"相同。\n- 在设计详情中设置重置高级染色会重置此前应用的所有高级染色，但允许较低的设计应用高级染色。"u8);
        Im.Line.Same();
        var slots = design.RevertAdvancedDyes;
        if (UiHelpers.DrawItemSlots("revertAdvancedDyes"u8, ref slots, readOnly: design.WriteProtected()))
            designManager.ChangeRevertAdvancedDyes(design, slots);
    }

    private void DrawName(MaterialValueIndex index)
    {
        using var style = ImStyleDouble.ButtonTextAlign.Push(new Vector2(0.05f, 0.5f));
        ImEx.TextFramed($"{index}", new Vector2((SliderWidth * 2 + ModeWidth) * Im.Style.GlobalScale + _spacing * 2, 0),
            borderColor: ImGuiColor.Text.Get());
    }

    private void DrawSingleRow(Design design)
    {
        for (var i = 0; i < design.Materials.Count; ++i)
        {
            using var id = Im.Id.Push(i);
            var (idx, value) = design.Materials[i];
            var key = MaterialValueIndex.FromKey(idx);

            DrawName(key);
            Im.Line.Same(0, _spacing);
            DeleteButton(design, key, ref i);
            Im.Line.Same(0, _spacing);
            CopyButton(value.Value, value.Mode);
            Im.Line.Same(0, _spacing);
            PasteButton(design, key);
            Im.Line.Same(0, _spacing);
            using var disabled = Im.Disabled(design.WriteProtected());
            EnabledToggle(design, key, value.Enabled);
            Im.Line.Same(0, _spacing);
            DrawRow(design, key, value.Value, value.Revert, value.Mode);
            DrawRowExtra(design, key, value.Value, value.Revert, value.Mode, true);
            Im.Line.Same(0, _spacing);
            RevertToggle(design, key, value.Revert);
        }
    }

    private void DrawMultipleRow(Design design)
    {
        for (var i = 0; i < design.Materials.Count; ++i)
        {
            using var id = Im.Id.Push(i);
            var (idx, value) = design.Materials[i];
            var key = MaterialValueIndex.FromKey(idx);

            DrawName(key);
            Im.Line.Same(0, _spacing);
            DeleteButton(design, key, ref i);
            Im.Line.Same(0, _spacing);
            CopyButton(value.Value, value.Mode);
            Im.Line.Same(0, _spacing);
            PasteButton(design, key);
            Im.Line.Same(0, _spacing);
            using var disabled = Im.Disabled(design.WriteProtected());
            EnabledToggle(design, key, value.Enabled);


            DrawRow(design, key, value.Value, value.Revert, value.Mode);
            Im.Line.Same(0, _spacing);
            RevertToggle(design, key, value.Revert);
            DrawRowExtra(design, key, value.Value, value.Revert, value.Mode, false);
            Im.Separator();
        }
    }

    private void DeleteButton(Design design, MaterialValueIndex index, ref int idx)
    {
        var deleteEnabled = config.DeleteDesignModifier.IsActive();
        if (!ImEx.Icon.Button(LunaStyle.DeleteIcon,
                $"删除此行颜色集。{(deleteEnabled ? string.Empty : $"\n按住 {config.DeleteDesignModifier} 来删除。")}",
                !deleteEnabled || design.WriteProtected()))
            return;

        designManager.ChangeMaterialValue(design, index, null);
        --idx;
    }

    private void CopyButton(in ColorRow row, ColorRow.Mode mode)
    {
        if (ImEx.Icon.Button(LunaStyle.ToClipboardIcon, "将此行导出到剪贴板。"u8))
        {
            ColorRowClipboard.Row     = row;
            ColorRowClipboard.RowMode = mode;
        }
    }

    private void PasteButton(Design design, MaterialValueIndex index)
    {
        if (ImEx.Icon.Button(LunaStyle.FromClipboardIcon, "将导出的行从剪贴板导入到此行。"u8,
                !ColorRowClipboard.IsSet || design.WriteProtected()))
            designManager.ChangeMaterialValue(design, index, ColorRowClipboard.Row, ColorRowClipboard.RowMode);
    }

    private void EnabledToggle(Design design, MaterialValueIndex index, bool enabled)
    {
        if (Im.Checkbox("启用"u8, ref enabled))
            designManager.ChangeApplyMaterialValue(design, index, enabled);
    }

    private void RevertToggle(Design design, MaterialValueIndex index, bool revert)
    {
        if (Im.Checkbox("还原"u8, ref revert))
            designManager.ChangeMaterialRevert(design, index, revert);
        Im.Tooltip.OnHover(
            "如果选中此项，Glamourer 将尝试将此行高级染色恢复到其游戏状态，不应用此行。"u8);
    }

    private void ModeToggle(Design design, MaterialValueIndex index, ColorRow.Mode mode)
    {
        if (Im.Button(ToCallsignString(mode), ImEx.ScaledVectorX(ModeWidth)))
            designManager.ChangeMaterialMode(design, index, GetNextMode(mode));
        Im.Tooltip.OnHover(ToTooltipString(mode));

        return;

        static ReadOnlySpan<byte> ToCallsignString(ColorRow.Mode mode)
            => mode switch
            {
                ColorRow.Mode.Legacy    => "Lgc###mode"u8,
                ColorRow.Mode.Dawntrail => "DT###mode"u8,
                _                       => StringU8.Empty,
            };

        static ColorRow.Mode GetNextMode(ColorRow.Mode mode)
            => mode switch
            {
                ColorRow.Mode.Legacy    => ColorRow.Mode.Dawntrail,
                ColorRow.Mode.Dawntrail => ColorRow.Mode.Legacy,
                _                       => ColorRow.Mode.Dawntrail,
            };

        static ReadOnlySpan<byte> ToTooltipString(ColorRow.Mode mode)
            => mode switch
            {
                ColorRow.Mode.Legacy    => "此行颜色集目前包含 Legacy 材质参数。\n点击此按钮切换到 Dawntrail 材质参数。"u8,
                ColorRow.Mode.Dawntrail => "此行颜色集目前包含 Dawntrail 材质参数。\n点击此按钮切换到 Legacy 材质参数。"u8,
                _                       => StringU8.Empty,
            };
    }

    private void DrawSlotCombo()
    {
        var width = Im.Font.CalculateSize(EquipSlot.OffHand.ToNameU8()).X + Im.Style.FrameHeightWithSpacing;
        Im.Item.SetNextWidth(width);
        using (var combo = Im.Combo.Begin("##slot"u8, _newKey.SlotName()))
        {
            if (combo)
                foreach (var slot in MaterialValueIndex.AllSlots)
                {
                    if (Im.Selectable(slot.SlotName(), slot.SlotEquals(_newKey)) && !slot.SlotEquals(_newKey))
                        _newKey = slot with
                        {
                            MaterialIndex = (byte)_newMaterialIdx,
                            RowIndex = (byte)_newRowIdx,
                        };
                }
        }

        Im.Tooltip.OnHover("为高级染色选择一个装备类型。"u8);
    }

    public void DrawNew(Design design)
    {
        DrawSlotCombo();
        Im.Line.SameInner();
        DrawMaterialIdxDrag();
        Im.Line.SameInner();
        DrawRowIdxDrag();
        Im.Line.SameInner();
        var exists = design.GetMaterialDataRef().TryGetValue(_newKey, out _);
        if (ImEx.Button("添加一行"u8, Vector2.Zero,
                exists ? "所选高级染色已存在"u8 : "添加一行高级染色。"u8,
                exists || design.WriteProtected()))
            designManager.ChangeMaterialValue(design, _newKey, ColorRow.Empty);
    }

    private void DrawMaterialIdxDrag()
    {
        Im.Item.SetNextWidth(Im.Font.CalculateSize("材质 AA"u8).X);
        if (Im.Drag("##Material"u8, ref _newMaterialIdx, $"材质 {(char)('A' + _newMaterialIdx)}", 0, MaterialService.MaterialsPerModel - 1,
                0.01f, SliderFlags.NoInput))
        {
            _newMaterialIdx = Math.Clamp(_newMaterialIdx, 0, MaterialService.MaterialsPerModel - 1);
            _newKey         = _newKey with { MaterialIndex = (byte)_newMaterialIdx };
        }

        Im.Tooltip.OnHover("左右拖动以更改其值。"u8);
    }

    private void DrawRowIdxDrag()
    {
        Im.Item.SetNextWidth(Im.Font.CalculateSize("行 0000"u8).X);
        if (Im.Drag("##Row"u8, ref _newRowIdx, $"行 {_newRowIdx / 2 + 1}{(char)(_newRowIdx % 2 + 'A')}", 0, ColorTable.NumRows - 1, 0.01f,
                SliderFlags.NoInput))
        {
            _newRowIdx = Math.Clamp(_newRowIdx, 0, ColorTable.NumRows - 1);
            _newKey    = _newKey with { RowIndex = (byte)_newRowIdx };
        }

        Im.Tooltip.OnHover("左右拖动以更改其值。"u8);
    }

    private void DrawRow(Design design, MaterialValueIndex index, in ColorRow row, bool disabled, ColorRow.Mode mode)
    {
        var tmp = row;
        using var _ = Im.Disabled(disabled);
        var applied = ImEx.ColorPickerButton("##diffuse"u8, "更改此行的漫反射值。"u8, row.Diffuse, out tmp.Diffuse, 'D');
        Im.Line.SameInner();
        applied |= ImEx.ColorPickerButton("##specular"u8, "更改此行的镜面反射值。"u8, row.Specular, out tmp.Specular, 'S');
        Im.Line.SameInner();
        applied |= ImEx.ColorPickerButton("##emissive"u8, "更改此行的发光值。"u8, row.Emissive, out tmp.Emissive, 'E');
        Im.Line.SameInner();
        ModeToggle(design, index, mode);
        Im.Line.SameInner();
        Im.Item.SetNextWidthScaled(SliderWidth);
        var editAsRoughness = config.RoughnessSetting.Get(mode is ColorRow.Mode.Dawntrail);
        applied |= (mode, editAsRoughness) switch
        {
            (ColorRow.Mode.Legacy, false)    => AdvancedDyePopup.DragGloss(ref tmp.GlossStrength, true),
            (ColorRow.Mode.Legacy, true)     => AdvancedDyePopup.DragGlossAsRoughness(ref tmp.GlossStrength, true),
            (ColorRow.Mode.Dawntrail, false) => AdvancedDyePopup.DragRoughnessAsGloss(ref tmp.Roughness, true),
            (ColorRow.Mode.Dawntrail, true)  => AdvancedDyePopup.DragRoughness(ref tmp.Roughness, true),
            _                                => false,
        };
        Im.Tooltip.OnHover(editAsRoughness
            ? "更改此行的粗糙度。\n按住Ctrl并单击右键可取消。"u8
            : "更改此行的光泽强度。\n按住Ctrl并单击右键可取消。"u8);
        if (mode is ColorRow.Mode.Dawntrail)
        {
            Im.Line.SameInner();
            Im.Item.SetNextWidthScaled(SliderWidth);
            applied |= AdvancedDyePopup.DragMetalness(ref tmp.Metalness, true);
            Im.Tooltip.OnHover("更改此行的金属度。\n按住Ctrl并单击右键可取消。"u8);
        }
        else
        {
            Im.Line.SameInner();
            Im.Item.SetNextWidthScaled(SliderWidth);
            applied |= AdvancedDyePopup.DragSpecularStrength(ref tmp.SpecularStrength, true);
            Im.Tooltip.OnHover("更改此行的镜面反射强度。\n按住Ctrl并单击右键可取消。"u8);
        }

        if (applied)
            designManager.ChangeMaterialValue(design, index, tmp);
    }

    private void DrawRowExtra(Design design, MaterialValueIndex index, in ColorRow row, bool disabled, ColorRow.Mode mode, bool compact)
    {
        if (mode is not ColorRow.Mode.Dawntrail)
            return;

        var       tmp = row;
        using var _   = Im.Disabled(disabled);

        if (!compact)
            Im.Dummy(_buttonSize with { X = _buttonSize.X * 3 + _spacing * 2 });

        Im.Line.SameInner();
        Im.Item.SetNextWidthScaled(SheenSliderWidth);
        var applied = AdvancedDyePopup.DragSheen(ref tmp.Sheen, true);
        Im.Tooltip.OnHover("更改此行的光泽强度。\n按住Ctrl并单击右键可取消。"u8);

        Im.Line.SameInner();
        Im.Item.SetNextWidthScaled(SheenSliderWidth);
        applied |= AdvancedDyePopup.DragSheenTint(ref tmp.SheenTint, true);
        Im.Tooltip.OnHover("更改此行的光泽色调。\n按住Ctrl并单击右键可取消。"u8);

        Im.Line.SameInner();
        Im.Item.SetNextWidthScaled(SheenSliderWidth);
        applied |= AdvancedDyePopup.DragSheenRoughness(ref tmp.SheenAperture, true);
        Im.Tooltip.OnHover("更改此行的光泽粗糙度。\n按住Ctrl并单击右键可取消。"u8);

        if (!compact)
            Im.Dummy(_buttonSize with { X = _buttonSize.X * 3 + _spacing * 2 });

        var allItemsWidth = RowBaseWidth - Im.Style.ItemInnerSpacing.X;
        var itemWidth     = MathF.Floor(allItemsWidth / 4);

        Im.Line.SameInner();
        Im.Item.SetNextWidth(itemWidth);
        applied |= AdvancedDyePopup.DragExposure(ref tmp.Exposure, true);
        Im.Tooltip.OnHover("更改此行的曝光值。\n按住Ctrl并单击右键可取消。"u8);

        Im.Line.SameInner();
        Im.Item.SetNextWidth(itemWidth);
        using (Im.Style.Push(ImStyleSingle.Alpha, 0.5f * Im.Style.Alpha, (index.RowIndex & 1) is not 0))
        {
            applied |= AdvancedDyePopup.DragAnisotropy(ref tmp.Anisotropy, true);
        }

        Im.Tooltip.OnHover((index.RowIndex & 1) is 0
            ? "更改此行的各向异性程度。\n按住Ctrl并单击右键可取消。"u8
            : "更改此行的各向异性程度。\n除非使用着色器模组，否则这对 B 行没有效果。\n按住Ctrl并单击右键可取消。"u8);

        Im.Line.SameInner();
        Im.Item.SetNextWidth(itemWidth);
        applied |= AdvancedDyePopup.InputSphereMapIndex(textureArraySlicePickers,
            "更改此行的球体贴图。\n按住Ctrl并单击右键可取消。"u8, ref tmp.SphereMapIndex, true);

        Im.Line.SameInner();
        Im.Item.SetNextWidth(allItemsWidth - itemWidth * 3);
        applied |= AdvancedDyePopup.DragSphereMapMask(ref tmp.SphereMapMask, true);
        Im.Tooltip.OnHover("更改此行的球体贴图强度。\n按住Ctrl并单击右键可取消。"u8);

        if (applied)
            designManager.ChangeMaterialValue(design, index, tmp);
    }
}
