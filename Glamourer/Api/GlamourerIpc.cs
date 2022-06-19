﻿using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Logging;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using System;

namespace Glamourer.Api;

public class GlamourerIpc : IDisposable
{
    public const int    CurrentApiVersion                         = 0;
    public const string LabelProviderApiVersion                   = "Glamourer.ApiVersion";
    public const string LabelProviderGetCharacterCustomization    = "Glamourer.GetCharacterCustomization";
    public const string LabelProviderApplyCharacterCustomization  = "Glamourer.ApplyCharacterCustomization";
    public const string LabelProviderRevertCharacterCustomization = "Glamourer.RevertCharacterCustomization";

    private readonly ClientState            _clientState;
    private readonly ObjectTable            _objectTable;
    private readonly DalamudPluginInterface _pluginInterface;

    internal ICallGateProvider<string>?                 ProviderGetCharacterCustomization;
    internal ICallGateProvider<string, string, object>? ProviderApplyCharacterCustomization;
    internal ICallGateProvider<string, object>?         ProviderRevertCharacterCustomization;
    internal ICallGateProvider<int>?                    ProviderGetApiVersion;

    public GlamourerIpc(ClientState clientState, ObjectTable objectTable, DalamudPluginInterface pluginInterface)
    {
        _clientState     = clientState;
        _objectTable     = objectTable;
        _pluginInterface = pluginInterface;

        InitializeProviders();
    }

    public void Dispose()
        => DisposeProviders();

    private void DisposeProviders()
    {
        ProviderApplyCharacterCustomization?.UnregisterFunc();
        ProviderGetCharacterCustomization?.UnregisterAction();
        ProviderRevertCharacterCustomization?.UnregisterAction();
        ProviderGetApiVersion?.UnregisterFunc();
    }

    private void InitializeProviders()
    {
        try
        {
            ProviderGetApiVersion = _pluginInterface.GetIpcProvider<int>(LabelProviderApiVersion);
            ProviderGetApiVersion.RegisterFunc(GetApiVersion);
        }
        catch (Exception ex)
        {
            PluginLog.Error(ex, $"Error registering IPC provider for {LabelProviderApiVersion}.");
        }

        try
        {
            ProviderGetCharacterCustomization = _pluginInterface.GetIpcProvider<string>(LabelProviderGetCharacterCustomization);
            ProviderGetCharacterCustomization.RegisterFunc(GetCharacterCustomization);
        }
        catch (Exception ex)
        {
            PluginLog.Error(ex, $"Error registering IPC provider for {LabelProviderApplyCharacterCustomization}.");
        }

        try
        {
            ProviderApplyCharacterCustomization =
                _pluginInterface.GetIpcProvider<string, string, object>(LabelProviderApplyCharacterCustomization);
            ProviderApplyCharacterCustomization.RegisterAction(ApplyCharacterCustomization);
        }
        catch (Exception ex)
        {
            PluginLog.Error(ex, $"Error registering IPC provider for {LabelProviderApplyCharacterCustomization}.");
        }

        try
        {
            ProviderRevertCharacterCustomization =
                _pluginInterface.GetIpcProvider<string, object>(LabelProviderRevertCharacterCustomization);
            ProviderRevertCharacterCustomization.RegisterAction(RevertCharacterCustomization);
        }
        catch (Exception ex)
        {
            PluginLog.Error(ex, $"Error registering IPC provider for {LabelProviderRevertCharacterCustomization}.");
        }
    }

    private static int GetApiVersion()
        => CurrentApiVersion;

    private void ApplyCharacterCustomization(string customization, string characterName)
    {
        var save = CharacterSave.FromString(customization);
        foreach (var gameObject in _objectTable)
        {
            if (gameObject.Name.ToString() != characterName)
                continue;

            var player = (Character)gameObject;
            Glamourer.RevertableDesigns.Revert(player);
            Glamourer.RevertableDesigns.Add(player);
            save.Apply(player);
            Glamourer.Penumbra.UpdateCharacters(player, null);
            break;
        }
    }

    private void RevertCharacterCustomization(string characterName)
    {
        foreach (var gameObject in _objectTable)
        {
            if (gameObject.Name.ToString() != characterName)
                continue;

            var player = (Character)gameObject;
            Glamourer.RevertableDesigns.Revert(player);
            Glamourer.Penumbra.UpdateCharacters(player, null);
            break;
        }
    }

    private string GetCharacterCustomization()
    {
        var save = new CharacterSave();
        save.LoadCharacter(_clientState.LocalPlayer!);
        return save.ToBase64();
    }
}