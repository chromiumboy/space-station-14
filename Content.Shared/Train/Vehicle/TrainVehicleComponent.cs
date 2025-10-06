using Content.Shared.Atmos;
using Content.Shared.Damage;
using Content.Shared.Train.Track;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;

namespace Content.Shared.Train.Vehicle;

/// <summary>
/// Data for entities that are train vehicles.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(new[] { typeof(SharedTrainVehicleSystem), typeof(SharedTrainTrackSystem) })]
public sealed partial class TrainVehicleComponent : Component, IGasMixtureHolder
{
    /// <summary>
    /// This container holds any entities being transported by the vehicle.
    /// </summary>
    [DataField]
    public Container? Container;

    /// <summary>
    /// The station that this vehicle is currently stopped at.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? CurrentStation;

    /// <summary>
    /// Sets the max speed at which the vehicle can move (~ tiles per second).
    /// </summary>
    [DataField]
    public float TraversalSpeed { get; set; } = 5f;

    /// <summary>
    /// Sets the vehicles current speed (~ tiles per second).
    /// </summary>
    /// <remarks>
    /// Limited by <see cref="TraversalSpeed"/>.
    /// </remarks>
    [DataField, AutoNetworkedField]
    public float CurrentSpeed { get; set; }

    /// <summary>
    /// Sets whether the vehicle will automatically move forward at max speed.
    /// </summary>
    [DataField]
    public bool Automatic { get; set; }

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
    public EntityUid? CurrentTrack { get; set; }

    /// <summary>
    /// The train track the vehicle is moving towards.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? NextTrack { get; set; }

    /// <summary>
    /// The current direction the vehicle is moving.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Direction CurrentDirection { get; set; } = Direction.Invalid;

    /// <summary>
    /// Sets whether the vehicle is airthight or not.
    /// </summary>
    [DataField]
    public bool Airtight = false;

    /// <summary>
    /// The atmosphere onboard the vehicle.
    /// </summary>
    [DataField]
    public GasMixture Air { get; set; } = new(70);

    /// <summary>
    /// Tracks the number of times the vehicle changes direction
    /// </summary>
    [DataField]
    public int DirectionChangeCount;

    /// <summary>
    /// After <see cref="DirectionChangeCount"/> exceeds this value, the
    /// vehicle has a chance of derailing the each time it changes direction
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
    /// Sets whether the vehicle is currently derailed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsDerailed = false;

    /// <summary>
    /// Sets how many seconds mobs will be stunned if thrown from a derailed vehicle.
    /// </summary>
    [DataField]
    public TimeSpan DerailmentStunDuration = TimeSpan.FromSeconds(1.5f);

    /// <summary>
    /// The amount of damage entities sustain if thrown from a derailed vehicle.
    /// </summary>
    [DataField]
    public DamageSpecifier DerailmentDamage = new()
    {
        DamageDict = new()
        {
            { "Blunt", 0.0 },
        }
    };

    /// <summary>
    /// Sets whether the vehicle should eject its contents on derailment.
    /// </summary>
    [DataField]
    public bool EjectContentsOnDerailment = false;
}

/// <summary>
/// Raised on train vehicles that are just about to derail.
/// </summary>
/// <param name="Vehicle">The vehicle.</param>
[ByRefEvent]
public record struct BeforeTrainVehicleDerailmentEvent();

/// <summary>
/// Raised on train vehicles that have been derailed.
/// </summary>
/// <param name="Vehicle">The vehicle.</param>
[ByRefEvent]
public record struct AfterTrainVehicleDerailmentEvent();

/// <summary>
/// Event raised when determining which direction a train vehicle should move next.
/// </summary>
/// <param name="Vehicle">The vehicle.</param>
/// <param name="Direction">The direction the vehicle is heading.</param>
[ByRefEvent]
public record struct GetTrainVehicleNextDirectionEvent(Entity<TrainVehicleComponent> Vehicle)
{
    /// <summary>
    /// Array of potential directions the vehicle could move in.
    /// </summary>
    public Direction[] Possibilities = { Direction.Invalid };

    /// <summary>
    /// The direction that has been selected.
    /// </summary>
    public Direction Next = Direction.Invalid;
}
