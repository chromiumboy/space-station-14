using Content.Shared.Access;
using Content.Shared.Turrets;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.TurretController;

/// <summary>
/// Attached to entities that can set data on linked turret-based entities
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedDeployableTurretControllerSystem))]
public sealed partial class DeployableTurretControllerComponent : Component
{
    /// <summary>
    /// A list of turrets being directed by this entity, indexed by their device address.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public Dictionary<string, Entity<SharedDeployableTurretComponent>> LinkedTurrets = new();

    /// <summary>
    /// The current armament state of the linked turrets.
    /// [-1: Inactive, 0: weapon mode A, 1: weapon mode B, etc]
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public int ArmamentState = -1;

    /// <summary>
    /// Access levels that are known to the entity.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<AccessLevelPrototype>> AccessLevels = new();

    /// <summary>
    ///Access groups that are known to the entity.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<AccessGroupPrototype>> AccessGroups = new();
}

[Serializable, NetSerializable]
public sealed class DeployableTurretControllerWindowBoundInterfaceState : BoundUserInterfaceState
{
    public List<(string, string)>? TurretStates = null;
    public int? ArmamentState = null;
    public HashSet<ProtoId<AccessLevelPrototype>>? ExemptAccessLevels = null;
}

[Serializable, NetSerializable]
public sealed class DeployableTurretArmamentSettingChangedMessage : BoundUserInterfaceMessage
{
    public int ArmamentState;

    public DeployableTurretArmamentSettingChangedMessage(int armamentState)
    {
        ArmamentState = armamentState;
    }
}

[Serializable, NetSerializable]
public sealed class DeployableTurretExemptAccessLevelChangedMessage : BoundUserInterfaceMessage
{
    public Dictionary<ProtoId<AccessLevelPrototype>, bool> AccessLevels;

    public DeployableTurretExemptAccessLevelChangedMessage(Dictionary<ProtoId<AccessLevelPrototype>, bool> accessLevels)
    {
        AccessLevels = accessLevels;
    }
}

[Serializable, NetSerializable]
public enum TurretControllerVisuals : byte
{
    ControlPanel,
}


[Serializable, NetSerializable]
public enum DeployableTurretControllerUiKey : byte
{
    General,
    StationAi,
}
