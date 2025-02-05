using Robust.Shared.Audio;
using Robust.Shared.Serialization;

namespace Content.Shared.Turrets;

/// <summary>
/// Attached to turrets that deploy with an accompanying animation
/// </summary>
public abstract partial class SharedDeployableTurretComponent : Component
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

    /// <summary>
    /// The time that the current animation should complete (in seconds)
    /// </summary>
    [DataField]
    public TimeSpan AnimationCompletionTime = TimeSpan.Zero;

    /// <summary>
    /// Sound to play when the denied access to the turret.
    /// </summary>
    [DataField]
    public SoundSpecifier AccessDeniedSound = new SoundPathSpecifier("/Audio/Machines/custom_deny.ogg");

    /// <summary>
    /// Sound to play when the turret deploys.
    /// </summary>
    [DataField]
    public SoundSpecifier DeploymentSound = new SoundPathSpecifier("/Audio/Machines/blastdoor.ogg");

    /// <summary>
    /// Sound to play when the turret retracts.
    /// </summary>
    [DataField]
    public SoundSpecifier RetractionSound = new SoundPathSpecifier("/Audio/Machines/blastdoor.ogg");
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
