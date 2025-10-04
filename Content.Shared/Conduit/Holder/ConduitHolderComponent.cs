using Content.Shared.Atmos;
using Content.Shared.FixedPoint;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Conduit.Holder;

/// <summary>
/// Holder of <see cref="ConduitHeldComponent"/> entities being transported through a conduit.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(new[] { typeof(SharedConduitSystem), typeof(SharedConduitHolderSystem) })]
public sealed partial class ConduitHolderComponent : Component, IGasMixtureHolder
{
    /// <summary>
    /// The container that holds any entities being transported.
    /// </summary>
    [DataField]
    public Container? Container;

    /// <summary>
    /// Sets how fast the holder traverses conduits (~ number of tiles per second).
    /// </summary>
    [DataField]
    public float TraversalSpeed { get; set; } = 5f;

    /// <summary>
    /// Multiplier for how fast contained entities are ejected from a conduit.
    /// </summary>
    [DataField]
    public float ExitSpeedMultiplier { get; set; } = 1f;

    /// <summary>
    /// Multiplier for how far contained entities are ejected from a conduit.
    /// </summary>
    [DataField]
    public float ExitDistanceMultiplier { get; set; } = 1f;

    /// <summary>
    /// The conduit the holder is currently exiting.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? CurrentConduit { get; set; }

    /// <summary>
    /// The conduit the holder is moving towards.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? NextConduit { get; set; }

    /// <summary>
    /// The current direction the holder is moving.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Direction CurrentDirection { get; set; } = Direction.Invalid;

    /// <summary>
    /// Is set when the holder is leaving the conduit system.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsExiting { get; set; } = false;

    /// <summary>
    /// A list of tags attached to the holder. Used for routing.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<string> Tags { get; set; } = new();

    /// <summary>
    /// The gas mixture contained in the holder.
    /// </summary>
    [DataField, AutoNetworkedField]
    public GasMixture Air { get; set; } = new(70);

    /// <summary>
    /// A dictionary containing the number of times the holder has passed through
    /// specific tubes. If the number of visits exceeds <see cref="TubeVisitThreshold"/>,
    /// the holder has a chance to break free of the disposal system, as set by
    /// <see cref="TubeEscapeChance"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<EntityUid, int> TubeVisits = new();

    /// <summary>
    /// The number of times the holder can pass through a tube before it has
    /// a chance to escape the disposals system.
    /// </summary>
    [DataField]
    public int TubeVisitThreshold = 1;

    /// <summary>
    /// The chance of the holder escaping the disposals system once the
    /// number of times it passes through the same pipe exceeds.
    /// <see cref="TubeVisitThreshold"/>.
    /// </summary>
    [DataField]
    public float TubeEscapeChance = 0.2f;

    /// <summary>
    /// Sets how many seconds mobs will be stunned after being ejected from a conduit.
    /// </summary>
    [DataField]
    public TimeSpan ExitStunDuration = TimeSpan.FromSeconds(1.5f);

    /// <summary>
    /// The amount of damage that has been accumulated from changing directions.
    /// </summary>
    [DataField]
    public FixedPoint2 AccumulatedDamage = 0;

    /// <summary>
    /// Sets the maximum amount of damage that contained entities can suffer.
    /// from changing directions.
    /// </summary>
    [DataField]
    public FixedPoint2 MaxAllowedDamage = 50;

    /// <summary>
    /// Effect that gets played when the holder is to be deleted.
    /// </summary>
    [DataField]
    public EntProtoId? DespawnEffect;
}
