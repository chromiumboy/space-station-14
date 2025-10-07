using Robust.Shared.GameStates;

namespace Content.Shared.Train.Vehicle;

/// <summary>
/// Attached/removed from entities that have embarked/disembarked
/// a <see cref="TrainVehicleComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedTrainVehicleSystem))]
public sealed partial class AboardTrainComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? TrainVehicle = null;
}
