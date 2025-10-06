using Content.Shared.NodeContainer;
using Content.Shared.Train.Vehicle;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Train.Track;

/// <summary>
/// Attached to entities that restrict the movement of <see cref="TrainVehicleComponent">
/// entities (e.g., trams, transit pods) to certain directions while passing along them.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(TrainTrackSystem))]
public sealed partial class TrainTrackComponent : Component
{
    /// <summary>
    /// Array of directions that entities passing this track can currently move along.
    /// </summary>
    /// <remarks>
    /// Requires a <see cref="NodeContainerComponent"/> to populate.
    /// </remarks>
    [DataField]
    public Direction[] Directions = { Direction.Invalid };

    /// <summary>
    /// Determines the type of train track
    /// </summary>
    /// <remarks>
    /// Only tracks of the same type will connect to each other.
    /// </remarks>
    [DataField]
    public TrainTrackType TrainTrackType;

    /// <summary>
    /// Sound played when entities on this track change direction.
    /// </summary>
    [DataField]
    public SoundSpecifier? ChangeDirectionsSound;
}

/// <summary>
/// The type of train track.
/// </summary>
/// <remarks>Only train tracks of same type may connect with each other.</remarks>
[Serializable, NetSerializable]
public enum TrainTrackType
{
    TransitTube,
}
