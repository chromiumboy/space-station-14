using Robust.Client.Animations;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Turrets;

[RegisterComponent, NetworkedComponent]
public sealed partial class PopupTurretComponent : Component
{
    [ViewVariables]
    public PopupTurretVisualState CurrentState = PopupTurretVisualState.Retracted;

    /// <summary>
    /// The key used to index the (de)activation animations played when turning the turret on/off.
    /// </summary>
    [ViewVariables]
    public const string AnimationKey = "popup_turret_animation";

    /// <summary>
    /// Used to build the <value cref="DeploymentAnimation">activation animation</value> when the component is initialized.
    /// </summary>
    [DataField]
    public string DeployedState = "cover_open";

    /// <summary>
    /// Used to build the <see cref="RetractionAnimation">deactivation animation</see> when the component is initialized.
    /// </summary>
    [DataField]
    public string RetractedState = "cover_closed";

    /// <summary>
    /// Used to build the <value cref="DeploymentAnimation">activation animation</value> when the component is initialized.
    /// </summary>
    [DataField]
    public string DeployingState = "cover_opening";

    /// <summary>
    /// Used to build the <see cref="RetractionAnimation">deactivation animation</see> when the component is initialized.
    /// </summary>
    [DataField]
    public string RetractingState = "cover_closing";

    [DataField]
    public float DeploymentAnimLength = 1.1f;

    [DataField]
    public float RetractionAnimLength = 1.1f;

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
    State,
}

[Serializable, NetSerializable]
public enum PopupTurretVisualLayers : byte
{
    Cover,
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
    Stun,
    Lethal,
}
