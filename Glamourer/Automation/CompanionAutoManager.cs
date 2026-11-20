using System.Text.Json;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin.Services;
using Glamourer.Designs;
using Glamourer.Events;
using Glamourer.Services;
using Glamourer.State;
using Luna;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;

namespace Glamourer.Automation;

public sealed class CompanionAutoManager : ISavable, IReadOnlyList<CompanionAutoSet>, IDisposable, IService
{
    public const int CurrentVersion = 1;

    private const uint ChocoboNameId = 780;

    private readonly SaveService       _saveService;
    private readonly ActorManager      _actors;
    private readonly IDataManager      _gameData;
    private readonly AutoDesignManager _autoDesigns;
    private readonly AutomationChanged _automationChanged;
    private readonly DesignChanged     _designChanged;

    private readonly List<CompanionAutoSet> _data = [];

    private readonly Dictionary<(ObjectKind, uint), AutoDesignSet?> _views    = [];
    private readonly HashSet<AutoDesignSet>                          _viewSets = [];

    public event Action? Changed;

    public CompanionAutoManager(SaveService saveService, ActorManager actors, IDataManager gameData, AutoDesignManager autoDesigns,
        AutomationChanged automationChanged, DesignChanged designChanged)
    {
        _saveService       = saveService;
        _actors            = actors;
        _gameData          = gameData;
        _autoDesigns       = autoDesigns;
        _automationChanged = automationChanged;
        _designChanged     = designChanged;
        Load();
        _automationChanged.Subscribe(OnAutomationChanged, AutomationChanged.Priority.AutoDesignApplier);
        _designChanged.Subscribe(OnDesignChanged, DesignChanged.Priority.AutoDesignManager);
    }

    public void Dispose()
    {
        _automationChanged.Unsubscribe(OnAutomationChanged);
        _designChanged.Unsubscribe(OnDesignChanged);
    }

    public IEnumerator<CompanionAutoSet> GetEnumerator()
        => _data.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public int Count
        => _data.Count;

    public CompanionAutoSet this[int index]
        => _data[index];

    public int IndexOf(CompanionAutoSet set)
        => _data.IndexOf(set);

    public bool Contains(AutoDesignSet set)
        => _viewSets.Contains(set);

    public bool IsLocalPlayerCompanion(in ActorIdentifier identifier)
    {
        if (identifier.Type is not IdentifierType.Owned || !HumanModelPolicy.AllowsHumanModelOverride(identifier))
            return false;

        var player = _actors.GetCurrentPlayer();
        return player.IsValid && player.HomeWorld == identifier.HomeWorld && player.PlayerName.Equals(identifier.PlayerName);
    }

    public bool TryGetEnabledSet(in ActorIdentifier identifier, [NotNullWhen(true)] out AutoDesignSet? set)
    {
        if (!IsLocalPlayerCompanion(identifier))
        {
            set = null;
            return false;
        }

        var key = (identifier.Kind, identifier.DataId.Id);
        if (!_views.TryGetValue(key, out set))
        {
            set = null;
            foreach (var companionSet in _data.Where(s => s.Set.Enabled))
            {
                set = companionSet.CreateView(identifier);
                if (set is not null)
                    break;
            }

            _views[key] = set;
            if (set is not null)
                _viewSets.Add(set);
        }

        return set is not null;
    }

    public void AddSet(string name)
    {
        if (name.Length is 0)
            return;

        _data.Add(new CompanionAutoSet(new AutoDesignSet(name)));
        Save();
        Glamourer.Log.Debug($"Created new companion design set {name}.");
    }

    public void DuplicateSet(CompanionAutoSet set)
    {
        var newSet = new CompanionAutoSet(new AutoDesignSet($"{set.Set.Name} (副本)"));
        foreach (var design in set.Set.Designs)
        {
            var clone = design.Clone();
            newSet.Set.Designs.Add(clone);
            var limit = set.GetLimit(design);
            if (limit.Kind is not CompanionLimitKind.Unset)
                newSet.Limits[clone] = limit;
        }

        _data.Add(newSet);
        Save();
        Glamourer.Log.Debug($"Duplicated companion design set {set.Set.Name}.");
    }

    public void DeleteSet(CompanionAutoSet set)
    {
        if (!_data.Remove(set))
            return;

        Glamourer.Log.Debug($"Deleted companion design set {set.Set.Name}.");
        OnChange();
    }

    public void MoveSet(int from, int to)
    {
        if (!_data.Move(from, to))
            return;

        Glamourer.Log.Debug($"Moved companion design set {from + 1} to position {to + 1}.");
        OnChange();
    }

    public void Rename(CompanionAutoSet set, string newName)
    {
        if (newName.Length is 0 || set.Set.Name == newName)
            return;

        set.Set.Name = newName;
        Save();
    }

    public void SetState(CompanionAutoSet set, bool value)
    {
        if (set.Set.Enabled == value)
            return;

        set.Set.Enabled = value;
        OnChange();
    }

    public void ChangeBaseState(CompanionAutoSet set, AutoDesignSet.Base newBase)
    {
        if (set.Set.BaseState == newBase)
            return;

        set.Set.BaseState = newBase;
        OnChange();
    }

    public void ChangeResetSettings(CompanionAutoSet set, bool value)
    {
        if (set.Set.ResetTemporarySettings == value)
            return;

        set.Set.ResetTemporarySettings = value;
        OnChange();
    }

    public void AddDesign(CompanionAutoSet set, IDesignStandIn design)
        => _autoDesigns.AddDesign(set.Set, design, CompanionDesignRules.Normalize(ApplicationType.All));

