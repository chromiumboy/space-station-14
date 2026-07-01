using Robust.Shared.GameStates;

namespace Content.Shared.Train.Vehicle;

/// <summary>
/// When combined with a <see cref="TrainVehicleComponent"/>, a train will automatically
/// move between stations, lingering at each one for a short while.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(TrainAutomatedSystem))]
public sealed partial class TrainAutomatedComponent : Component
{
    /// <summary>
    /// Sets whether the train should automatically board entities
    /// stored in a station.
    /// </summary>
    [DataField]
    public bool AutoBoard = true;

    /// <summary>
    /// Sets the number of seconds the train will linger at stations
    /// before automatically taking anything stored at the station.
    /// </summary>
    [DataField]
    public TimeSpan DepartureDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Sets the number of seconds the train will linger at stations
    /// before automatically departing.
    /// </summary>
    [DataField]
    public TimeSpan BoardingDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The time at which the train will next initiate boarding.
    /// </summary>
    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan? NextBoardingTime { get; set; } = null;

    /// <summary>
    /// The time at which the train will next depart a station.
    /// </summary>
    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan? NextDepartureTime { get; set; } = null;
}

/// <summary>
/// Raised on entities vehicles that detrained.
/// </summary>
/// <param name="Vehicle">The departed vehicle.</param>
[ByRefEvent]
public record struct TrainAutomatedBoardingEvent(Entity<TrainAutomatedComponent> Train);

/// <summary>
/// Raised on entities vehicles that detrained.
/// </summary>
/// <param name="Vehicle">The departed vehicle.</param>
[ByRefEvent]
public record struct TrainAutomatedDepartingEvent(Entity<TrainAutomatedComponent> Train);
