using Dalamud.Interface;
using Glamourer.Config;
using Glamourer.Interop.Penumbra;
using Glamourer.Services;
using ImSharp;
using Luna;
using Penumbra.Api.Enums;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Interop;

namespace Glamourer.Gui.Tabs.SettingsTab;

public class CollectionOverrideDrawer(
    CollectionOverrideService collectionOverrides,
    Configuration config,
    ActorObjectManager objects,
    ActorManager actors,
    PenumbraSubscriber penumbra,
    CollectionCombo combo) : IService
{
    private string            _newIdentifier = string.Empty;
    private ActorIdentifier[] _identifiers   = [];
    private int               _dragDropIndex = -1;
    private Exception?        _exception;

    public void Draw()
    {
        using var header = Im.Tree.HeaderId("合集覆盖"u8);
        Im.Tooltip.OnHover(
            "在这里，您可以为Penumbra合集设置覆盖，当自动应用来自设计的模组设置时，这些合集中的设置应该被更改。\n"u8
          + "与被覆盖角色相关的合集，而不是与被覆盖角色相关的合集。"u8);
        if (!header)
            return;

        using var table = Im.Table.Begin("table"u8, 4, TableFlags.RowBackground);
        if (!table)
            return;

        table.SetupColumn("buttons"u8,     TableColumnFlags.WidthFixed,   Im.Style.FrameHeight);
        table.SetupColumn("identifiers"u8, TableColumnFlags.WidthStretch, 0.35f);
        table.SetupColumn("collections"u8, TableColumnFlags.WidthStretch, 0.4f);
        table.SetupColumn("name"u8,        TableColumnFlags.WidthStretch, 0.25f);

        for (var i = 0; i < collectionOverrides.Overrides.Count; ++i)
            DrawCollectionRow(table, ref i);

        DrawNewOverride(table);
    }

    private void DrawCollectionRow(in Im.TableDisposable table, ref int idx)
    {
        using var id = Im.Id.Push(idx);
        var (exists, actor, collection, name) = collectionOverrides.Fetch(idx);

        table.NextColumn();
        if (ImEx.Icon.Button(LunaStyle.DeleteIcon, "删除此覆盖。"u8))
            collectionOverrides.DeleteOverride(idx--);

        table.NextColumn();
        DrawActorIdentifier(idx, actor);

        table.NextColumn();
        if (combo.Draw("##collection"u8, name, out var newName, ref collection, Im.ContentRegion.Available.X))
            collectionOverrides.ChangeOverride(idx, collection, newName);

        if (Im.Item.Hovered())
        {
            using var tt = Im.Tooltip.Begin();
            Im.Text("选择覆盖的合集。当前GUID："u8);
            using var indent = Im.Indent();
            ImEx.MonoText($"{collection}");
        }

        table.NextColumn();
        DrawCollectionName(exists, collection, name);
    }

    private void DrawCollectionName(bool exists, Guid collection, string name)
    {
        if (!exists)
        {
            Im.Text("<不存在>"u8);
            if (!Im.Item.Hovered())
                return;

            using var tt1 = Im.Tooltip.Begin();
            Im.Text($"设计 {name} 的 GUID");
            using (Im.Font.PushMono())
            {
                Im.Text($"    {collection}");
            }

            Im.Text("在 Penumbra 中不存在。"u8);
            return;
        }

        Im.Text(config.Ephemeral.IncognitoMode ? collection.ToString()[..8] : name);
        if (!Im.Item.Hovered())
            return;

        using var tt2 = Im.Tooltip.Begin();
        using var f   = Im.Font.PushMono();
        Im.Text($"{collection}");
    }

    private void DrawActorIdentifier(int idx, ActorIdentifier actor)
    {
        Im.Selectable(config.Ephemeral.IncognitoMode ? actor.Incognito(null) : actor.ToString());
        using (var target = Im.DragDrop.Target())
        {
            if (target.IsDropping("DraggingOverride"u8))
            {
                collectionOverrides.MoveOverride(_dragDropIndex, idx);
                _dragDropIndex = -1;
            }
        }

        using (var source = Im.DragDrop.Source())
        {
            if (source)
            {
                source.SetPayload("DraggingOverride"u8);
                Im.Text($"重新排序覆盖 #{idx + 1}...");
                _dragDropIndex = idx;
            }
        }
    }

    private void DrawNewOverride(in Im.TableDisposable table)
    {
        if (!penumbra.Available)
        {
            Im.Text("Not attached to Penumbra."u8);
            return;
        }

        var (currentId, currentName, _) = penumbra.Collections.TypeCollectionId(ApiCollectionType.Current)!.Value;
        table.NextColumn();
        if (ImEx.Icon.Button(FontAwesomeIcon.PersonCirclePlus.Icon(), "添加对当前玩家的覆盖。"u8,
                !objects.Player.Valid && currentId != Guid.Empty))
            collectionOverrides.AddOverride([objects.PlayerData.Identifier], currentId, currentName);

        table.NextColumn();
        Im.Item.SetNextWidthFull();
        if (Im.Input.Text("##newActor"u8, ref _newIdentifier, "新标识符..."u8))
            try
            {
                _identifiers = actors.FromUserString(_newIdentifier, false);
            }
            catch (ActorIdentifierFactory.IdentifierParseError e)
            {
                _exception   = e;
                _identifiers = [];
            }

        var tt = _identifiers.Any(i => i.IsValid)
            ? $"为 [{_identifiers.First(i => i.IsValid)}] 添加新的覆盖"
            : _newIdentifier.Length is 0
                ? "请先输入标识符字符串。"
                : $"标识符字符串 {_newIdentifier} 不是有效的标识符。{(_exception == null ? "." : $":\n\n{_exception?.Message}")}";

        table.NextColumn();
        if (ImEx.Icon.Button(LunaStyle.AddObjectIcon, tt, tt[0] is not 'A'))
            collectionOverrides.AddOverride(_identifiers, currentId, currentName);

        Im.Line.SameInner();
        ImEx.Icon.DrawAligned(LunaStyle.InfoIcon, ImGuiColor.TextDisabled.Get());
        if (Im.Item.Hovered())
            ActorIdentifierFactory.WriteUserStringTooltip(false);
    }
}
