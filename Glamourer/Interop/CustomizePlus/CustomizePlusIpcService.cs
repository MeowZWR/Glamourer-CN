using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;
using Glamourer.Designs.CustomizePlus;
using Luna;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;

using CustomizePlusCharacterTuple = (string Name, ushort WorldId, byte CharacterType, ushort CharacterSubType);
using CustomizePlusProfileTuple = (System.Guid UniqueId, string Name, string VirtualPath, System.Collections.Generic.List<(string Name, ushort WorldId, byte CharacterType, ushort CharacterSubType)> Characters, int Priority, bool IsEnabled);

namespace Glamourer.Interop.CustomizePlus;

internal enum CustomizePlusErrorCode
{
    Success = 0,
    InvalidCharacter = 1,
    CorruptedProfile = 2,
    ProfileNotFound = 3,
    InvalidArgument = 4,
    UnknownError = 5,
}

public sealed class DynamicBridgeGate(IDalamudPluginInterface pluginInterface) : IService
{
    public bool IsLoaded
        => pluginInterface.InstalledPlugins.Any(plugin => plugin.InternalName == "DynamicBridge" && plugin.IsLoaded);
}

public sealed class CustomizePlusIpcService : IService
{
    public const int RequiredBreakingVersion = 6;

    private static readonly TimeSpan ProfileCacheDuration = TimeSpan.FromSeconds(5);

    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly ICallGateSubscriber<(int Breaking, int Feature)> _getApiVersion;
    private readonly ICallGateSubscriber<bool> _isValid;
    private readonly ICallGateSubscriber<IList<CustomizePlusProfileTuple>> _getProfileList;
    private readonly ICallGateSubscriber<Guid, (int ErrorCode, string? Json)> _getProfileByUniqueId;
    private readonly ICallGateSubscriber<ushort, (int ErrorCode, Guid? ProfileId)> _getActiveProfileIdOnCharacter;
    private readonly ICallGateSubscriber<ushort, string, (int ErrorCode, Guid? ProfileId)> _setTemporaryProfileOnCharacter;
    private readonly ICallGateSubscriber<Guid, int> _deleteTemporaryProfileByUniqueId;
    private readonly ICallGateSubscriber<Guid, int> _enableProfileByUniqueId;
    private readonly ICallGateSubscriber<Guid, int> _disableProfileByUniqueId;

    private DateTime _lastProfileCache = DateTime.MinValue;
    private IReadOnlyList<CustomizePlusProfileTuple> _cachedRawProfiles = [];
    private IReadOnlyList<CustomizePlusAssociation> _cachedProfiles = [];

    public CustomizePlusIpcService(IDalamudPluginInterface pluginInterface)
    {
        _pluginInterface = pluginInterface;
        _getApiVersion = pluginInterface.GetIpcSubscriber<(int Breaking, int Feature)>("CustomizePlus.General.GetApiVersion");
        _isValid = pluginInterface.GetIpcSubscriber<bool>("CustomizePlus.General.IsValid");
        _getProfileList = pluginInterface.GetIpcSubscriber<IList<CustomizePlusProfileTuple>>("CustomizePlus.Profile.GetList");
        _getProfileByUniqueId = pluginInterface.GetIpcSubscriber<Guid, (int ErrorCode, string? Json)>("CustomizePlus.Profile.GetByUniqueId");
        _getActiveProfileIdOnCharacter = pluginInterface.GetIpcSubscriber<ushort, (int ErrorCode, Guid? ProfileId)>("CustomizePlus.Profile.GetActiveProfileIdOnCharacter");
        _setTemporaryProfileOnCharacter =
            pluginInterface.GetIpcSubscriber<ushort, string, (int ErrorCode, Guid? ProfileId)>("CustomizePlus.Profile.SetTemporaryProfileOnCharacter");
        _deleteTemporaryProfileByUniqueId =
            pluginInterface.GetIpcSubscriber<Guid, int>("CustomizePlus.Profile.DeleteTemporaryProfileByUniqueId");
        _enableProfileByUniqueId = pluginInterface.GetIpcSubscriber<Guid, int>("CustomizePlus.Profile.EnableByUniqueId");
        _disableProfileByUniqueId = pluginInterface.GetIpcSubscriber<Guid, int>("CustomizePlus.Profile.DisableByUniqueId");
    }

    public bool IsLoaded
        => _pluginInterface.InstalledPlugins.Any(plugin => plugin.InternalName == "CustomizePlus" && plugin.IsLoaded);

