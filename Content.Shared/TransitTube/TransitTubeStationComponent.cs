using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.TransitTube;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedTransitTubeStationSystem))]
public sealed partial class TransitTubeStationComponent : Component
{
    /// <summary>
    ///
    /// </summary>
    [DataField]
    public TransitTubePodSpawnCondition TransitTubePodSpawnCondition;

    /// <summary>
    ///
    /// </summary>
    [DataField]
    public EntProtoId? TransitTubePodPrototype;

    /// <summary>
    /// Sound to play when the station opens.
    /// </summary>
    [DataField]
    public SoundSpecifier OpeningSound = new SoundPathSpecifier("/Audio/Machines/windoor_open.ogg");

    /// <summary>
    /// Sound to play when the station closes.
    /// </summary>
    [DataField]
    public SoundSpecifier ClosingSound = new SoundPathSpecifier("/Audio/Machines/windoor_open.ogg");

    /// <summary>
    /// The current state of the station.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TransitTubeStationState CurrentState = TransitTubeStationState.Closed;

    /// <summary>
    /// The current visual state of the station.
    /// </summary>
    [DataField]
    public TransitTubeStationState VisualState = TransitTubeStationState.Closed;

    /// <summary>
    /// The visual state to use when the station is open.
    /// </summary>
    [DataField]
    public string OpenState = "station_open";

    /// <summary>
    /// The visual state to use when the station is closed.
    /// </summary>
    [DataField]
    public string ClosedState = "station_closed";

    /// <summary>
    /// The visual state to use when the station is opening.
    /// </summary>
    [DataField]
    public string OpeningState = "station_opening";

    /// <summary>
    /// The visual state to use when the station is closing.
    /// </summary>
    [DataField]
    public string ClosingState = "station_closing";

    /// <summary>
    /// The length of the opening animation (in seconds)
    /// </summary>
    [DataField]
    public TimeSpan OpeningLength = TimeSpan.FromSeconds(0.5f);

    /// <summary>
    /// The length of the closing animation (in seconds)
    /// </summary>
    [DataField]
    public TimeSpan ClosingLength = TimeSpan.FromSeconds(0.5f);

    /// <summary>
    /// The animation used when the station opens.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public object OpeningAnimation = default!;

    /// <summary>
    /// The animation used when the station closes.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public object ClosingAnimation = default!;
}

/// <summary>
/// State for the transit tube station.
/// </summary>
[Serializable, NetSerializable]
public enum TransitTubeStationState
{
    Closed = 0,
    Open = (1 << 0),
    Closing = (1 << 1),
    Opening = (1 << 1) | Open,
}

/// <summary>
/// Condition required for a transit tube pod to be spawned.
/// </summary>
public enum TransitTubePodSpawnCondition
{
    OnInsert,
    OnDerailment,
}

[Serializable, NetSerializable]
public enum TransitTubeStationVisuals
{
    Key
}

[Serializable, NetSerializable]
public enum TransitTubeStationVisualLayers
{
    Base
}

