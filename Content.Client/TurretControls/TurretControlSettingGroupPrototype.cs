using Robust.Shared.Prototypes;

namespace Content.Client.TurretControls;

[Prototype("turretControlSettingGroup")]
public sealed partial class TurretControlSettingGroupPrototype : IPrototype
{
    [IdDataField, ViewVariables]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// A list of the setting prototypes that will be used to populate
    /// the turret controls UI
    /// </summary>
    [DataField(required: true)]
    public HashSet<ProtoId<TurretControlSettingPrototype>> Settings = default!;
}
