using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;

namespace Content.Shared.Containers;

[RegisterComponent, NetworkedComponent]
[Access(typeof(GenericModuleSystem))]
public sealed partial class GenericModuleReceiverComponent : Component
{
    /// <summary>
    /// A whitelist for what types of modules can be installed into this device
    /// </summary>
    [DataField]
    public EntityWhitelist? ModuleWhitelist;

    /// <summary>
    /// How many modules can be installed in this device
    /// </summary>
    [DataField]
    public int MaxModules = 3;

    /// <summary>
    /// The ID for the module container
    /// </summary>
    [DataField]
    public string ModuleContainerId = "modules";

    [ViewVariables(VVAccess.ReadOnly)]
    public Container ModuleContainer = default!;

    [ViewVariables(VVAccess.ReadOnly)]
    public int ModuleCount => ModuleContainer.ContainedEntities.Count;
}
