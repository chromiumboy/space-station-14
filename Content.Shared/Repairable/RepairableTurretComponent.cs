using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Repairable;

/// <summary>
/// Denotes that this entity is a repairable turret. Must be paired with <see cref="RepairableComponent"/> to function.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class RepairableTurretComponent : Component
{

}

[Serializable, NetSerializable]
public enum RepairableTurretVisuals : byte
{
    Broken,
}
