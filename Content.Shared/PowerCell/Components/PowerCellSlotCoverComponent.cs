using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.PowerCell.Components;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedPowerCellSystem))]
[AutoGenerateComponentState]
public sealed partial class PowerCellSlotCoverComponent : Component
{
    /// <summary>
    /// The actual item-slot protected by the cover. Allows all the interaction logic to be handled by <see cref="SharedPowerCellSystem"/>.
    /// </summary>
    [DataField("cellSlotId", required: true)]
    [AutoNetworkedField]
    public string CellSlotId = string.Empty;

    /// <summary>
    ///     Determines if the cover is opened/closed
    /// cell and recharge it separately.
    /// </summary>
    [DataField("coverState")]
    [AutoNetworkedField]
    public PowerCellCoverState CoverState = PowerCellCoverState.Closed;

    /// <summary>
    ///     Determines how many seconds it takes to pry open/close the cover
    /// </summary>
    [DataField("coverPryingDelay")]
    [AutoNetworkedField]
    public TimeSpan CoverPryingDelay = TimeSpan.FromSeconds(2f);

    /// <summary>
    ///     Determines if the cover can be opened/closed
    /// </summary>
    /// <remarks>
    ///     If disabled, the state of the lock is not displayed on the examination tooltip
    /// </remarks>
    [DataField("lockState")]
    [AutoNetworkedField]
    public PowerCellCoverLockState LockState = PowerCellCoverLockState.Disabled;
}

[Serializable, NetSerializable]
public enum PowerCellCoverState : byte
{
    Closed,
    Open
}

[Serializable, NetSerializable]
public enum PowerCellCoverLockState : byte
{
    Disabled,
    Engaged,
    Disengaged
}
