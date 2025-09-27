using Robust.Shared.GameStates;

namespace Content.Shared.Light.Components;

/// <summary>
/// Will draw lighting in a range around the tile.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TileEmissionComponent : Component
{
    /// <summary>
    /// How far the emitted light spreads.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Range = 0.25f;

    /// <summary>
    /// The color of the emitted light.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public Color Color = Color.Transparent;

    /// <summary>
    /// Sets whether the light should be on/off.
    /// </summary>
    [DataField]
    public bool Enabled = true;
}
