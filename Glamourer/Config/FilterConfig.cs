using System.Text.Json;
using Glamourer.Gui.Tabs.UnlocksTab;
using Glamourer.Services;
using ImSharp;
using Luna;
using Luna.Generators;
using Penumbra.GameData.DataContainers;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;

namespace Glamourer.Config;

public sealed partial class FilterConfig : ConfigurationFile<FilenameService>
{
    private const    ActorTypeFilter AllFiltered = (ActorTypeFilter)0x3FF;
    private readonly DictJob         _jobs;

    public FilterConfig(SaveService saveService, MessageService messager, DictJob jobs)
        : base(saveService, messager, TimeSpan.FromMinutes(5))
    {
        _jobs             = jobs;
        _unlocksJobFilter = _jobs.AllAvailableJobs;
        Load();
    }

    public override int CurrentVersion
        => 1;

    protected override void AddData(Utf8JsonWriter j)
    {
        WriteActor(j);
        WriteFilter(j, DesignFilter, "Designs"u8);
        WriteFilter(j, NpcFilter,    "Npcs"u8);
        WriteAutomation(j);
        WriteUnlocksTab(j);
    }

    protected override void LoadData(in JsonElement j)
    {
        if (j.TryReadObject("Actors"u8, out var actors))
        {
            _actorFilter     = actors.PropertyOrDefault("Filter"u8, string.Empty);
            _actorTypeFilter = (ActorTypeFilter)(actors.PropertyOrDefault("Type"u8, 0u) & (uint)AllFiltered);
        }

        if (j.TryReadObject("Designs"u8, out var designs))
            _designFilter = designs.PropertyOrDefault("Filter"u8, string.Empty);

        if (j.TryReadObject("Npcs"u8, out var npcs))
            _npcFilter = npcs.PropertyOrDefault("Filter"u8, string.Empty);

        if (j.TryReadObject("Automation"u8, out var automation))
        {
            _automationFilter = automation.PropertyOrDefault("Filter"u8, string.Empty);
            _automationStateFilter = automation.TryReadProperty("State"u8, out bool? state, true) ? state : null;
        }

        LoadUnlocksTab(j);
    }

    [ConfigProperty]
    private string _designFilter = string.Empty;

    [ConfigProperty]
    private string _actorFilter = string.Empty;

    [ConfigProperty]
    private ActorTypeFilter _actorTypeFilter = ActorTypeFilter.None;

    [ConfigProperty]
    private string _automationFilter = string.Empty;

    [ConfigProperty]
    private bool? _automationStateFilter;

    [ConfigProperty]
    private string _npcFilter = string.Empty;

    [ConfigProperty]
    private YesNoFlag _unlocksFavoriteFilter = YesNoFlag.Either;

    [ConfigProperty]
    private UnlockCacheItem.Modded _unlocksModdedFilter = UnlockCacheItem.ModdedAll;

    [ConfigProperty]
    private string _unlocksNameFilter = string.Empty;

    [ConfigProperty]
    private string _unlocksTypeFilter = string.Empty;

    [ConfigProperty]
    private EquipFlag _unlocksSlotFilter = UnlockCacheItem.SlotsAll;

    [ConfigProperty]
    private YesNoFlag _unlocksUnlockedFilter = YesNoFlag.Either;

    [ConfigProperty]
    private string _unlocksItemIdFilter = string.Empty;

    [ConfigProperty]
    private string _unlocksModelDataFilter = string.Empty;

    [ConfigProperty]
    private string _unlocksLevelFilter = string.Empty;

    [ConfigProperty]
    private JobFlag _unlocksJobFilter;

    [ConfigProperty]
    private UnlockCacheItem.Dyability _unlocksDyabilityFilter = UnlockCacheItem.DyableAll;

    [ConfigProperty]
    private YesNoFlag _unlocksTradableFilter = YesNoFlag.Either;

    [ConfigProperty]
    private YesNoFlag _unlocksCrestFilter = YesNoFlag.Either;

