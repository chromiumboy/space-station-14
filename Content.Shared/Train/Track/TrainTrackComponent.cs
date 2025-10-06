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
[Access(typeof(SharedTrainTrackSystem))]
public sealed partial class TrainTrackComponent : Component
{
    /// <summary>
    /// Directions that vehicles passing this track can currently move along.
    /// The dictionary is indexed by the entry direction, and the value is
    /// the possible directions that vehicles can exit. If there are more
    /// than one possible exits, one will be selected at random.
    /// </summary>
    /// <remarks>
    /// Requires a <see cref="NodeContainerComponent"/> to populate.
    /// </remarks>
    [DataField]
    public Dictionary<Direction, Direction[]> Directions = new();

    /// <summary>
    /// Other pieces of track that are connected to this one.
    /// The dirctionary is indexed by the relative directions
    /// these other pieces of track are located.
    /// </summary>
    /// <remarks>
    /// Requires a <see cref="NodeContainerComponent"/> to populate.
    /// </remarks>
    [DataField]
    public Dictionary<Direction, EntityUid> AdjacentTrack = new();


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
