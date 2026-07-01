namespace Content.Server.TransitTube;

[RegisterComponent]
[Access(typeof(TransitTubeSystem))]
public sealed partial class TransitTubeComponent : Component
{
    [DataField]
    public Color NormalLightingColor = Color.White;

    [DataField]
    public Color WarningLightingColor = Color.FromHex("#ff4d4d");
}
