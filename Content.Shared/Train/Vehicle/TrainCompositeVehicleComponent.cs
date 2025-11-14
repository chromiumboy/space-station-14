using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Train.Vehicle;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TrainCompositeVehicleComponent : Component
{
    [DataField(required: true)]
    public EntProtoId JointPrototype;

    [DataField(required: true)]
    public ResPath TrainGridPath;

    [DataField]
    public int JointBaseLenght = 2;

    [DataField]
    public string ForwardLocomotorJoint = "FowardLocomotorJoint";

    [DataField]
    public string RearLocomotorJoint = "RearLocomotorJoint";

    [DataField]
    public string TrainGridJoint = "TrainGridJoint";

    [DataField, AutoNetworkedField]
    public EntityUid ForwardLocomotorJointUid;

    [DataField, AutoNetworkedField]
    public EntityUid RearLocomotorJointUid;

    [DataField, AutoNetworkedField]
    public EntityUid GridUid;
}
