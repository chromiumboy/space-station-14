using Content.Shared.Access;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Turrets;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TurretTargetingComponent : Component
{
    /// <summary>
    /// Determines whether the turret will attack cyborgs.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public bool TargetCyborgs = true;

    /// <summary>
    /// Determines whether the turret will attack bots.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public bool TargetBasicSilicons = true;

    /// <summary>
    /// Determines whether the turret will attack xenos and animals.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public bool TargetAnimalsAndXenos = true;

    /// <summary>
    /// Determines whether the turret will attack those who are openly carrying weapons or contraband and are not cleared to do so.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public bool TargetVisibleContraband = true;

    /// <summary>
    /// Determines whether the turret will attack wanted criminals.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public bool TargetWantedCriminals = true;

    /// <summary>
    /// Determines whether the turret will attack crew who do not have sufficent access permissions,
    /// see <see cref="AuthorizedAccessLevels" />. Only applies to humanoids.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public bool TargetUnauthorizedCrew = true;

    /// <summary>
    /// Crew with one or more access privileges from this list are exempt from <see cref="TargetUnauthorizedCrew" />.
    /// </summary>
    /// <remarks>
    /// This list does not have to match the one on the turret's <see cref="AccessReaderComponent.AccessLists" />
    /// (i.e., entities can be authorized to passby but not to interfere with the operation of the turret).
    /// </remarks>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public HashSet<ProtoId<AccessLevelPrototype>> AuthorizedAccessLevels = new();
}
