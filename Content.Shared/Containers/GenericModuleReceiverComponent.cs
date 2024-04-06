using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared.Containers;

[RegisterComponent, NetworkedComponent]
[Access(typeof(GenericModuleSystem))]
public sealed partial class GenericModuleReceiverComponent : Component
{
    /// <summary>
    /// A whitelist for what types of modules can be installed into this device
    /// </summary>
    [DataField(required: true)]
    public EntityWhitelist ModuleWhitelist { get; set; } = new();

    /// <summary>
    /// How many modules can be installed in this device
    /// </summary>
    [DataField]
    public int MaxModules { get; set; } = 3;

    /// <summary>
    /// The ID for the module container
    /// </summary>
    [DataField]
    public string ModuleContainerId { get; set; } = "modules";

    /// <summary>
    /// The module container
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public Container ModuleContainer { get; set; } = default!;

    /// <summary>
    /// The number of entities installed on the device
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public int ModuleCount => ModuleContainer.ContainedEntities.Count;

    /// <summary>
    /// A white listed tag cannot be present on more than one installed module
    /// </summary>
    [DataField]
    public bool NoDuplicateWhitelistTags = true;
}

/// <summary>
/// Raised on a generic module receiver when it is examined
/// </summary>
public sealed class GenericModuleReceiverExamineEvent : EntityEventArgs
{
    public FormattedMessage Message;

    public GenericModuleReceiverExamineEvent(ref FormattedMessage message)
    {
        Message = message;
    }
}
