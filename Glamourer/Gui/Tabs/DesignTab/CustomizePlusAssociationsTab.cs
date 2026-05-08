using Glamourer.Config;
using Glamourer.Designs;
using Glamourer.Designs.CustomizePlus;
using Glamourer.Interop.CustomizePlus;
using ImSharp;
using Luna;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Interop;
using Penumbra.GameData.Structs;

namespace Glamourer.Gui.Tabs.DesignTab;

public sealed class CustomizePlusAssociationsTab(
    DesignFileSystem fileSystem,
    DesignManager manager,
    Configuration config,
    CustomizePlusIpcService customizePlus,
    DynamicBridgeGate dynamicBridge,
    ActorObjectManager objects) : IUiService
{
    private readonly CustomizePlusApplicationModeButton _customizePlusModeButton = new(fileSystem, manager);

    private string _filter = string.Empty;

    private Design Selection
        => (Design)fileSystem.Selection.Selection!.Value;

    public void Draw()
    {
        var bridgeLoaded = dynamicBridge.IsLoaded;
        using var h = DesignPanelFlag.CustomizePlusAssociations.Header(config);
        if (!h.Alive)
            return;

        Im.Tooltip.OnHover(
            "「国服特供」Customize+ 关联\n"u8
          + "● 此功能旨在提供轻量化的联动体验，仅在未启用 DynamicBridge 时生效。\n"u8
          + "● 只适用于 C+ 角色配置中按角色名称分配的配置。不支持使用「应用于您登陆的任何角色」类型的配置。\n"u8
          + "● 生效机制：手动或自动应用此设计时，按下方模式应用关联的 Customize+ 配置。\n"u8
          + "● 临时配置：不会修改你在 Customize+ 中的选择，但可能不会被 Mare 同步。\n"u8
          + "● 正常配置：类似 DynamicBridge 的应用方式。\n"u8
          + "※ 请根据实际需求谨慎开启，可能会损害你的配置，请做好备份。"u8);
        if (!h)
            return;

        DrawToolbarAndCombo(bridgeLoaded);
        Im.Separator();
        DrawFooter(bridgeLoaded);
    }

    private void DrawToolbarAndCombo(bool bridgeLoaded)
    {
        var currentPlayer = objects.PlayerData.Identifier;
        var hasPlayer = currentPlayer.IsValid;
        if (ImEx.Icon.LabeledButton(LunaStyle.RefreshIcon, "##refreshCustomizePlusProfiles"u8, "刷新 Customize+ 列表。"u8))
            RefreshCustomizePlusProfiles();

        Im.Line.SameInner();
        if (ImEx.Icon.LabeledButton(LunaStyle.DeleteIcon, "##clearCustomizePlusAssociation"u8,
                "移除此设计的 Customize+ 关联。"u8,
                !Selection.CustomizePlusAssociation.IsSet))
        {
            manager.ClearCustomizePlusAssociation(Selection);
        }

        Im.Line.SameInner();

        var modeWidth  = Im.Style.FrameHeight;
        var comboWidth = Math.Max(120, Im.ContentRegion.Available.X - modeWidth - Im.Style.ItemInnerSpacing.X);
        Im.Item.SetNextWidth(comboWidth);
        var preview = CustomizePlusAssociationComboPreview();
        using (Im.Disabled(bridgeLoaded || !customizePlus.IsAvailable(out _) || !hasPlayer))
        {
            using var combo = Im.Combo.Begin("##CustomizePlusProfile"u8, preview);
            if (combo)
            {
                Im.Item.SetNextWidthFull();
                Im.Input.Text("##CustomizePlusFilter"u8, ref _filter, "筛选名称或路径..."u8);
                foreach (var profile in customizePlus.GetProfiles())
                {
                    if (_filter.Length > 0
                     && !DisplayName(profile).Contains(_filter, StringComparison.OrdinalIgnoreCase)
                     && !profile.ProfileName.Contains(_filter, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var selected = profile.ProfileId == Selection.CustomizePlusAssociation.ProfileId;
                    var matchesCurrent = hasPlayer && CustomizePlusIpcService.Matches(currentPlayer, profile);
                    using var itemDisabled = Im.Disabled(!matchesCurrent);
                    var selectableLabel = matchesCurrent
                        ? $"{DisplayName(profile)}##{profile.ProfileId}"
                        : $"{DisplayName(profile)}（不可用于当前角色）##{profile.ProfileId}";
                    using (ImGuiColor.Text.Push(ColorId.ActorUnavailable.Value(), !matchesCurrent))
                    {
                        if (Im.Selectable(selectableLabel, selected) && matchesCurrent)
                        {
                            manager.ChangeCustomizePlusAssociation(Selection, profile);
                            manager.ChangeApplyCustomizePlusAssociation(Selection, true);
                        }
                    }

                    if (Im.Item.Hovered())
                    {
                        using var tooltip = Im.Tooltip.Begin();
                        Im.Text($"名称：{profile.ProfileName}");
                        if (profile.ProfilePath.Length > 0)
                            Im.Text($"路径：{profile.ProfilePath}");
                        Im.Text("可用于当前角色。"u8);
                        if (profile.Characters.Count > 0)
                        {
                            Im.Separator();
                            foreach (var character in profile.Characters)
                                Im.Text(FormatCharacter(character));
                        }
                    }
                }
            }
        }

        var association = Selection.CustomizePlusAssociation;
        if (association.IsSet)
        {
            if (AssociationMissingFromCustomizePlusList(association))
                Im.Tooltip.OnHover("未在 Customize+ 角色配置中找到此关联配置（可能已被删除）。"u8);
            else if (association.Characters.Count > 0)
                Im.Tooltip.OnHover($"关联角色：{string.Join(", ", association.Characters.Select(FormatCharacter))}");
        }

        Im.Line.SameInner();
        _customizePlusModeButton.DrawButton(default);
    }

    private void DrawFooter(bool bridgeLoaded)
    {
        var currentPlayer = objects.PlayerData.Identifier;
        var hasPlayer = currentPlayer.IsValid;

        if (Selection.CustomizePlusAssociation.IsSet
         && AssociationMissingFromCustomizePlusList(Selection.CustomizePlusAssociation))
        {
            using (ImGuiColor.Text.Push(ColorId.ActorUnavailable.Value()))
                Im.Text("未在 Customize+ 角色配置中找到此关联配置（可能已被删除）。"u8);
        }
        else if (!hasPlayer)
            Im.Text("当前玩家不可用，无法校验角色匹配。"u8);
        else if (Selection.CustomizePlusAssociation.IsSet)
        {
            var matches = CustomizePlusIpcService.Matches(currentPlayer, Selection.CustomizePlusAssociation);
            using (ImGuiColor.Text.Push(matches ? ColorId.ActorAvailable.Value() : ColorId.ActorUnavailable.Value()))
                Im.Text(matches
                    ? "此设计关联的 Customize+ 配置可对当前角色生效。"u8
                    : "此设计关联的 Customize+ 配置不会对当前角色生效。"u8);
        }

        if (bridgeLoaded)
        {
            using (ImGuiColor.Text.Push(ColorId.FolderLine.Value()))
                Im.Text("DynamicBridge 已加载，此功能已被禁用。"u8);
        }

        if (!customizePlus.IsAvailable(out var reason))
        {
            using (ImGuiColor.Text.Push(ColorId.FolderLine.Value()))
                Im.Text($"{reason}");
        }
    }

    private void RefreshCustomizePlusProfiles()
    {
        var profiles = customizePlus.GetProfiles(true);
        var stored   = Selection.CustomizePlusAssociation;
        if (!stored.IsSet)
            return;

        foreach (var profile in profiles)
        {
            if (profile.ProfileId != stored.ProfileId)
                continue;
            manager.ChangeCustomizePlusAssociation(Selection, profile);
            break;
        }
    }

    private string CustomizePlusAssociationComboPreview()
    {
        var assoc = Selection.CustomizePlusAssociation;
        if (!assoc.IsSet)
            return "选择 Customize+ 角色配置...";

        var name = DisplayName(assoc);
        return AssociationMissingFromCustomizePlusList(assoc) ? $"{name}（此配置已失效）" : name;
    }

    private bool AssociationMissingFromCustomizePlusList(in CustomizePlusAssociation assoc)
    {
        if (!assoc.IsSet || !customizePlus.IsAvailable(out _))
            return false;

        foreach (var profile in customizePlus.GetProfiles())
        {
            if (profile.ProfileId == assoc.ProfileId)
                return false;
        }

        return true;
    }

    private static string DisplayName(CustomizePlusAssociation association)
        => association.ProfilePath.Length > 0 ? association.ProfilePath : association.ProfileName;

    private string FormatCharacter(CustomizePlusCharacterAssociation character)
        => ((IdentifierType)character.CharacterType) switch
        {
            IdentifierType.Player   => $"{character.Name} ({objects.Actors.Data.ToWorldName(new WorldId(character.WorldId))})",
            IdentifierType.Retainer => $"{character.Name} [雇员]",
            IdentifierType.Owned    => $"{character.Name} [所属NPC]",
            IdentifierType.Npc      => $"{character.Name} [NPC]",
            _                       => character.Name,
        };
}
