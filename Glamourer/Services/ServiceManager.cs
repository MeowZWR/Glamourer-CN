﻿using Dalamud.Plugin;
using Glamourer.Api;
using Glamourer.Automation;
using Glamourer.Designs;
using Glamourer.Events;
using Glamourer.Gui;
using Glamourer.Gui.Customization;
using Glamourer.Gui.Equipment;
using Glamourer.Gui.Tabs;
using Glamourer.Gui.Tabs.ActorTab;
using Glamourer.Gui.Tabs.AutomationTab;
using Glamourer.Gui.Tabs.DesignTab;
using Glamourer.Interop;
using Glamourer.Interop.Penumbra;
using Glamourer.State;
using Microsoft.Extensions.DependencyInjection;
using OtterGui.Classes;
using OtterGui.Log;

namespace Glamourer.Services;

public static class ServiceManager
{
    public static ServiceProvider CreateProvider(DalamudPluginInterface pi, Logger log)
    {
        var services = new ServiceCollection()
            .AddSingleton(log)
            .AddDalamud(pi)
            .AddMeta()
            .AddInterop()
            .AddEvents()
            .AddData()
            .AddDesigns()
            .AddState()
            .AddUi()
            .AddApi();

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
    }

    private static IServiceCollection AddDalamud(this IServiceCollection services, DalamudPluginInterface pi)
    {
        new DalamudServices(pi).AddServices(services);
        return services;
    }

    private static IServiceCollection AddMeta(this IServiceCollection services)
        => services.AddSingleton<ChatService>()
            .AddSingleton<FilenameService>()
            .AddSingleton<BackupService>()
            .AddSingleton<FrameworkManager>()
            .AddSingleton<SaveService>()
            .AddSingleton<PhrasingService>()
            .AddSingleton<ConfigMigrationService>()
            .AddSingleton<Configuration>();

    private static IServiceCollection AddEvents(this IServiceCollection services)
        => services.AddSingleton<VisorStateChanged>()
            .AddSingleton<SlotUpdating>()
            .AddSingleton<DesignChanged>()
            .AddSingleton<AutomationChanged>()
            .AddSingleton<StateChanged>()
            .AddSingleton<WeaponLoading>()
            .AddSingleton<HeadGearVisibilityChanged>()
            .AddSingleton<WeaponVisibilityChanged>();

    private static IServiceCollection AddData(this IServiceCollection services)
        => services.AddSingleton<IdentifierService>()
            .AddSingleton<ItemService>()
            .AddSingleton<ActorService>()
            .AddSingleton<CustomizationService>()
            .AddSingleton<ItemManager>();

    private static IServiceCollection AddInterop(this IServiceCollection services)
        => services.AddSingleton<VisorService>()
            .AddSingleton<ChangeCustomizeService>()
            .AddSingleton<MetaService>()
            .AddSingleton<UpdateSlotService>()
            .AddSingleton<WeaponService>()
            .AddSingleton<PenumbraService>()
            .AddSingleton<ObjectManager>()
            .AddSingleton<PenumbraAutoRedraw>()
            .AddSingleton<JobService>();

    private static IServiceCollection AddDesigns(this IServiceCollection services)
        => services.AddSingleton<DesignManager>()
            .AddSingleton<DesignFileSystem>()
            .AddSingleton<AutoDesignManager>()
            .AddSingleton<AutoDesignApplier>()
            .AddSingleton<FixedDesignMigrator>();

    private static IServiceCollection AddState(this IServiceCollection services)
        => services.AddSingleton<StateManager>()
            .AddSingleton<StateEditor>()
            .AddSingleton<StateListener>();

    private static IServiceCollection AddUi(this IServiceCollection services)
        => services.AddSingleton<DebugTab>()
            .AddSingleton<SettingsTab>()
            .AddSingleton<ActorTab>()
            .AddSingleton<ActorSelector>()
            .AddSingleton<ActorPanel>()
            .AddSingleton<MainWindow>()
            .AddSingleton<GlamourerWindowSystem>()
            .AddSingleton<CustomizationDrawer>()
            .AddSingleton<EquipmentDrawer>()
            .AddSingleton<DesignFileSystemSelector>()
            .AddSingleton<DesignPanel>()
            .AddSingleton<DesignTab>()
            .AddSingleton<PenumbraChangedItemTooltip>()
            .AddSingleton<AutomationTab>()
            .AddSingleton<SetSelector>()
            .AddSingleton<SetPanel>();

    private static IServiceCollection AddApi(this IServiceCollection services)
        => services.AddSingleton<CommandService>()
            .AddSingleton<GlamourerIpc>();
}