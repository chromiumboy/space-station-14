using Robust.Shared.Prototypes;

namespace Content.Client.TurretControls;

[Prototype("turretControlSetting")]
public sealed partial class TurretControlSettingPrototype : IPrototype
{
    [IdDataField, ViewVariables]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// The name of the setting; used to set the label of the appropriate control
    /// </summary>
    [DataField(required: true)]
    public LocId Name = default!;
}
