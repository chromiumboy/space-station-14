using Content.Shared.Access;
using Content.Shared.Turrets;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.TurretControls;

[RegisterComponent, NetworkedComponent]
public sealed partial class TurretControlsComponent : Component
{
    public Dictionary<Entity<TurretTargetingComponent>, string> LinkedTurrets = new();
}

/// <summary>
/// Sends a list from the client to the server informing the entity of changes to its turret control settings
/// </summary>
[Serializable, NetSerializable]
public sealed class TurretControlSettingsChangedMessage : BoundUserInterfaceMessage
{
    public TurretControlsArmamentState? ArmamentState;
    public bool? TargetCyborgs;
    public bool? TargetBasicSilicons;
    public bool? TargetAnimalsAndXenos;
    public bool? TargetVisibleContraband;
    public bool? TargetWantedCriminals;
    public bool? TargetUnauthorizedCrew;
    public HashSet<ProtoId<AccessLevelPrototype>>? AuthorizedAccessLevels;
}

[Serializable, NetSerializable]
public enum TurretControlsArmamentState : byte
{
    Safe,
    Stun,
    Lethal,
}

[Serializable, NetSerializable]
public enum TurretControlsUiKey : byte
{
    General,
    StationAi,
}