    public void ChangeTarget(CompanionAutoSet set, AutoDesign design, CompanionLimit limit)
    {
        if (set.GetLimit(design).SameAs(limit))
            return;

        if (limit.Kind is CompanionLimitKind.Unset)
            set.Limits.Remove(design);
        else
            set.Limits[design] = limit;

        OnChange();
    }

    public IEnumerable<CompanionTarget> GetAvailableTargets()
    {
        var petNames = _gameData.GetExcelSheet<Lumina.Excel.Sheets.Pet>()
            .Select(p => p.Name.ExtractTextExtended())
            .Where(n => n.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var pets = _actors.Data.BNpcs.Where(kvp => kvp.Key.Id is ChocoboNameId || petNames.Contains(kvp.Value));
        return Group(ObjectKind.BattleNpc, pets).Concat(Group(ObjectKind.Companion, _actors.Data.Companions));

        static IEnumerable<CompanionTarget> Group(ObjectKind kind, IEnumerable<KeyValuePair<NpcId, string>> table)
            => table.GroupBy(kvp => kvp.Value)
                .Select(g => new CompanionTarget(kind, g.Min(kvp => kvp.Key.Id), g.Key, g.Select(kvp => kvp.Key.Id).ToHashSet()));
    }

    private CompanionTarget? CreateTarget(ObjectKind kind, uint id)
    {
        if (kind is not (ObjectKind.BattleNpc or ObjectKind.Companion) || !_actors.Data.TryGetName(kind, id, out var name))
            return null;

        IReadOnlyDictionary<NpcId, string> table = kind is ObjectKind.BattleNpc ? _actors.Data.BNpcs : _actors.Data.Companions;
        return new CompanionTarget(kind, id, name, table.Where(kvp => kvp.Value == name).Select(kvp => kvp.Key.Id).ToHashSet());
    }

    private void OnChange()
    {
        _views.Clear();
        _viewSets.Clear();
        Save();
        Changed?.Invoke();
    }

    private void OnAutomationChanged(in AutomationChanged.Arguments arguments)
    {
        var set = arguments.Set;
        if (_data.Any(s => s.Set == set))
            OnChange();
    }

    private void OnDesignChanged(in DesignChanged.Arguments arguments)
    {
        if (arguments.Type is DesignChanged.Type.Deleted)
        {
            foreach (var set in _data)
            {
                for (var i = 0; i < set.Set.Designs.Count; ++i)
                {
                    if (set.Set.Designs[i].Design == arguments.Design)
                        _autoDesigns.DeleteDesign(set.Set, i--);
                }
            }

            return;
        }

        if (arguments.Type is not (DesignChanged.Type.ApplyCustomize or DesignChanged.Type.EntireCustomize)
         && !CompanionDesignRules.ChangesCustomizeSet(arguments.Transaction))
            return;

        var changed = arguments.Design;
        if (_data.Any(set => set.Set.Designs.Any(design => design.Design == changed)))
            OnChange();
    }

    public string ToFilePath(FilenameService fileNames)
        => Path.Combine(Path.GetDirectoryName(fileNames.AutomationFile)!, "companion_automation.json");

    public void Save(Stream stream)
    {
        using var j = new Utf8JsonWriter(stream, JsonFunctions.WriterOptions);
        j.WriteStartObject();
        j.WriteNumber("Version"u8, CurrentVersion);
        j.WriteStartArray("Data"u8);
        foreach (var set in _data)
            set.Serialize(j);
        j.WriteEndArray();
        j.WriteEndObject();
    }

    private void Save()
        => _saveService.DelaySave(this);

    private void Load()
    {
        var file = ToFilePath(_saveService.FileNames);
        _data.Clear();
        if (!File.Exists(file))
            return;

        try
        {
            var text = JsonFunctions.ReadUtf8Bytes(file, out _);
            var obj  = JsonDocument.Parse(text, JsonFunctions.DocumentOptions);
            if (obj.RootElement.PropertyOrDefault("Version"u8, 0) is not CurrentVersion
             || !obj.RootElement.TryReadArray("Data"u8, out var array))
                return;

            foreach (var setObj in array.EnumerateArray())
            {
                if (setObj.ValueKind is not JsonValueKind.Object)
                    continue;

                var name = setObj.PropertyOrDefault("Name"u8, string.Empty);
                if (name.Length is 0)
                    continue;

                var set = new CompanionAutoSet(new AutoDesignSet(name)
                {
                    Enabled                = setObj.PropertyOrDefault("Enabled"u8,                false),
                    ResetTemporarySettings = setObj.PropertyOrDefault("ResetTemporarySettings"u8, false),
                    BaseState              = setObj.EnumOrDefault("BaseState"u8, AutoDesignSet.Base.Current),
                });

                if (setObj.TryReadArray("Rows"u8, out var rows))
                    foreach (var row in rows.EnumerateArray())
                    {
                        if (row.ValueKind is not JsonValueKind.Object
                         || !row.TryReadObject("Design"u8, out var designObj)
                         || _autoDesigns.ToDesignObject(name, designObj) is not { } design)
                            continue;

                        set.Set.Designs.Add(design);
                        if (row.PropertyOrDefault("AllPets"u8, false))
                            set.Limits[design] = CompanionLimit.AllPets;
                        else if (CreateTarget(row.EnumOrDefault("Kind"u8, ObjectKind.None), row.PropertyOrDefault("Id"u8, 0u)) is { } target)
                            set.Limits[design] = new CompanionLimit(CompanionLimitKind.Target, target);
                    }

                _data.Add(set);
            }
        }
        catch (Exception ex)
        {
            Glamourer.Messager.NotificationMessage(ex, "Failure to load companion automation: Error during parsing.",
                "Failure to load companion automation", NotificationType.Error);
        }
    }
}
