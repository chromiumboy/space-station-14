using Content.Shared.Access;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Turrets;

/// <summary>
/// Attached to turrets that deploy with an accompanying animation
/// </summary>
public abstract partial class SharedPopupTurretComponent : Component
{
    /// <summary>
    /// The length of the deployment animation (in seconds)
    /// </summary>
    [DataField]
    public float DeploymentLength = 1.19f;

    /// <summary>
    /// The length of the retraction animation (in seconds)
    /// </summary>
    [DataField]
    public float RetractionLength = 1.19f;
}

[Serializable, NetSerializable]
public enum PopupTurretVisuals : byte
{
    Turret,
    Weapon,
}

[Serializable, NetSerializable]
public enum PopupTurretVisualLayers : byte
{
    Turret,
    Weapon,
}

[Serializable, NetSerializable]
public enum PopupTurretVisualState : byte
{
    Retracted,
    Deploying,
    Deployed,
    Retracting,
}
