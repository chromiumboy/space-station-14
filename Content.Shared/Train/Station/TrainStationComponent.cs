using Content.Shared.Atmos;
using Content.Shared.DoAfter;
using Content.Shared.Train.Vehicle;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Train.Station;

/// <summary>
/// Stopping points for entities with <see cref="TrainVehicleComponent"/>. They will automatically
/// transfer any entities they contain into vehicles that depart them.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause]
public sealed partial class TrainStationComponent : Component
{
    /// <summary>
    /// Air contained in the disposal unit.
    /// </summary>
    [DataField]
    public GasMixture Air = new(Atmospherics.CellVolume);

    /// <summary>
    /// Container of entities currently inside this station.
    /// </summary>
    [DataField]
    public Container? Container;

    /// <summary>
    /// The max number of entities that can wait inside the station.
    /// </summary>
    [DataField]
    public int MaxCapacity = 30;

    /// <summary>
    /// Blacklists (prevents) entities listed from being placed inside the station.
    /// </summary>
    [DataField]
    public EntityWhitelist? Blacklist;

    /// <summary>
    /// Whitelists (allows) entities listed to be being placed inside the station.
    /// </summary>
    [DataField]
    public EntityWhitelist? Whitelist;

    /// <summary>
    /// Last time that an entity tried to exit the station.
    /// </summary>
    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan LastExitAttempt;

    /// <summary>
    /// Delay in seconds before entities are allowed another
    /// attempt to exit the station again.
    /// </summary>
    [DataField]
    public TimeSpan ExitAttemptDelay = TimeSpan.FromSeconds(0.5);

    /// <summary>
    /// Delay for entities trying to enter the station themselves.
    /// </summary>
    [DataField]
    public float EntryDelay = 0.5f;

    /// <summary>
    /// Delay from trying to drag someone else into the station.
    /// </summary>
    [DataField]
    public float DraggedEntryDelay = 2.0f;
}

/// <summary>
/// Do after raised when attempting to insert an entity into a train station.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class TrainStationDoAfterEvent : SimpleDoAfterEvent;
