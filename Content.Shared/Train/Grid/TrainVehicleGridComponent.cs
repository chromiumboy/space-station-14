using Robust.Shared.GameStates;

namespace Content.Shared.Train.Grid;

[RegisterComponent, NetworkedComponent]
[Access(typeof(TrainVehicleGridSystem))]
public sealed partial class TrainVehicleGridComponent : Component
{
}
