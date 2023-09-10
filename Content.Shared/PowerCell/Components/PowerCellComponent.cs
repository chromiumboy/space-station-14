using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.PowerCell;

/// <summary>
///     This component enables power-cell related interactions (e.g., entity white-lists, cell sizes, examine, rigging).
///     The actual power functionality is provided by the server-side BatteryComponent.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PowerCellComponent : Component
{
    public const int PowerCellVisualsLevels = 2;

    [DataField("cellType"), AutoNetworkedField]
    public PowerCellType CellType = PowerCellType.Standard;
}

[Serializable, NetSerializable]
public enum PowerCellVisuals : byte
{
    ChargeLevel
}

[Serializable, NetSerializable]
public enum PowerCellSlotVisuals : byte
{
    Enabled
}

[Serializable, NetSerializable]
public enum PowerCellType : byte
{
    Standard,
    Industrial,
}
