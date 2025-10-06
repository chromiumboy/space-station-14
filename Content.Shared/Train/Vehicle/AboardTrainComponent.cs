namespace Content.Shared.Train.Vehicle;

/// <summary>
/// Attached/removed from entities that have embarked/disembarked
/// a <see cref="TrainVehicleComponent"/>.
/// </summary>
public sealed partial class AboardTrainComponent : Component
{
    [DataField]
    public EntityUid? TrainVehicle = null;
}
