namespace Content.Shared.Light.Components;

/// <summary>
/// Attached to <see cref="TileEmissionComponent"/> entities that require
/// power from an APC to emit light.
/// </summary>
[RegisterComponent]
public sealed partial class TileEmissionRequiresPowerComponent : Component;
