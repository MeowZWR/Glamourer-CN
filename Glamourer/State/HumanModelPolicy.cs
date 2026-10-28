using Dalamud.Game.ClientState.Objects.Enums;
using Penumbra.GameData.Actors;
using Penumbra.GameData.Enums;

namespace Glamourer.State;

public static class HumanModelPolicy
{
    public static bool AllowsHumanModelOverride(in ActorIdentifier identifier)
        => identifier.Kind is ObjectKind.Companion
         || identifier is { Type: IdentifierType.Owned, Kind: ObjectKind.BattleNpc };
}