    private void WriteUnlocksTab(Utf8JsonWriter j)
    {
        using var tmp = j.TemporaryObject("Unlocks"u8);
        tmp.WriteUnsignedIfNot("Favorite"u8,  UnlocksFavoriteFilter,  YesNoFlagExtensions.Either);
        tmp.WriteUnsignedIfNot("Crest"u8,     UnlocksCrestFilter,     YesNoFlagExtensions.Either);
        tmp.WriteUnsignedIfNot("Tradable"u8,  UnlocksTradableFilter,  YesNoFlagExtensions.Either);
        tmp.WriteUnsignedIfNot("Unlocked"u8,  UnlocksUnlockedFilter,  YesNoFlagExtensions.Either);
        tmp.WriteUnsignedIfNot("Modded"u8,    UnlocksModdedFilter,    UnlockCacheItem.ModdedAll);
        tmp.WriteUnsignedIfNot("Dyability"u8, UnlocksDyabilityFilter, UnlockCacheItem.DyableAll);
        tmp.WriteUnsignedIfNot("Slot"u8,      UnlocksSlotFilter,      UnlockCacheItem.SlotsAll);
        tmp.WriteUnsignedIfNot("Job"u8,       UnlocksJobFilter,       _jobs.AllAvailableJobs);
        tmp.WriteNonEmptyString("Level"u8,     UnlocksLevelFilter);
        tmp.WriteNonEmptyString("ModelData"u8, UnlocksModelDataFilter);
        tmp.WriteNonEmptyString("ItemId"u8,    UnlocksItemIdFilter);
        tmp.WriteNonEmptyString("Name"u8,      UnlocksNameFilter);
        tmp.WriteNonEmptyString("Type"u8,      UnlocksTypeFilter);
    }

    private void LoadUnlocksTab(in JsonElement j)
    {
        if (!j.TryReadObject("Unlocks"u8, out var unlocks))
            return;

        _unlocksFavoriteFilter  = (YesNoFlag)unlocks.PropertyOrDefault("Favorite"u8,  (uint)YesNoFlag.Either);
        _unlocksCrestFilter     = (YesNoFlag)unlocks.PropertyOrDefault("Crest"u8,     (uint)YesNoFlag.Either);
        _unlocksTradableFilter  = (YesNoFlag)unlocks.PropertyOrDefault("Tradable"u8,  (uint)YesNoFlag.Either);
        _unlocksUnlockedFilter  = (YesNoFlag)unlocks.PropertyOrDefault("Unlocked"u8,  (uint)YesNoFlag.Either);
        _unlocksModdedFilter    = (UnlockCacheItem.Modded)unlocks.PropertyOrDefault("Modded"u8,    (uint)UnlockCacheItem.ModdedAll);
        _unlocksDyabilityFilter = (UnlockCacheItem.Dyability)unlocks.PropertyOrDefault("Dyability"u8, (uint)UnlockCacheItem.DyableAll);
        _unlocksSlotFilter      = (EquipFlag)unlocks.PropertyOrDefault("Slot"u8,      (uint)UnlockCacheItem.SlotsAll);
        _unlocksJobFilter       = (JobFlag)unlocks.PropertyOrDefault("Job"u8,         (ulong)_jobs.AllAvailableJobs);
        _unlocksLevelFilter     = unlocks.PropertyOrDefault("Level"u8,     string.Empty);
        _unlocksModelDataFilter = unlocks.PropertyOrDefault("ModelData"u8, string.Empty);
        _unlocksItemIdFilter    = unlocks.PropertyOrDefault("ItemId"u8,    string.Empty);
        _unlocksNameFilter      = unlocks.PropertyOrDefault("Name"u8,      string.Empty);
        _unlocksTypeFilter      = unlocks.PropertyOrDefault("Type"u8,      string.Empty);
    }


    public override string ToFilePath(FilenameService fileNames)
        => fileNames.FilterFile;

    private void WriteActor(Utf8JsonWriter j)
    {
        using var tmp = j.TemporaryObject("Actors"u8);
        j.WriteNonEmptyString("Filter"u8, ActorFilter);
        if (ActorTypeFilter is not ActorTypeFilter.None)
            j.WriteNumber("Type"u8, (uint)ActorTypeFilter);
    }

    private void WriteAutomation(Utf8JsonWriter j)
    {
        using var tmp = j.TemporaryObject("Automation"u8);
        if (AutomationStateFilter is not null)
            j.WriteBoolean("State"u8, AutomationStateFilter.Value);
        tmp.WriteNonEmptyString("Filter"u8, AutomationFilter);
    }

    private static void WriteFilter(Utf8JsonWriter j, string filter, ReadOnlySpan<byte> tabName)
    {
        if (filter.Length is 0)
            return;

        j.WritePropertyName(tabName);
        j.WriteStartObject();
        j.WriteString("Filter"u8, filter);
        j.WriteEndObject();
    }
}
