using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Glamourer.Interop.Penumbra;
using Luna;
using Lumina.Excel.Sheets;
using Penumbra.Api.Wrappers;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Interop;
using Penumbra.GameData.Structs;

namespace Glamourer.MountReplacement;

public sealed unsafe class MountReplacementApplier : IRequiredService, IDisposable
{
    private const uint CompanyChocoboMountId = 1;

    private readonly MountReplacementManager _manager;
    private readonly PenumbraSubscriber      _penumbra;
    private readonly ActorManager            _actors;
    private readonly ActorObjectManager      _objects;
    private readonly IDataManager            _gameData;

    public MountReplacementApplier(MountReplacementManager manager, PenumbraSubscriber penumbra, ActorManager actors,
        ActorObjectManager objects, IDataManager gameData)
    {
        _manager  = manager;
        _penumbra = penumbra;
        _actors   = actors;
        _objects  = objects;
        _gameData = gameData;
        _penumbra.GameState.CreatingCharacterBase += OnCreatingCharacterBase;
        _manager.Changed                          += OnChanged;
    }

    public void Dispose()
    {
        _penumbra.GameState.CreatingCharacterBase -= OnCreatingCharacterBase;
        _manager.Changed                          -= OnChanged;
    }

    private void OnCreatingCharacterBase(in CreatingCharacterBaseArguments args)
    {
        if (!_manager.Enabled)
            return;

        var identifier = ((Actor)args.GameObject).GetIdentifier(_actors);
        if (!_manager.IsLocalPlayerMount(identifier))
            return;

        if (!_manager.TryGetTarget(identifier.DataId.Id, out var target))
            return;

        *(uint*)args.ModelId = target.ModelChara;
        var equip = (CharacterArmor*)args.EquipData;
        if (target.Id is CompanyChocoboMountId)
        {
            var (head, body, legs) = GetBarding();
            equip[0] = head;
            equip[1] = body;
            equip[3] = target.Legs.With((StainId)UIState.Instance()->Buddy.CompanionInfo.CurrentColorStainId);
            equip[4] = legs;
            return;
        }

        if (!target.HasEquipment
         && _manager.Mounts.TryGetValue(identifier.DataId.Id, out var source)
         && source.ModelChara == target.ModelChara)
            return;

        equip[0] = target.Head;
        equip[1] = target.Body;
        equip[3] = target.Legs;
        equip[4] = target.Feet;
    }

    private (CharacterArmor Head, CharacterArmor Body, CharacterArmor Legs) GetBarding()
    {
        var ids   = UIState.Instance()->Buddy.CompanionInfo.BuddyEquipRowIds;
        var sheet = _gameData.GetExcelSheet<BuddyEquip>();
        var head  = sheet.TryGetRow(ids[0], out var headRow) ? headRow.ModelTop : 0;
        var body  = sheet.TryGetRow(ids[1], out var bodyRow) ? bodyRow.ModelBody : 0;
        var legs  = sheet.TryGetRow(ids[2], out var legsRow) ? legsRow.ModelLegs : 0;
        return (MountReplacementManager.ToArmor(head), MountReplacementManager.ToArmor(body), MountReplacementManager.ToArmor(legs));
    }

    private void OnChanged()
    {
        var mount = _objects.Objects[1];
        if (mount.Valid && _manager.IsLocalPlayerMount(mount.GetIdentifier(_actors)))
            _penumbra.Redraw(mount.Index.Index);
    }
}
