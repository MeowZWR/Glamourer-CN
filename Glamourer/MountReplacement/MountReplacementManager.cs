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
    bool IsFlying,
    ushort Model,
    byte ModelType,
    ushort RideBgmId,
    ushort SoundPack,
    string SummonSound,
    string DismissSound,
    ushort[] RaceScales)
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

    private readonly List<MountReplacementRule> _rules     = [];
    private readonly HashSet<uint>              _favorites = [];

    public readonly IReadOnlyDictionary<uint, MountInfo> Mounts;

    public bool Enabled { get; private set; }

    public IReadOnlyList<MountReplacementRule> Rules
        => _rules;

    public IReadOnlySet<uint> Favorites
        => _favorites;

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

    public bool HasSource(uint source)
        => _rules.Any(r => r.Source == source);

    public bool IsFavorite(uint mount)
        => _favorites.Contains(mount);

    public void ToggleFavorite(uint mount)
    {
        if (mount is 0 || !Mounts.ContainsKey(mount))
            return;

        if (!_favorites.Remove(mount))
            _favorites.Add(mount);
        _saveService.DelaySave(this);
    }

    public void SetTarget(uint source, uint target)
    {
        if (target is not 0 && target == source)
            return;

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
        if (HasSource(source))
            return;

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
        if (rule.Source == source || HasSource(source))
            return;

        rule.Source = source;
        OnChange();
    }

    public void ChangeTarget(MountReplacementRule rule, uint target)
    {
        if (rule.Target == target || (target is not 0 && target == rule.Source))
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
        var ret    = new Dictionary<uint, MountInfo>();
        var charas = gameData.GetExcelSheet<Lumina.Excel.Sheets.ModelChara>()!;
        foreach (var mount in gameData.GetExcelSheet<Lumina.Excel.Sheets.Mount>())
        {
            var name = mount.Singular.ExtractTextExtended();
            if (mount.ModelChara.RowId is 0 || name.Length is 0 || !charas.TryGetRow(mount.ModelChara.RowId, out var chara))
                continue;

            ret[mount.RowId] = new MountInfo(mount.RowId, name, mount.ModelChara.RowId, ToArmor(mount.EquipHead), ToArmor(mount.EquipBody),
                ToArmor(mount.EquipLeg), ToArmor(mount.EquipFoot), mount.ExtraSeats, mount.IsFlying is not 0, chara.Model, chara.Type,
                (ushort)mount.RideBGM.RowId, chara.SEPack, mount.Unknown1.ExtractTextExtended(), mount.Unknown3.ExtractTextExtended(),
                CreateRaceScales(mount));
        }

        return ret;
    }

    private static ushort[] CreateRaceScales(Lumina.Excel.Sheets.Mount mount)
    {
        var scales = new ushort[18];
        Array.Fill(scales, (ushort)100);
        if (!mount.MountCustomize.IsValid)
            return scales;

        var row = mount.MountCustomize.Value;
        scales[0]  = Scale(row.HyurMidlanderMaleScale);
        scales[1]  = Scale(row.HyurMidlanderFemaleScale);
        scales[2]  = Scale(row.HyurHighlanderMaleScale);
        scales[3]  = Scale(row.HyurHighlanderFemaleScale);
        scales[4]  = Scale(row.ElezenMaleScale);
        scales[5]  = Scale(row.ElezenFemaleScale);
        scales[6]  = Scale(row.MiqoMaleScale);
        scales[7]  = Scale(row.MiqoFemaleScale);
        scales[8]  = Scale(row.RoeMaleScale);
        scales[9]  = Scale(row.RoeFemaleScale);
        scales[10] = Scale(row.LalaMaleScale);
        scales[11] = Scale(row.LalaFemaleScale);
        scales[12] = Scale(row.AuRaMaleScale);
        scales[13] = Scale(row.AuRaFemaleScale);
        scales[14] = Scale(row.HrothgarMaleScale);
        scales[15] = Scale(row.HrothgarFemaleScale);
        scales[16] = Scale(row.VieraMaleScale);
        scales[17] = Scale(row.VieraFemaleScale);
        return scales;
    }

    private static ushort Scale(ushort value)
        => value == 0 ? (ushort)100 : value;

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
        j.WriteStartArray("Favorites"u8);
        foreach (var favorite in _favorites)
            j.WriteNumberValue(favorite);
        j.WriteEndArray();
        j.WriteEndObject();
    }

    private void Load()
    {
        var file = ToFilePath(_saveService.FileNames);
        _rules.Clear();
        _favorites.Clear();
        if (!File.Exists(file))
            return;

        try
        {
            var text = JsonFunctions.ReadUtf8Bytes(file, out _);
            var obj  = JsonDocument.Parse(text, JsonFunctions.DocumentOptions);
            if (obj.RootElement.PropertyOrDefault("Version"u8, 0) is not CurrentVersion)
                return;

            Enabled = obj.RootElement.PropertyOrDefault("Enabled"u8, false);
            if (obj.RootElement.TryReadArray("Favorites"u8, out var favorites))
            {
                foreach (var favorite in favorites.EnumerateArray())
                {
                    if (favorite.TryGetUInt32(out var id))
                        _favorites.Add(id);
                }
            }

            if (!obj.RootElement.TryReadArray("Rules"u8, out var array))
                return;

            foreach (var ruleObj in array.EnumerateArray())
            {
                if (ruleObj.ValueKind is not JsonValueKind.Object)
                    continue;

                var source = ruleObj.PropertyOrDefault("Source"u8, 0u);
                if (HasSource(source))
                    continue;

                _rules.Add(new MountReplacementRule
                {
                    Source  = source,
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
