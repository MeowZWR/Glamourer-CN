using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Glamourer.Config;
using Glamourer.Designs;
using Glamourer.Gui.Customization;
using Glamourer.Gui.Equipment;
using Glamourer.Gui.Tabs.DesignTab;
using Glamourer.Gui.Tabs.SettingsTab;
using Glamourer.Services;
using ImSharp;
using Luna;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Interop;

namespace Glamourer.Gui.Tabs.NpcTab;

public sealed class NpcPanel(
    Configuration config,
    NpcSelection selection,
    CustomizationDrawer customizeDrawer,
    EquipmentDrawer equipmentDrawer,
    ActorObjectManager objects,
    LocalNpcAppearanceData favorites,
    DesignColors designColors,
    DesignApplier designApplier) : IPanel
{
    private readonly DesignColorCombo _combo = new(designColors, true);

    public ReadOnlySpan<byte> Id
        => "NpcPanel"u8;

    public void Draw()
    {
        using var table = Im.Table.Begin("##Panel"u8, 1, TableFlags.None, Im.ContentRegion.Available);
        if (!table || !selection.HasSelection)
            return;

        table.SetupScrollFreeze(0, 1);
        table.NextColumn();
        Im.Dummy(Vector2.Zero);
        DrawButtonRow();

        table.NextColumn();
        DrawCustomization();
        DrawEquipment();
        DrawAppearanceInfo();
    }

    private void DrawButtonRow()
    {
        DrawApplyToSelf();
        Im.Line.Same();
        DrawApplyToTarget();
    }

    private void DrawCustomization()
    {
        if (config.HideDesignPanel.HasFlag(DesignPanelFlag.Customization))
            return;

        var expand = config.AutoExpandDesignPanel.HasFlag(DesignPanelFlag.Customization);
        using var h = Im.Tree.HeaderId(selection.Data.ModelId is 0
                ? "外貌###Customization"u8
                : $"外貌（模型ID #{selection.Data.ModelId}）###Customization",
            expand ? TreeNodeFlags.DefaultOpen : TreeNodeFlags.None);
        if (!h)
            return;

        customizeDrawer.Draw(selection.Data.Customize, true, true);
        Im.Dummy(new Vector2(Im.Style.TextHeight / 2));
    }

    private void DrawEquipment()
    {
        using var h = DesignPanelFlag.Equipment.Header(config);
        if (!h)
            return;

        equipmentDrawer.Prepare(false);
        var designData = selection.ToDesignData();

        foreach (var slot in EquipSlotExtensions.EqdpSlots)
        {
            var data = new EquipDrawData(slot, in designData) { Locked = true };
            equipmentDrawer.DrawEquip(data);
        }

        var mainhandData = new EquipDrawData(EquipSlot.MainHand, in designData) { Locked = true };
        var offhandData  = new EquipDrawData(EquipSlot.OffHand,  in designData) { Locked = true };
        equipmentDrawer.DrawWeapons(mainhandData, offhandData, false);

        Im.Dummy(new Vector2(Im.Style.TextHeight / 2));
        EquipmentDrawer.DrawMetaToggle(ToggleDrawData.FromValue(MetaIndex.VisorState, selection.Data.VisorToggled));
        Im.Dummy(new Vector2(Im.Style.TextHeight / 2));
    }

    private void DrawApplyToSelf()
    {
        var (id, data) = objects.PlayerData;
        var canApply = designApplier.CanApplyTo(id, data);
        var tt = canApply switch
        {
            DeniedApplicationReason.None =>
                "将当前NPC外观应用于你的角色。\n按住Ctrl仅应用装备。\n按住Shift仅应用外貌。"u8,
            DeniedApplicationReason.TargetInvalid or DeniedApplicationReason.TargetUnavailable => "你的角色不可用。"u8,
            _                                                                                  => ""u8,
        };

        if (ImEx.Button("应用到自己"u8, Vector2.Zero, tt, canApply is not DeniedApplicationReason.None))
            designApplier.ApplyTo(selection.ToDesignBase(), id, data, false);
    }

    private void DrawApplyToTarget()
    {
        var (id, data) = objects.TargetData;
        var canApply = designApplier.CanApplyTo(id, data);
        var tt = canApply switch
        {
            DeniedApplicationReason.None =>
                "将当前NPC外观应用于你的目标。\n按住Ctrl仅应用装备。\n按住Shift仅应用外貌。"u8,
            DeniedApplicationReason.TargetUnavailable => "当前目标无法操作。"u8,
            DeniedApplicationReason.TargetInvalid     => "未选择有效的目标。"u8,
            DeniedApplicationReason.TargetNonHuman    => "无法将设计应用于非人类对象。"u8,
            _                                         => ""u8,
        };
        if (ImEx.Button("应用到目标"u8, Vector2.Zero, tt, canApply is not DeniedApplicationReason.None))
            designApplier.ApplyTo(selection.ToDesignBase(), id, data, false);
    }

    private void DrawAppearanceInfo()
    {
        using var h = DesignPanelFlag.AppearanceDetails.Header(config);
        if (!h)
            return;

        using var table = Im.Table.Begin("详细信息"u8, 2);
        if (!table)
            return;

        using var style = ImStyleDouble.ButtonTextAlign.Push(new Vector2(0, 0.5f));
        table.SetupColumn("类型"u8, TableColumnFlags.WidthFixed, Im.Font.CalculateButtonSize("最后更新时间"u8).X);
        table.SetupColumn("数据"u8, TableColumnFlags.WidthStretch);

        CopyButton(table, "NPC 名称"u8,    selection.Name);
        CopyButton(table, "NPC ID"u8,      $"{selection.Data.Id.Id}");
        CopyButton(table, "NPC 名称 ID"u8, $"{selection.Data.NameId}");
        table.DrawFrameColumn("NPC 类型"u8);
        table.NextColumn();
        var width = Im.ContentRegion.Available.X;
        ImEx.TextFramed(selection.Data.Kind is ObjectKind.BattleNpc ? "战斗 NPC"u8 : "事件 NPC"u8, new Vector2(width, 0),
            ImGuiColor.FrameBackground.Get());

        table.DrawFrameColumn("配色"u8);
        table.NextColumn();
        var color = selection.ColorText;
        if (_combo.Draw("##colorCombo"u8, selection.ColorTextU8,
                "将颜色与此NPC外观相关联。\n"u8
              + "右键单击可恢复为自动配色。\n"u8
              + "按住Ctrl并滚动鼠标滚轮进行滚动选择。"u8,
                width - Im.Style.ItemInnerSpacing.X - Im.Style.FrameHeight, out var newColorText))
            favorites.SetColor(selection.Data, newColorText.Item == DesignColors.AutomaticName ? string.Empty : newColorText.Item);

        if (Im.Item.RightClicked())
        {
            favorites.SetColor(selection.Data, string.Empty);
            color = string.Empty;
        }

        if (designColors.TryGetValue(color, out var currentColor))
        {
            Im.Line.SameInner();
            if (DesignColorUi.DrawColorButton($"与 {color} 关联的颜色", currentColor, out var newColor))
                designColors.SetColor(color, newColor);
        }
        else if (color.Length is not 0)
        {
            Im.Line.SameInner();
            var size = new Vector2(Im.Style.FrameHeight);
            using (AwesomeIcon.Font.Push())
            {
                ImEx.TextFramed(LunaStyle.WarningIcon.Span, size, designColors.MissingColor);
            }

            Im.Tooltip.OnHover("与此设计相关联的颜色不存在。"u8);
        }

        return;

        static void CopyButton(in Im.TableDisposable table, ReadOnlySpan<byte> label, Utf8StringHandler<HintStringHandlerBuffer> text)
        {
            table.DrawFrameColumn(label);
            table.NextColumn();
            if (!text.GetSpan(out var span))
                return;

            if (Im.Button(span, Im.ContentRegion.Available with { Y = 0 }))
                Im.Clipboard.Set(span);
            Im.Tooltip.OnHover("点击复制到剪贴板。"u8);
        }
    }
}
