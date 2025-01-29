using Content.Shared.Turrets;
using Robust.Client.Animations;
using Robust.Shared.GameStates;

namespace Content.Client.Turrets;

/// <summary>
/// Attached to turrets that deploy with an accompanying animation
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PopupTurretComponent : SharedPopupTurretComponent
{
    /// <summary>
    /// The current visual state of the turret
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public PopupTurretVisualState CurrentState = PopupTurretVisualState.Retracted;

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

    /// <summary>
    /// The key used to index the animation played when turning the turret on/off.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
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
}