    public bool IsAvailable(out string reason)
    {
        if (!IsLoaded)
        {
            reason = "Customize+ 未加载。";
            return false;
        }

        try
        {
            var version = _getApiVersion.InvokeFunc();
            if (version.Breaking != RequiredBreakingVersion)
            {
                reason = $"Customize+ IPC 主版本不兼容，当前为 {version.Breaking}.{version.Feature}。";
                return false;
            }

            if (!_isValid.InvokeFunc())
            {
                reason = "Customize+ 当前状态不可用。";
                return false;
            }
        }
        catch (IpcNotReadyError)
        {
            reason = "Customize+ IPC 尚未就绪。";
            return false;
        }
        catch (IpcError ex)
        {
            reason = $"Customize+ IPC 调用失败: {ex.Message}";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public IReadOnlyList<CustomizePlusAssociation> GetProfiles(bool forceRefresh = false)
    {
        if (!RefreshProfileCache(forceRefresh, out _))
            return _cachedProfiles;

        return _cachedProfiles;
    }

    private bool RefreshProfileCache(bool forceRefresh, out string error)
    {
        if (!forceRefresh && DateTime.UtcNow - _lastProfileCache <= ProfileCacheDuration)
        {
            error = string.Empty;
            return true;
        }

        if (!IsAvailable(out error))
            return false;

        try
        {
            _cachedRawProfiles = _getProfileList.InvokeFunc().ToArray();
            _cachedProfiles = _cachedRawProfiles
                .Select(profile =>
                {
                    var association = new CustomizePlusAssociation();
                    association.Update(profile.UniqueId, profile.Name, profile.VirtualPath,
                        profile.Characters.Select(character =>
                            new CustomizePlusCharacterAssociation(character.Name, character.WorldId, character.CharacterType,
                                character.CharacterSubType)));
                    return association;
                })
                .ToArray();
            _lastProfileCache = DateTime.UtcNow;
            error = string.Empty;
            return true;
        }
        catch (IpcError ex)
        {
            error = $"刷新 Customize+ 配置列表失败: {ex.Message}";
            Glamourer.Log.Warning(error);
            return false;
        }
    }

    public bool TryGetProfile(Guid profileId, out CustomizePlusAssociation association, bool forceRefresh = false)
    {
        association = GetProfiles(forceRefresh).FirstOrDefault(profile => profile.ProfileId == profileId)?.Clone() ?? new CustomizePlusAssociation();
        return association.IsSet;
    }

    public bool TryGetProfileJson(Guid profileId, out string profileJson, out string error)
    {
        profileJson = string.Empty;
        if (!IsAvailable(out error))
            return false;

        try
        {
            var result = _getProfileByUniqueId.InvokeFunc(profileId);
            if ((CustomizePlusErrorCode)result.ErrorCode is not CustomizePlusErrorCode.Success || result.Json is null)
            {
                error = $"读取 Customize+ 配置失败: {(CustomizePlusErrorCode)result.ErrorCode}.";
                return false;
            }

            profileJson = result.Json;
            error = string.Empty;
            return true;
        }
        catch (IpcError ex)
        {
            error = $"读取 Customize+ 配置失败: {ex.Message}";
            return false;
        }
    }

    public bool TryGetProfileIds(ActorIdentifier identifier, out IReadOnlyList<Guid> profileIds,
        out IReadOnlyList<Guid> enabledProfileIds, out string error)
    {
        profileIds = [];
        enabledProfileIds = [];
        if (!RefreshProfileCache(true, out error))
            return false;

        var profiles = _cachedRawProfiles.Where(profile => Matches(identifier, profile.Characters)).ToArray();
        profileIds = profiles.Select(profile => profile.UniqueId).ToArray();
        enabledProfileIds = profiles.Where(profile => profile.IsEnabled).Select(profile => profile.UniqueId).ToArray();
        return true;
    }

    public bool TrySetProfileEnabled(Guid profileId, bool enabled, out string error)
    {
        error = string.Empty;
        if (profileId == Guid.Empty)
            return true;

        if (!IsAvailable(out error))
            return false;

        try
        {
            var result = (CustomizePlusErrorCode)(enabled
                ? _enableProfileByUniqueId.InvokeFunc(profileId)
                : _disableProfileByUniqueId.InvokeFunc(profileId));
            switch (result)
            {
                case CustomizePlusErrorCode.Success:
                case CustomizePlusErrorCode.ProfileNotFound when !enabled:
                    _lastProfileCache = DateTime.MinValue;
                    return true;
                default:
                    error = $"{(enabled ? "启用" : "禁用")} Customize+ 配置失败: {result}.";
                    return false;
            }
        }
        catch (IpcError ex)
        {
            error = $"{(enabled ? "启用" : "禁用")} Customize+ 配置失败: {ex.Message}";
            return false;
        }
    }

    public bool TryGetActiveProfileId(ObjectIndex objectIndex, out Guid profileId, out string error)
    {
        profileId = Guid.Empty;
        if (!IsAvailable(out error))
            return false;

        try
        {
            var result = _getActiveProfileIdOnCharacter.InvokeFunc(objectIndex.Index);
            if ((CustomizePlusErrorCode)result.ErrorCode is CustomizePlusErrorCode.Success && result.ProfileId.HasValue)
            {
                profileId = result.ProfileId.Value;
                error = string.Empty;
                return true;
            }

            if ((CustomizePlusErrorCode)result.ErrorCode is CustomizePlusErrorCode.ProfileNotFound)
            {
                error = string.Empty;
                return false;
            }

            error = $"查询当前 Customize+ 配置失败: {(CustomizePlusErrorCode)result.ErrorCode}.";
            return false;
        }
        catch (IpcError ex)
        {
            error = $"查询当前 Customize+ 配置失败: {ex.Message}";
            return false;
        }
    }

    public bool TrySetTemporaryProfile(ObjectIndex objectIndex, string profileJson, out Guid profileId, out string error)
    {
        profileId = Guid.Empty;
        if (!IsAvailable(out error))
            return false;

        try
        {
            var result = _setTemporaryProfileOnCharacter.InvokeFunc(objectIndex.Index, profileJson);
            if ((CustomizePlusErrorCode)result.ErrorCode is CustomizePlusErrorCode.Success && result.ProfileId.HasValue)
            {
                profileId = result.ProfileId.Value;
                error = string.Empty;
                return true;
            }

            error = $"应用 Customize+ 临时配置失败: {(CustomizePlusErrorCode)result.ErrorCode}.";
            return false;
        }
        catch (IpcError ex)
        {
            error = $"应用 Customize+ 临时配置失败: {ex.Message}";
            return false;
        }
    }

    public bool TryDeleteTemporaryProfile(Guid profileId, out string error)
    {
        error = string.Empty;
        if (profileId == Guid.Empty)
            return true;

        if (!IsAvailable(out error))
            return false;

        try
        {
            var result = (CustomizePlusErrorCode)_deleteTemporaryProfileByUniqueId.InvokeFunc(profileId);
            switch (result)
            {
                case CustomizePlusErrorCode.Success:
                case CustomizePlusErrorCode.ProfileNotFound:
                case CustomizePlusErrorCode.InvalidCharacter:
                    return true;
                default:
                    error = $"删除 Customize+ 临时配置失败: {result}.";
                    return false;
            }
        }
        catch (IpcError ex)
        {
            error = $"删除 Customize+ 临时配置失败: {ex.Message}";
            return false;
        }
    }

    public static bool Matches(ActorIdentifier identifier, CustomizePlusAssociation association)
        => association.IsSet && association.Characters.Any(character => Matches(identifier, character));

    private static bool Matches(ActorIdentifier identifier, IEnumerable<CustomizePlusCharacterTuple> characters)
        => characters.Any(character => Matches(identifier,
            new CustomizePlusCharacterAssociation(character.Name, character.WorldId, character.CharacterType, character.CharacterSubType)));

    private static bool Matches(ActorIdentifier identifier, CustomizePlusCharacterAssociation character)
    {
        var worldMatches = identifier.HomeWorld.Id == character.WorldId || character.WorldId == WorldId.AnyWorld.Id;
        var nameMatches = identifier.PlayerName.ToString().Equals(character.Name, StringComparison.OrdinalIgnoreCase);
        return character.CharacterType switch
        {
            (byte)IdentifierType.Player => identifier.Type is IdentifierType.Player && worldMatches && nameMatches,
            (byte)IdentifierType.Owned => identifier.Type is IdentifierType.Owned && worldMatches && nameMatches,
            (byte)IdentifierType.Retainer => identifier.Type is IdentifierType.Retainer
                && (character.CharacterSubType == 0 || (ushort)identifier.Retainer == character.CharacterSubType)
                && nameMatches,
            (byte)IdentifierType.Npc => identifier.Type is IdentifierType.Npc
                && identifier.ToName().Equals(character.Name, StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }
}
