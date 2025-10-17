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
    /// Sets the number of seconds the train will linger at stations before departing.
    /// </summary>
    [DataField]
    public TimeSpan AutomaticDelayAtStations { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The time at which the train will next depart a station.
    /// </summary>
    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan AutomaticDepatureTime { get; set; }
}
