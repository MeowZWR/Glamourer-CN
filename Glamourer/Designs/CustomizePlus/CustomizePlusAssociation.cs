using System.Text.Json;
using Luna;
using Newtonsoft.Json.Linq;

namespace Glamourer.Designs.CustomizePlus;

public enum CustomizePlusApplicationMode
{
    TemporaryProfile,
    PermanentProfile,
}

public readonly record struct CustomizePlusCharacterAssociation(string Name, ushort WorldId, byte CharacterType, ushort CharacterSubType)
{
    public JObject Serialize()
        => new()
        {
            ["Name"]             = Name,
            ["WorldId"]          = WorldId,
            ["CharacterType"]    = CharacterType,
            ["CharacterSubType"] = CharacterSubType,
        };

    public static CustomizePlusCharacterAssociation Load(JToken? token)
        => new(
            token?["Name"]?.ToObject<string>() ?? string.Empty,
            token?["WorldId"]?.ToObject<ushort>() ?? 0,
            token?["CharacterType"]?.ToObject<byte>() ?? 0,
            token?["CharacterSubType"]?.ToObject<ushort>() ?? 0);
}

public sealed class CustomizePlusAssociation
{
    private CustomizePlusCharacterAssociation[] _characters = [];

    public Guid ProfileId { get; private set; } = Guid.Empty;
    public string ProfileName { get; private set; } = string.Empty;
    public string ProfilePath { get; private set; } = string.Empty;

    public IReadOnlyList<CustomizePlusCharacterAssociation> Characters
        => _characters;

    public bool IsSet
        => ProfileId != Guid.Empty;

    public CustomizePlusAssociation Clone()
        => new()
        {
            ProfileId   = ProfileId,
            ProfileName = ProfileName,
            ProfilePath = ProfilePath,
            _characters = [.. _characters],
        };

    public bool Update(Guid profileId, string profileName, string profilePath, IEnumerable<CustomizePlusCharacterAssociation>? characters)
    {
        var newCharacters = characters?.ToArray() ?? [];
        if (ProfileId == profileId
         && ProfileName == profileName
         && ProfilePath == profilePath
         && _characters.SequenceEqual(newCharacters))
            return false;

        ProfileId   = profileId;
        ProfileName = profileName;
        ProfilePath = profilePath;
        _characters = newCharacters;
        return true;
    }

    public bool Update(CustomizePlusAssociation other)
        => Update(other.ProfileId, other.ProfileName, other.ProfilePath, other.Characters);

    public bool Clear()
        => Update(Guid.Empty, string.Empty, string.Empty, []);

    public JObject Serialize()
    {
        var ret = new JObject
        {
            ["ProfileId"]   = ProfileId,
            ["ProfileName"] = ProfileName,
            ["ProfilePath"] = ProfilePath,
        };

        if (_characters.Length > 0)
            ret["Characters"] = new JArray(_characters.Select(character => character.Serialize()));

        return ret;
    }

    public static CustomizePlusAssociation Load(JToken? token)
    {
        var association = new CustomizePlusAssociation();
        if (token is not JObject obj)
            return association;

        association.Update(
            obj["ProfileId"]?.ToObject<Guid>() ?? Guid.Empty,
            obj["ProfileName"]?.ToObject<string>() ?? string.Empty,
            obj["ProfilePath"]?.ToObject<string>() ?? string.Empty,
            obj["Characters"] is JArray array
                ? array.Select(CustomizePlusCharacterAssociation.Load)
                : []);
        return association;
    }

    public void WriteJson(Utf8JsonWriter j, ReadOnlySpan<byte> propertyName)
    {
        j.WritePropertyName(propertyName);
        WriteJson(j);
    }

    public void WriteJson(Utf8JsonWriter j)
    {
        j.WriteStartObject();
        j.WriteString("ProfileId"u8, ProfileId);
        j.WriteNonEmptyString("ProfileName"u8, ProfileName);
        j.WriteNonEmptyString("ProfilePath"u8, ProfilePath);
        if (_characters.Length > 0)
        {
            j.WriteStartArray("Characters"u8);
            foreach (var character in _characters)
            {
                j.WriteStartObject();
                j.WriteNonEmptyString("Name"u8, character.Name);
                j.WriteNumber("WorldId"u8, character.WorldId);
                j.WriteNumber("CharacterType"u8, character.CharacterType);
                j.WriteNumber("CharacterSubType"u8, character.CharacterSubType);
                j.WriteEndObject();
            }

            j.WriteEndArray();
        }

        j.WriteEndObject();
    }

    public static CustomizePlusAssociation Load(in JsonElement? token)
    {
        var association = new CustomizePlusAssociation();
        if (token is not { } json || json.ValueKind is not JsonValueKind.Object)
            return association;

        association.Update(
            json.TryReadProperty("ProfileId"u8, out Guid? id) ? id.GetValueOrDefault() : Guid.Empty,
            json.PropertyOrDefault("ProfileName"u8, string.Empty),
            json.PropertyOrDefault("ProfilePath"u8, string.Empty),
            LoadCharacters(json));
        return association;
    }

    private static IEnumerable<CustomizePlusCharacterAssociation> LoadCharacters(in JsonElement json)
    {
        if (!json.TryReadArray("Characters"u8, out var array))
            return [];

        List<CustomizePlusCharacterAssociation> characters = [];
        foreach (var entry in array.EnumerateArray())
        {
            if (entry.ValueKind is not JsonValueKind.Object)
                continue;

            characters.Add(new CustomizePlusCharacterAssociation(
                entry.PropertyOrDefault("Name"u8, string.Empty),
                entry.PropertyOrDefault("WorldId"u8, (ushort)0),
                entry.PropertyOrDefault("CharacterType"u8, (byte)0),
                entry.PropertyOrDefault("CharacterSubType"u8, (ushort)0)));
        }

        return characters;
    }
}
