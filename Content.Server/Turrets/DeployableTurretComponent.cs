using Content.Shared.Damage.Prototypes;
using Content.Shared.Turrets;
using Robust.Shared.Prototypes;

namespace Content.Server.Turrets;

/// <summary>
/// Attached to turrets that deploy with an accompanying animation
/// </summary>
[RegisterComponent, Access(typeof(DeployableTurretSystem))]
public sealed partial class DeployableTurretComponent : SharedDeployableTurretComponent
{
    /// <summary>
    /// Determines whether the turret is currently active
    /// </summary>
    [DataField]
    public bool Enabled = false;

    /// <summary>
    /// Indicates whether the turret is currently broken
    /// </summary>
    [DataField]
    public bool Broken = false;

    /// <summary>
    /// The physics fixture that will have its collisions disabled when the turret is retracted.
    /// </summary>
    [DataField]
    public string? DeployedFixture = "turret";

    /// <summary>
    /// When retracted, the following damage modifier set will be applied to the turret.
    /// </summary>
    [DataField]
    public ProtoId<DamageModifierSetPrototype>? RetractedDamageModifierSetId;

    /// <summary>
    /// When deployed, the following damage modifier set will be applied to the turret.
    /// </summary>
    [DataField]
    public ProtoId<DamageModifierSetPrototype>? DeployedDamageModifierSetId;
}
