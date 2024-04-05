using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.Containers;

[RegisterComponent, NetworkedComponent]
[Access(typeof(GenericModuleSystem))]
public sealed partial class GenericModuleComponent : Component
{
    /// <summary>
    /// The entity this module is installed into
    /// </summary>
    [DataField]
    public EntityUid? InstalledEntity { get; set; }

    /// <summary>
    /// Returns whether the module is currently installed
    /// </summary>
    public bool Installed => InstalledEntity != null;

    /// <summary>
    /// Sound that plays when the module is installed
    /// </summary>
    [DataField]
    public SoundSpecifier? InsertSound { get; set; }

    /// <summary>
    /// Sound that plays when the module is removed
    /// </summary>
    [DataField]
    public SoundSpecifier? EjectSound { get; set; }
}

/// <summary>
/// Raised on a module when it is installed in order to add specific behavior to an entity.
/// </summary>
[ByRefEvent]
public readonly record struct GenericModuleInstalledEvent(EntityUid Owner);

/// <summary>
/// Raised on a module when it's uninstalled in order to add specific behavior to an entity.
/// </summary>
[ByRefEvent]
public readonly record struct GenericModuleUninstalledEvent(EntityUid Owner);
