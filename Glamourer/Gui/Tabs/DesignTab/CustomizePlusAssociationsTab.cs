using Glamourer.Config;
using Glamourer.Designs;
using Glamourer.Designs.CustomizePlus;
using Glamourer.Interop;
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
            "【国服特供】Customize+ 关联\n"u8
          + "● 此功能旨在提供轻量化的联动体验，仅在未启用 DynamicBridge 时生效。\n"u8
          + "● 生效机制：手动或自动应用此设计时，按下方模式应用关联的 Customize+ 配置。\n"u8
          + "● 临时配置：沿用现有临时应用方式，不修改普通配置开关。\n"u8
          + "● 永久配置：切换普通配置开关，可被同步插件同步，并会在不再匹配时尝试恢复应用前状态。\n"u8
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
        var canClear = config.DeleteDesignModifier.IsActive();
        if (ImEx.Icon.LabeledButton(LunaStyle.RefreshIcon, "##refreshCustomizePlusProfiles"u8, "刷新 Customize+ 列表。"u8))
            customizePlus.GetProfiles(true);

        Im.Line.SameInner();
        if (ImEx.Icon.LabeledButton(LunaStyle.DeleteIcon, "##clearCustomizePlusAssociation"u8,
                "移除此设计的 Customize+ 关联。"u8,
                !Selection.CustomizePlusAssociation.IsSet || !canClear))
        {
            manager.ClearCustomizePlusAssociation(Selection);
        }
        if (!canClear)
            Im.Tooltip.OnHover($"\n按住{config.DeleteDesignModifier}来删除。");

        Im.Line.SameInner();

        const float modeWidth = 116;
        var comboWidth = Math.Max(120, Im.ContentRegion.Available.X - modeWidth - Im.Style.ItemInnerSpacing.X);
        Im.Item.SetNextWidth(comboWidth);
        var preview = Selection.CustomizePlusAssociation.IsSet
            ? DisplayName(Selection.CustomizePlusAssociation)
            : "选择 Customize+ 角色配置...";
        using (Im.Disabled(bridgeLoaded || !customizePlus.IsAvailable(out _) || !hasPlayer))
        {
            using var combo = Im.Combo.Begin("##CustomizePlusProfile"u8, preview);
            if (combo)
            {
                Im.Item.SetNextWidthFull();
                ImEx.InputOnDeactivation.Text("##CustomizePlusFilter"u8, _filter, out _filter);
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
        if (association is { IsSet: true, Characters.Count: > 0 })
            Im.Tooltip.OnHover($"关联角色：{string.Join(", ", association.Characters.Select(FormatCharacter))}");

        Im.Line.SameInner();
        Im.Item.SetNextWidth(modeWidth);
        using (var modeCombo = Im.Combo.Begin("##CustomizePlusApplicationMode"u8, ModeName(Selection.CustomizePlusApplicationMode)))
        {
            if (modeCombo)
            {
                DrawModeSelectable(CustomizePlusApplicationMode.TemporaryProfile);
                DrawModeSelectable(CustomizePlusApplicationMode.PermanentProfile);
            }
        }
        Im.Tooltip.OnHover(ModeDescription(Selection.CustomizePlusApplicationMode));
    }

    private void DrawFooter(bool bridgeLoaded)
    {
        var currentPlayer = objects.PlayerData.Identifier;
        var hasPlayer = currentPlayer.IsValid;

        if (!hasPlayer)
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
                Im.Text("DynamicBridge 已加载，Glamourer 侧 Customize+ 关联已禁用。"u8);
        }

        if (!customizePlus.IsAvailable(out var reason))
        {
            using (ImGuiColor.Text.Push(ColorId.FolderLine.Value()))
                Im.Text($"{reason}");
        }
    }

    private static string DisplayName(CustomizePlusAssociation association)
        => association.ProfilePath.Length > 0 ? association.ProfilePath : association.ProfileName;

    private static string ModeName(CustomizePlusApplicationMode mode)
        => mode switch
        {
            CustomizePlusApplicationMode.PermanentProfile => "永久配置",
            _                                             => "临时配置",
        };

    private static string ModeDescription(CustomizePlusApplicationMode mode)
        => mode switch
        {
            CustomizePlusApplicationMode.PermanentProfile =>
                "切换 Customize+ 普通配置开关，可被 Mare 等同步插件同步。\n应用前会清理 Glamourer 创建的临时配置，并在不再匹配时尝试恢复应用前已启用的配置。",
            _ =>
                "使用 Customize+ 临时配置应用方式，不修改普通配置开关。\n这是旧设计的默认行为。",
        };

    private void DrawModeSelectable(CustomizePlusApplicationMode mode)
    {
        if (Im.Selectable($"{ModeName(mode)}##{mode}", Selection.CustomizePlusApplicationMode == mode))
            manager.ChangeCustomizePlusApplicationMode(Selection, mode);
        Im.Tooltip.OnHover(ModeDescription(mode));
    }

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
