using Content.Shared.Access;
using Content.Shared.TurretController;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Turrets;

/// <summary>
/// Attached to entities to provide them with turret target selection data.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TurretTargetSettingsComponent : Component
{
    /// <summary>
    /// Crew with one or more access levels from this list are exempt from being targeted by turrets.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    [AutoNetworkedField]
    public HashSet<ProtoId<AccessLevelPrototype>> ExemptAccessLevels = new();
}
