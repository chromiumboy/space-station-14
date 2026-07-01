using Robust.Shared.GameStates;

namespace Content.Shared.Train.Vehicle;

/// <summary>
/// The component of the train that provides movement.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
//[Access(typeof(TrainVehicleLocomotorSystem))]
public sealed partial class TrainVehicleLocomotorComponent : Component
{
    /// <summary>
    /// The current direction the vehicle is moving.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Direction CurrentDirection { get; set; } = Direction.Invalid;

    /// <summary>
    /// Tracks the number of times the vehicle changes direction
    /// </summary>
    [DataField]
    public int DirectionChangeCount;

    /// <summary>
    /// The train track the vehicle is moving along.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? CurrentTrack { get; set; }

    /// <summary>
    /// The train track the vehicle is moving towards.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? NextTrack { get; set; }

    [DataField, AutoNetworkedField]
    public EntityUid TrainBody;

}

/// <summary>
/// Event raised when determining which direction a train vehicle locomotor should move next.
/// </summary>
/// <param name="Locomotor">The locomotor.</param>
[ByRefEvent]
public record struct GetTrainVehicleLocomotorNextDirectionEvent(Entity<TrainVehicleLocomotorComponent> Locomotor)
{
    /// <summary>
    /// Array of potential directions the locomotor could move in.
    /// </summary>
    public Direction[] Possibilities = { Direction.Invalid };

    /// <summary>
    /// The direction that has been selected.
    /// </summary>
    public Direction Next = Direction.Invalid;
}
