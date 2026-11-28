using System.Text.Json;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin.Services;
using Glamourer.Services;
using Luna;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Data;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;

namespace Glamourer.MountReplacement;

public sealed record MountInfo(
    uint Id,
    string Name,
    uint ModelChara,
    CharacterArmor Head,
    CharacterArmor Body,
    CharacterArmor Legs,
    CharacterArmor Feet,
    byte ExtraSeats,
    bool IsFlying)
{
    public bool HasEquipment
        => Head != CharacterArmor.Empty || Body != CharacterArmor.Empty || Legs != CharacterArmor.Empty || Feet != CharacterArmor.Empty;
}

public sealed class MountReplacementRule
{
    /// <summary> The original mount ID, 0 for any mount. </summary>
    public uint Source;

    /// <summary> The displayed mount ID, 0 for unset. </summary>
    public uint Target;

    public bool Enabled = true;
}

public sealed class MountReplacementManager : ISavable, IService
{
    public const int CurrentVersion = 1;

    private readonly SaveService  _saveService;
    private readonly ActorManager _actors;

    private readonly List<MountReplacementRule> _rules = [];

    public readonly IReadOnlyDictionary<uint, MountInfo> Mounts;

    public bool Enabled { get; private set; }

    public IReadOnlyList<MountReplacementRule> Rules
        => _rules;

    public event Action? Changed;

    public MountReplacementManager(SaveService saveService, ActorManager actors, IDataManager gameData)
    {
        _saveService = saveService;
        _actors      = actors;
        Mounts       = CreateMounts(gameData);
        Load();
    }

    public bool IsLocalPlayerMount(in ActorIdentifier identifier)
    {
        if (identifier.Type is not IdentifierType.Owned || identifier.Kind is not ObjectKind.Mount)
            return false;

        var player = _actors.GetCurrentPlayer();
        return player.IsValid && player.HomeWorld == identifier.HomeWorld && player.PlayerName.Equals(identifier.PlayerName);
    }

    public bool TryGetTarget(uint source, [NotNullWhen(true)] out MountInfo? target)
    {
        target = null;
        if (!Enabled)
            return false;

        var rule = _rules.FirstOrDefault(r => r.Enabled && r.Target is not 0 && r.Source == source)
         ?? _rules.FirstOrDefault(r => r.Enabled && r.Target is not 0 && r.Source is 0);
        return rule is not null && rule.Target != source && Mounts.TryGetValue(rule.Target, out target);
    }

    public uint GetTarget(uint source)
        => _rules.FirstOrDefault(r => r.Source == source && source is not 0) is { Enabled: true } rule ? rule.Target : 0;

    public void SetTarget(uint source, uint target)
    {
        var rule = _rules.FirstOrDefault(r => r.Source == source);
        if (rule is null)
        {
            if (target is 0)
                return;

            rule = new MountReplacementRule { Source = source };
            _rules.Add(rule);
        }
        else if (rule.Target == target && (rule.Enabled || target is 0))
        {
            return;
        }

        rule.Target  = target;
        rule.Enabled = rule.Enabled || target is not 0;
        OnChange();
    }

    public void SetEnabled(bool value)
    {
        if (Enabled == value)
            return;

        Enabled = value;
        OnChange();
    }

    public void AddRule(uint source)
    {
        _rules.Add(new MountReplacementRule { Source = source });
        OnChange();
    }

    public void DeleteRule(int index)
    {
        if (index < 0 || index >= _rules.Count)
            return;

        _rules.RemoveAt(index);
        OnChange();
    }

    public void ChangeSource(MountReplacementRule rule, uint source)
    {
        if (rule.Source == source)
            return;

        rule.Source = source;
        OnChange();
    }

    public void ChangeTarget(MountReplacementRule rule, uint target)
    {
        if (rule.Target == target)
            return;

        rule.Target = target;
        OnChange();
    }

    public void SetRuleEnabled(MountReplacementRule rule, bool value)
    {
        if (rule.Enabled == value)
            return;

        rule.Enabled = value;
        OnChange();
    }

    private void OnChange()
    {
        _saveService.DelaySave(this);
        Changed?.Invoke();
    }

    private static Dictionary<uint, MountInfo> CreateMounts(IDataManager gameData)
    {
        var ret = new Dictionary<uint, MountInfo>();
        foreach (var mount in gameData.GetExcelSheet<Lumina.Excel.Sheets.Mount>())
        {
            var name = mount.Singular.ExtractTextExtended();
            if (mount.ModelChara.RowId is 0 || name.Length is 0)
                continue;

            ret[mount.RowId] = new MountInfo(mount.RowId, name, mount.ModelChara.RowId, ToArmor(mount.EquipHead), ToArmor(mount.EquipBody),
                ToArmor(mount.EquipLeg), ToArmor(mount.EquipFoot), mount.ExtraSeats, mount.IsFlying is not 0);
        }

        return ret;
    }

    public static CharacterArmor ToArmor(int value)
    {
        var v = (ulong)(uint)value;
        return Unsafe.As<ulong, CharacterArmor>(ref v);
    }

    public string ToFilePath(FilenameService fileNames)
        => Path.Combine(Path.GetDirectoryName(fileNames.AutomationFile)!, "mount_replacement.json");

    public void Save(Stream stream)
    {
        using var j = new Utf8JsonWriter(stream, JsonFunctions.WriterOptions);
        j.WriteStartObject();
        j.WriteNumber("Version"u8, CurrentVersion);
        j.WriteBoolean("Enabled"u8, Enabled);
        j.WriteStartArray("Rules"u8);
        foreach (var rule in _rules)
        {
            j.WriteStartObject();
            j.WriteNumber("Source"u8, rule.Source);
            j.WriteNumber("Target"u8, rule.Target);
            j.WriteBoolean("Enabled"u8, rule.Enabled);
            j.WriteEndObject();
        }

        j.WriteEndArray();
        j.WriteEndObject();
    }

    private void Load()
    {
        var file = ToFilePath(_saveService.FileNames);
        _rules.Clear();
        if (!File.Exists(file))
            return;

        try
        {
            var text = JsonFunctions.ReadUtf8Bytes(file, out _);
            var obj  = JsonDocument.Parse(text, JsonFunctions.DocumentOptions);
            if (obj.RootElement.PropertyOrDefault("Version"u8, 0) is not CurrentVersion)
                return;

            Enabled = obj.RootElement.PropertyOrDefault("Enabled"u8, false);
            if (!obj.RootElement.TryReadArray("Rules"u8, out var array))
                return;

            foreach (var ruleObj in array.EnumerateArray())
            {
                if (ruleObj.ValueKind is not JsonValueKind.Object)
                    continue;

                _rules.Add(new MountReplacementRule
                {
                    Source  = ruleObj.PropertyOrDefault("Source"u8,  0u),
                    Target  = ruleObj.PropertyOrDefault("Target"u8,  0u),
                    Enabled = ruleObj.PropertyOrDefault("Enabled"u8, true),
                });
            }
        }
        catch (Exception ex)
        {
            Glamourer.Messager.NotificationMessage(ex, "Failure to load mount replacement: Error during parsing.",
                "Failure to load mount replacement", NotificationType.Error);
        }
    }
}
