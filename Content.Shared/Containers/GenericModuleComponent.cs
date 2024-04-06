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
    public SoundSpecifier? InstallSound { get; set; }

    /// <summary>
    /// Sound that plays when the module is removed
    /// </summary>
    [DataField]
    public SoundSpecifier? UninstallSound { get; set; }

    /// <summary>
    /// Denotes whether the module can be removed by hand once inserted into a device
    /// </summary>
    public bool ManualUninstall { get; set; } = true;

    /// <summary>
    /// Denotes whether the module should be listed on the examination card of the device it is installed on
    /// </summary>
    public bool HiddenOnReceiverExamination { get; set; } = false;
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

/// <summary>
/// Raised on a module when attempting to install it.
/// </summary>
[ByRefEvent]
public readonly record struct GenericModuleInstallAttemptEvent(EntityUid Owner, bool Cancelled = false);

/// <summary>
/// Raised on a module when attempting to uninstall it.
/// </summary>
[ByRefEvent]
public readonly record struct GenericModuleUninstallAttemptEvent(EntityUid Owner, bool Cancelled = false);
