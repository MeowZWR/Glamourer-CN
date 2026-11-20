using System.Text.Json;
using Dalamud.Game.ClientState.Objects.Enums;
using Glamourer.Designs;
using Glamourer.Designs.History;
using Luna;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Enums;

namespace Glamourer.Automation;

public static class CompanionDesignRules
{
    public const ApplicationType Required  = ApplicationType.Customizations;
    public const ApplicationType Forbidden = ApplicationType.Weapons | ApplicationType.CustomizePlusProfile;

    public static bool HasAllCustomizeRules(IDesignStandIn standIn)
    {
        if (standIn is not Design design || !design.GetDesignDataRef().IsHuman)
            return false;

        var required = design.CustomizeSet.SettingAvailable
          | CustomizeFlag.Clan
          | CustomizeFlag.Gender
          | CustomizeFlag.BodyType;
        return (design.Application.Customize & required) == required;
    }

    public static ApplicationType Normalize(ApplicationType type)
        => (type | Required) & ~Forbidden;

    public static bool ChangesCustomizeSet(ITransaction? transaction)
        => transaction is CustomizeTransaction
        {
            Slot: Penumbra.GameData.Enums.CustomizeIndex.Race
                or Penumbra.GameData.Enums.CustomizeIndex.Clan
                or Penumbra.GameData.Enums.CustomizeIndex.Gender,
        };
}

public sealed record CompanionTarget(ObjectKind Kind, uint Id, string Name, IReadOnlySet<uint> Ids)
{
    public bool Matches(in ActorIdentifier identifier)
        => Kind == identifier.Kind && Ids.Contains(identifier.DataId.Id);

    public bool SameAs(CompanionTarget? other)
        => other is not null && Kind == other.Kind && Name == other.Name;
}

public enum CompanionLimitKind : byte
{
    Unset,
    AllPets,
    Target,
}

public readonly record struct CompanionLimit(CompanionLimitKind Kind, CompanionTarget? Target)
{
    public static CompanionLimit Unset { get; } = new(CompanionLimitKind.Unset, null);

    public static CompanionLimit AllPets { get; } = new(CompanionLimitKind.AllPets, null);

    public bool Matches(in ActorIdentifier identifier)
        => Kind switch
        {
            CompanionLimitKind.AllPets => identifier.Kind is ObjectKind.Companion,
            CompanionLimitKind.Target  => Target!.Matches(identifier),
            _                          => false,
        };

    public bool SameAs(CompanionLimit other)
        => Kind == other.Kind && (Kind is not CompanionLimitKind.Target || Target!.SameAs(other.Target));
}

public sealed class CompanionAutoSet(AutoDesignSet set)
{
    public readonly AutoDesignSet Set = set;

    public readonly Dictionary<AutoDesign, CompanionLimit> Limits = [];

    public CompanionLimit GetLimit(AutoDesign design)
        => Limits.GetValueOrDefault(design);

    public IEnumerable<CompanionTarget> Targets
        => Set.Designs.Select(GetLimit).Where(l => l.Kind is CompanionLimitKind.Target).Select(l => l.Target!).DistinctBy(t => (t.Kind, t.Name));

    public bool Matches(in ActorIdentifier identifier)
    {
        foreach (var design in Set.Designs)
        {
            if (GetLimit(design).Matches(identifier))
                return true;
        }

        return false;
    }

    public AutoDesignSet? CreateView(in ActorIdentifier identifier)
    {
        var designs = new List<AutoDesign>();
        foreach (var design in Set.Designs)
        {
            if (!GetLimit(design).Matches(identifier) || !CompanionDesignRules.HasAllCustomizeRules(design.Design))
                continue;

            var row        = design;
            var normalized = CompanionDesignRules.Normalize(row.Type);
            if (row.Type != normalized)
            {
                row      = design.Clone();
                row.Type = normalized;
            }

            designs.Add(row);
        }

        if (designs.Count is 0)
            return null;

        return new AutoDesignSet(Set.Name, [], designs)
        {
            Enabled                = true,
            BaseState              = Set.BaseState,
            ResetTemporarySettings = Set.ResetTemporarySettings,
        };
    }

    public void Serialize(Utf8JsonWriter j)
    {
        j.WriteStartObject();
        j.WriteNonEmptyString("Name"u8, Set.Name);
        j.WriteIfNot("Enabled"u8, Set.Enabled, false);
        j.WriteEnumIfNot("BaseState"u8, Set.BaseState, AutoDesignSet.Base.Current);
        j.WriteIfNot("ResetTemporarySettings"u8, Set.ResetTemporarySettings, false);
        if (Set.Designs.Count > 0)
        {
            j.WriteStartArray("Rows"u8);
            foreach (var design in Set.Designs)
            {
                j.WriteStartObject();
                var limit = GetLimit(design);
                if (limit.Kind is CompanionLimitKind.AllPets)
                    j.WriteBoolean("AllPets"u8, true);
                else if (limit.Target is { } target)
                {
                    j.WriteEnumIfNot("Kind"u8, target.Kind, ObjectKind.None);
                    j.WriteNumber("Id"u8, target.Id);
                }

                j.WritePropertyName("Design"u8);
                design.Serialize(j);
                j.WriteEndObject();
            }

            j.WriteEndArray();
        }

        j.WriteEndObject();
    }
}
