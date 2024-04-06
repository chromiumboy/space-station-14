using Content.Shared.RCD.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.RCD.Components;

[RegisterComponent, NetworkedComponent]
[Access(typeof(RCDSystem))]
public sealed partial class RCDModuleComponent : Component
{
    /// <summary>
    /// List of RCD prototypes that the device comes loaded with
    /// </summary>
    [DataField]
    public HashSet<ProtoId<RCDPrototype>>? BasePrototypes { get; set; }

    /// <summary>
    /// The number of charges consumed by the device is modified by this multiplier
    /// </summary>
    [DataField]
    public float EfficiencyMultipler { get; set; } = 1f;

    /// <summary>
    /// Adds additional max charges to the RCD
    /// </summary>
    [DataField]
    public int CapacityModifier { get; set; } = 0;
}
