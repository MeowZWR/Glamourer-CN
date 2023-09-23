using System;
using System.Numerics;
using Dalamud.Interface;
using Glamourer.Services;
using ImGuiNET;
using Lumina.Misc;
using OtterGui;
using OtterGui.Raii;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;

namespace Glamourer.Gui;

[Flags]
public enum DataChange : byte
{
    None        = 0x00,
    Item        = 0x01,
    Stain       = 0x02,
    ApplyItem   = 0x04,
    ApplyStain  = 0x08,
    Item2       = 0x10,
    Stain2      = 0x20,
    ApplyItem2  = 0x40,
    ApplyStain2 = 0x80,
}

public static class UiHelpers
{
    /// <summary> Open a combo popup with another method than the combo itself. </summary>
    public static void OpenCombo(string comboLabel)
    {
        var windowId = ImGui.GetID(comboLabel);
        var popupId  = ~Crc32.Get("##ComboPopup", windowId);
        ImGui.OpenPopup(popupId);
    }

    public static void DrawIcon(this EquipItem item, TextureService textures, Vector2 size, EquipSlot slot)
    {
        var isEmpty = item.ModelId.Id == 0;
        var (ptr, textureSize, empty) = textures.GetIcon(item, slot);
        if (empty)
        {
            var (bgColor, tint) = isEmpty
                ? (ImGui.GetColorU32(ImGuiCol.FrameBg), new Vector4(0.1f,       0.1f, 0.1f, 0.5f))
                : (ImGui.GetColorU32(ImGuiCol.FrameBgActive), new Vector4(0.3f, 0.3f, 0.3f, 0.8f));
            var pos = ImGui.GetCursorScreenPos();
            ImGui.GetWindowDrawList().AddRectFilled(pos, pos + size, bgColor, 5 * ImGuiHelpers.GlobalScale);
            if (ptr != nint.Zero)
                ImGui.Image(ptr, size, Vector2.Zero, Vector2.One, tint);
            else
                ImGui.Dummy(size);
        }
        else
        {
            ImGuiUtil.HoverIcon(ptr, textureSize, size);
        }
    }

    public static bool DrawCheckbox(string label, string tooltip, bool value, out bool on, bool locked)
    {
        using var disabled = ImRaii.Disabled(locked);
        var       ret      = ImGuiUtil.Checkbox(label, string.Empty, value, v => value = v);
        ImGuiUtil.HoverTooltip(tooltip);
        on = value;
        return ret;
    }

    public static DataChange DrawMetaToggle(string label, string tooltip, bool currentValue, bool currentApply, out bool newValue,
        out bool newApply,
        bool locked)
    {
        var       flags = currentApply ? currentValue ? 3 : 0 : 2;
        bool      ret;
        using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, ImGui.GetStyle().ItemInnerSpacing);
        using (var disabled = ImRaii.Disabled(locked))
        {
            ret = ImGui.CheckboxFlags("##" + label, ref flags, 3);
        }

        ImGuiUtil.HoverTooltip(tooltip);

        ImGui.SameLine();
        ImGui.TextUnformatted(label);

        if (ret)
        {
            (newValue, newApply, var change) = (currentValue, currentApply) switch
            {
                (false, false) => (false, true, DataChange.ApplyItem),
                (false, true)  => (true, true, DataChange.Item),
                (true, false)  => (false, false, DataChange.Item), // Should not happen
                (true, true)   => (false, false, DataChange.Item | DataChange.ApplyItem),
            };
            return change;
        }

        newValue = currentValue;
        newApply = currentApply;
        return DataChange.None;
    }
}
