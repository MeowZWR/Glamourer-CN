using Glamourer.Designs;
using Glamourer.Designs.CustomizePlus;
using Glamourer.Designs.Links;
using Glamourer.State;
using Luna;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Interop;
using Penumbra.GameData.Structs;

namespace Glamourer.Interop.CustomizePlus;

public sealed class CustomizePlusAssociationApplier(
    CustomizePlusIpcService customizePlus,
    DynamicBridgeGate dynamicBridge,
    ActorObjectManager objects) : IService, IDisposable
{
    private readonly Dictionary<ActorIdentifier, (Guid SourceProfileId, Guid TemporaryProfileId)> _applied = [];

    /// <summary> Actors where Glamourer last successfully applied a temporary C+ profile from a manual design apply. </summary>
    private readonly HashSet<ActorIdentifier> _manualTemporaryCustomizePlus = [];

    /// <summary> Remove every temporary Customize+ profile this plugin applied (e.g. on unload). </summary>
    public void RestoreAll()
    {
        foreach (var id in new List<ActorIdentifier>(_applied.Keys))
            Restore(id);
    }

    public void Dispose()
        => RestoreAll();

    public void Apply(ActorIdentifier identifier, in ObjectIndex objectIndex, DesignBase design)
        => Apply(identifier, objectIndex, design is Design d && d.ApplyCustomizePlusAssociation ? d.CustomizePlusAssociation : null,
            StateSource.Manual, false);

    public void Apply(ActorIdentifier identifier, in ObjectIndex objectIndex, MergedDesign design, StateSource applySource,
        bool respectManual)
        => Apply(identifier, objectIndex, design.ApplyCustomizePlusAssociation ? design.CustomizePlusAssociation : null, applySource,
            respectManual);

    public void Restore(ActorIdentifier identifier)
    {
        _manualTemporaryCustomizePlus.Remove(identifier);
        if (!_applied.Remove(identifier, out var state))
            return;

        if (!customizePlus.TryDeleteTemporaryProfile(state.TemporaryProfileId, out var error) && error.Length > 0)
            Glamourer.Log.Warning($"Failed to restore Customize+ state for {identifier.Incognito(null)}: {error}");
    }

    public void Restore(ActorIdentifier identifier, in ObjectIndex _)
        => Restore(identifier);

    public void RestoreUnavailable(ActorIdentifier identifier)
    {
        if (!objects.TryGetValue(identifier, out var data) || !data.Valid)
        {
            Restore(identifier);
            return;
        }

        Restore(identifier, data.Objects[0].Index);
    }

    private void Apply(ActorIdentifier identifier, in ObjectIndex objectIndex, CustomizePlusAssociation? association,
        StateSource applySource, bool respectManual)
    {
        if (respectManual && applySource.IsFixed() && _manualTemporaryCustomizePlus.Contains(identifier))
            return;

        if (dynamicBridge.IsLoaded || association is not { IsSet: true } || !customizePlus.IsAvailable(out _)
         || !CustomizePlusIpcService.Matches(identifier, association))
        {
            Restore(identifier, objectIndex);
            return;
        }

        if (_applied.TryGetValue(identifier, out var existing) && existing.SourceProfileId == association.ProfileId)
        {
            if (applySource.IsManual())
                _manualTemporaryCustomizePlus.Add(identifier);
            else
                _manualTemporaryCustomizePlus.Remove(identifier);
            return;
        }

        if (!customizePlus.TryGetProfileJson(association.ProfileId, out var profileJson, out var readError))
        {
            if (readError.Length > 0)
                Glamourer.Log.Warning($"Failed to load Customize+ profile {association.ProfileId} for {identifier.Incognito(null)}: {readError}");
            Restore(identifier, objectIndex);
            return;
        }

        Restore(identifier, objectIndex);
        if (!customizePlus.TrySetTemporaryProfile(objectIndex, profileJson, out var temporaryId, out var applyError))
        {
            if (applyError.Length > 0)
                Glamourer.Log.Warning($"Failed to apply Customize+ profile {association.ProfileId} for {identifier.Incognito(null)}: {applyError}");
            return;
        }

        _applied[identifier] = (association.ProfileId, temporaryId);
        if (applySource.IsManual())
            _manualTemporaryCustomizePlus.Add(identifier);
        else
            _manualTemporaryCustomizePlus.Remove(identifier);
    }
}
