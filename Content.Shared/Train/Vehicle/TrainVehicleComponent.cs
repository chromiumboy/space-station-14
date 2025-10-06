using Content.Shared.Atmos;
using Content.Shared.FixedPoint;
using Content.Shared.Train.Track;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;

namespace Content.Shared.Train.Vehicle;

/// <summary>
/// Data for entities that are train vehicles.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(new[] { typeof(SharedTrainVehicleSystem), typeof(TrainTrackSystem) })]
public sealed partial class TrainVehicleComponent : Component, IGasMixtureHolder
{
    /// <summary>
    /// This container holds any entities being transported by the vehicle.
    /// </summary>
    [DataField]
    public Container? Container;

    /// <summary>
    /// Sets how fast the vehicle moves (~ tiles per second).
    /// </summary>
    [DataField]
    public float TraversalSpeed { get; set; } = 5f;

    /// <summary>
    /// Multiplier for how fast the vehicle moves when derailed.
    /// </summary>
    [DataField]
    public float DerailmentSpeedMultiplier { get; set; } = 1f;

    /// <summary>
    /// Multiplier for how far the vehicle moves when derailed.
    /// </summary>
    [DataField]
    public float ExitDistanceMultiplier { get; set; } = 1f;

    /// <summary>
    /// The train track the vehicle is moving along.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? CurrentTube { get; set; }

    /// <summary>
    /// The train track the vehicle is moving towards.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? NextTube { get; set; }

    /// <summary>
    /// The current direction the vehicle is moving.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Direction CurrentDirection { get; set; } = Direction.Invalid;

    /// <summary>
    /// The atmosphere onboard the vehicle.
    /// </summary>
    [DataField, AutoNetworkedField]
    public GasMixture Air { get; set; } = new(70);

    /// <summary>
    /// Tracks the number of times the vehicle changes direction
    /// </summary>
    [DataField]
    public int DirectionChangeCount;

    /// <summary>
    /// After <see cref="DirectionChangeCount"/> exceeds this value,
    /// the vehicle chance of derailing the each time it changes direction
    /// (as set by <see cref="DerailmentChance"/>).
    /// </summary>
    [DataField]
    public int? DerailmentThreshold = null;

    /// <summary>
    /// The chance the vehicle has of derailing.
    /// </summary>
    [DataField]
    public float DerailmentChance = 0.2f;

    /// <summary>
    /// Sets how many seconds mobs will be stunned if their vehicle derails.
    /// </summary>
    [DataField]
    public TimeSpan DerailmentStunDuration = TimeSpan.FromSeconds(1.5f);

    /// <summary>
    /// The amount of damage entities sustain if their vehicle derails.
    /// </summary>
    [DataField]
    public FixedPoint2 DerailmentDamage = 0;
}

/// <summary>
/// Event raised when determining which direction a train vehicle should move next.
/// </summary>
/// <param name="Holder">The vehicle.</param>
[ByRefEvent]
public record struct GetTrainVehicleNextDirectionEvent(Entity<TrainVehicleComponent> Vehicle)
{
    public Direction Next;
}
