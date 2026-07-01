using Robust.Shared.Prototypes;

namespace Content.Shared.TransitTube;

[RegisterComponent]
[Access(typeof(TransitTubePodSystem))]
public sealed partial class TransitTubePodComponent : Component
{
    /// <summary>
    /// Sets whether the entity should despawn after entering a station.
    /// </summary>
    [DataField]
    public bool Permanent = true;

    /// <summary>
    /// Effect played when the entity despawns.
    /// </summary>
    [DataField]
    public EntProtoId? DespawnEffect = "EffectTransitTubePodDisappear";
}
