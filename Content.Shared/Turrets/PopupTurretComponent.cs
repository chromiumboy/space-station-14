using Robust.Client.Animations;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Turrets;

/// <summary>
/// Attached to turrets that deploy with an accompanying animation
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PopupTurretComponent : Component
{
    /// <summary>
    /// Determines whether the turret is currently active or not
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool Enabled = false;

    /// <summary>
    /// The amount of time (in seconds) to deploy the turret
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float DeploymentTime = 1.2f;

    /// <summary>
    /// The amount of time (in seconds) to retract the turret
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float RetractionTime = 1.2f;

    /// <summary>
    /// The current visual state of the turret
    /// </summary>
    [ViewVariables]
    public PopupTurretVisualState CurrentState = PopupTurretVisualState.Retracted;

    /// <summary>
    /// The key used to index the animation played when turning the turret on/off.
    /// </summary>
    [ViewVariables]
    public const string AnimationKey = "popup_turret_animation";

    /// <summary>
    /// The visual state to use when the turret is deployed.
    /// </summary>
    [DataField]
    public string DeployedState = "cover_open";

    /// <summary>
    /// The visual state to use when the turret is not deployed.
    /// </summary>
    [DataField]
    public string RetractedState = "cover_closed";

    /// <summary>
    /// Used to build the <value cref="DeploymentAnimation">deployment animation</value> when the component is initialized.
    /// </summary>
    [DataField]
    public string DeployingState = "cover_opening";

    /// <summary>
    /// Used to build the <see cref="RetractionAnimation">retraction animation</see> when the component is initialized.
    /// </summary>
    [DataField]
    public string RetractingState = "cover_closing";

    /// <summary>
    /// The length of the deployment animation (in seconds)
    /// </summary>
    [DataField]
    public float DeploymentAnimLength = 1.19f;

    /// <summary>
    /// The length of the retraction animation (in seconds)
    /// </summary>
    [DataField]
    public float RetractionAnimLength = 1.19f;

    /// <summary>
    /// The animation used when turret activates
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public Animation DeploymentAnimation = default!;

    /// <summary>
    /// The animation used when turret deactivates
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public Animation RetractionAnimation = default!;
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

[Serializable, NetSerializable]
public enum PopupTurretWeaponState : byte
{
    Inactive,
    Setting1,
    Setting2,
    Setting3,
}
