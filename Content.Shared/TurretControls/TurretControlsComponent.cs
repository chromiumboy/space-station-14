using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.TurretControls;

[RegisterComponent, NetworkedComponent]
public sealed partial class TurretControlsComponent : Component
{

}

[Serializable, NetSerializable]
public enum TurretControlsUiKey : byte
{
    Key,
}
