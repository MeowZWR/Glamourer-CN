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
    private readonly Dictionary<ActorIdentifier, PermanentState> _permanent = [];

    /// <summary> Actors where Glamourer last successfully applied a C+ profile from a manual design apply. </summary>
    private readonly HashSet<ActorIdentifier> _manualCustomizePlus = [];

    /// <summary> Remove every Customize+ profile change this plugin applied (e.g. on unload). </summary>
    public void RestoreAll()
    {
        foreach (var id in _applied.Keys.Concat(_permanent.Keys).Distinct().ToArray())
            Restore(id);
    }

    public void Dispose()
        => RestoreAll();

    public void Apply(ActorIdentifier identifier, in ObjectIndex objectIndex, DesignBase design)
        => Apply(identifier, objectIndex, design is Design d && d.ApplyCustomizePlusAssociation ? d.CustomizePlusAssociation : null,
            design is Design d2 ? d2.CustomizePlusApplicationMode : CustomizePlusApplicationMode.TemporaryProfile, StateSource.Manual,
            false, false);

    public void Apply(ActorIdentifier identifier, in ObjectIndex objectIndex, MergedDesign design, StateSource applySource,
        bool respectManual, bool stateHasManualGlamourerSource)
        => Apply(identifier, objectIndex, design.ApplyCustomizePlusAssociation ? design.CustomizePlusAssociation : null,
            design.CustomizePlusApplicationMode, applySource, respectManual, stateHasManualGlamourerSource);

    public void Restore(ActorIdentifier identifier)
    {
        _manualCustomizePlus.Remove(identifier);
        RestoreTemporary(identifier);
        RestorePermanent(identifier);
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
        CustomizePlusApplicationMode mode, StateSource applySource, bool respectManual, bool stateHasManualGlamourerSource)
    {
        if (association is { IsSet: true } && respectManual && applySource.IsFixed()
         && (_manualCustomizePlus.Contains(identifier) || stateHasManualGlamourerSource))
            return;

        if (dynamicBridge.IsLoaded || !customizePlus.IsAvailable(out _))
        {
            Restore(identifier, objectIndex);
            return;
        }

        if (association is not { IsSet: true })
        {
            if (respectManual && applySource.IsFixed() && _manualCustomizePlus.Contains(identifier))
                return;

            Restore(identifier, objectIndex);
            return;
        }

        if (!CustomizePlusIpcService.Matches(identifier, association))
        {
            Restore(identifier, objectIndex);
            return;
        }

        if (mode is CustomizePlusApplicationMode.PermanentProfile)
            ApplyPermanent(identifier, association, applySource);
        else
            ApplyTemporary(identifier, objectIndex, association, applySource);
    }

    private void ApplyTemporary(ActorIdentifier identifier, in ObjectIndex objectIndex, CustomizePlusAssociation association,
        StateSource applySource)
    {
        RestorePermanent(identifier);

        if (_applied.TryGetValue(identifier, out var existing) && existing.SourceProfileId == association.ProfileId)
        {
            SetManualState(identifier, applySource);
            return;
        }

        if (!customizePlus.TryGetProfileJson(association.ProfileId, out var profileJson, out var readError))
        {
            if (readError.Length > 0)
                Glamourer.Log.Warning($"Failed to load Customize+ profile {association.ProfileId} for {identifier.Incognito(null)}: {readError}");
            Restore(identifier, objectIndex);
            return;
        }

        RestoreTemporary(identifier);
        if (!customizePlus.TrySetTemporaryProfile(objectIndex, profileJson, out var temporaryId, out var applyError))
        {
            if (applyError.Length > 0)
                Glamourer.Log.Warning($"Failed to apply Customize+ profile {association.ProfileId} for {identifier.Incognito(null)}: {applyError}");
            return;
        }

        _applied[identifier] = (association.ProfileId, temporaryId);
        SetManualState(identifier, applySource);
    }

    private void ApplyPermanent(ActorIdentifier identifier, CustomizePlusAssociation association, StateSource applySource)
    {
        RestoreTemporary(identifier);

        if (_permanent.TryGetValue(identifier, out var existing) && existing.SourceProfileId == association.ProfileId)
        {
            SetManualState(identifier, applySource);
            return;
        }

        if (!customizePlus.TryGetProfileIds(identifier, out var profileIds, out var enabledProfileIds, out var readError))
        {
            if (readError.Length > 0)
                Glamourer.Log.Warning($"Failed to read Customize+ profiles for {identifier.Incognito(null)}: {readError}");
            RestorePermanent(identifier);
            return;
        }

        var savedProfileIds = _permanent.TryGetValue(identifier, out existing)
            ? existing.SavedProfileIds
            : enabledProfileIds.ToArray();
        _permanent[identifier] = new PermanentState(association.ProfileId, association.ProfileId, savedProfileIds);

        foreach (var profileId in profileIds)
        {
            if (!customizePlus.TrySetProfileEnabled(profileId, false, out var disableError) && disableError.Length > 0)
                Glamourer.Log.Warning(
                    $"Failed to disable Customize+ profile {profileId} for {identifier.Incognito(null)}: {disableError}");
        }

        if (!customizePlus.TrySetProfileEnabled(association.ProfileId, true, out var applyError))
        {
            if (applyError.Length > 0)
                Glamourer.Log.Warning(
                    $"Failed to enable Customize+ profile {association.ProfileId} for {identifier.Incognito(null)}: {applyError}");
            RestorePermanent(identifier);
            return;
        }

        SetManualState(identifier, applySource);
    }

    private void RestoreTemporary(ActorIdentifier identifier)
    {
        if (!_applied.Remove(identifier, out var state))
            return;

        if (!customizePlus.TryDeleteTemporaryProfile(state.TemporaryProfileId, out var error) && error.Length > 0)
            Glamourer.Log.Warning($"Failed to restore Customize+ temporary state for {identifier.Incognito(null)}: {error}");
    }

    private void RestorePermanent(ActorIdentifier identifier)
    {
        if (!_permanent.Remove(identifier, out var state))
            return;

        if (!customizePlus.TrySetProfileEnabled(state.LastEnabledProfileId, false, out var disableError) && disableError.Length > 0)
            Glamourer.Log.Warning(
                $"Failed to disable Customize+ profile {state.LastEnabledProfileId} for {identifier.Incognito(null)}: {disableError}");

        foreach (var profileId in state.SavedProfileIds)
        {
            if (!customizePlus.TrySetProfileEnabled(profileId, true, out var enableError) && enableError.Length > 0)
                Glamourer.Log.Warning(
                    $"Failed to restore Customize+ profile {profileId} for {identifier.Incognito(null)}: {enableError}");
        }
    }

    private void SetManualState(ActorIdentifier identifier, StateSource applySource)
    {
        if (applySource.IsManual())
            _manualCustomizePlus.Add(identifier);
        else
            _manualCustomizePlus.Remove(identifier);
    }

    private sealed record PermanentState(Guid SourceProfileId, Guid LastEnabledProfileId, Guid[] SavedProfileIds);
}
