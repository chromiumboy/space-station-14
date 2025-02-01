using Content.Shared.Access;
using Content.Shared.Power;
using Content.Shared.Turrets;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.TurretControls;

[RegisterComponent, NetworkedComponent]
public sealed partial class TurretControlsComponent : Component
{
    public Dictionary<Entity<TurretTargetingComponent>, string> LinkedTurrets = new();

    [DataField]
    public HashSet<ProtoId<AccessLevelPrototype>> AccessLevels = new();

    [DataField]
    public HashSet<ProtoId<AccessGroupPrototype>> AccessGroups = new();
}

/// <summary>
///     Data from by the server to the client for the power monitoring console UI
/// </summary>
[Serializable, NetSerializable]
public sealed class TurretControlsBoundInterfaceState : BoundUserInterfaceState
{
    public List<(string, string)> TurretStates = new();

    public TurretControlsBoundInterfaceState(List<(string, string)> turretStates)
    {
        TurretStates = turretStates;
    }
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

/// <summary>
/// Sends a list from the client to the server informing the entity of changes to its turret control settings
/// </summary>
[Serializable, NetSerializable]
public sealed class TurretControlArmamentSettingChangedMessage : BoundUserInterfaceMessage
{
    public TurretControlsArmamentState ArmamentState;

    public TurretControlArmamentSettingChangedMessage(TurretControlsArmamentState armamentState)
    {
        ArmamentState = armamentState;
    }
}

/// <summary>
/// Sends a list from the client to the server informing the entity of changes to its turret control settings
/// </summary>
[Serializable, NetSerializable]
public sealed class TurretControlAccessLevelChangedMessage : BoundUserInterfaceMessage
{
    public ProtoId<AccessLevelPrototype> AccessLevel;
    public bool Enabled;

    public TurretControlAccessLevelChangedMessage(ProtoId<AccessLevelPrototype> accessLevel, bool enabled)
    {
        AccessLevel = accessLevel;
        Enabled = enabled;
    }
}

[Serializable, NetSerializable]
public enum TurretControlsArmamentState : byte
{
    Safe = 0,
    Stun = 1,
    Lethal = 2,
}

[Serializable, NetSerializable]
public enum TurretControlsUiKey : byte
{
    General,
    StationAi,
}
