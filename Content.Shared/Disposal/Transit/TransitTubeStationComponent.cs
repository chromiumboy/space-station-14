using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Disposal.Transit;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedTransitTubeStationSystem))]
public sealed partial class TransitTubeStationComponent : Component
{
    /// <summary>
    /// Sound to play when the station opens.
    /// </summary>
    [DataField]
    public SoundSpecifier OpeningSound = new SoundPathSpecifier("/Audio/Machines/blastdoor.ogg");

    /// <summary>
    /// Sound to play when the station closes.
    /// </summary>
    [DataField]
    public SoundSpecifier ClosingSound = new SoundPathSpecifier("/Audio/Machines/blastdoor.ogg");

    /// <summary>
    /// The current state of the station.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TransitTubeStationState CurentState = TransitTubeStationState.Closed;

    /// <summary>
    /// The current visual state of the station.
    /// </summary>
    [DataField]
    public TransitTubeStationState VisualState = TransitTubeStationState.Closed;

    /// <summary>
    /// The visual state to use when the station is open.
    /// </summary>
    [DataField]
    public string OpenState = "terminus_open";

    /// <summary>
    /// The visual state to use when the station is closed.
    /// </summary>
    [DataField]
    public string ClosedState = "terminus_closed";

    /// <summary>
    /// The visual state to use when the station is opening.
    /// </summary>
    [DataField]
    public string OpeningState = "terminus_opening";

    /// <summary>
    /// The visual state to use when the station is closing.
    /// </summary>
    [DataField]
    public string ClosingState = "terminus_closing";

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

[Serializable, NetSerializable]
public enum TransitTubeStationState
{
    Closed = 0,
    Open = (1 << 0),
    Closing = (1 << 1),
    Opening = (1 << 1) | Open,
}

[Serializable, NetSerializable]
public enum TransitTubeStationVisuals
{
    Base
}

