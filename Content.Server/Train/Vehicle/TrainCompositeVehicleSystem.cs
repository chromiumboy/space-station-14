using Content.Server.Station.Systems;
using Content.Shared.Train.Vehicle;
using Robust.Server.GameObjects;
using Robust.Server.Physics;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using System.Numerics;

namespace Content.Server.Train.Vehicle;

public sealed class TrainCompositeVehicleSystem : SharedTrainCompositeVehicleSystem
{
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly JointSystem _joint = default!;
    [Dependency] private readonly TransformSystem _xform = default!;
    [Dependency] private readonly TrainVehicleLocomotorSystem _locomotor = default!;
    [Dependency] private readonly PhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTrainVehicleSystem _trainVehicle = default!;
    [Dependency] private readonly MapSystem _map = default!;

    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        _xformQuery = GetEntityQuery<TransformComponent>();

        base.Initialize();

        SubscribeLocalEvent<TrainCompositeVehicleComponent, ComponentInit>(OnCompInit);
    }

    private void OnCompInit(Entity<TrainCompositeVehicleComponent> ent, ref ComponentInit args)
    {
        if (TryLoadTrainGrid(ent))
        {
            CreateLocomotors(ent);
        }
        else
        {
            QueueDel(ent);
        }
    }

    public bool TryLoadTrainGrid(Entity<TrainCompositeVehicleComponent> ent)
    {
        var xform = Transform(ent);

        if (!_mapLoader.TryLoadGrid(xform.MapID, ent.Comp.TrainGridPath, out var grid))
            return false;

        var gridXform = Transform(grid.Value);

        //_station.AddGridToStation(stationMember.Station, grid.Value);

        var mapCoordinates = _xform.ToMapCoordinates(xform.Coordinates);
        var mapUid = _map.GetMap(mapCoordinates.MapId);
        _xform.SetCoordinates(grid.Value, gridXform, new EntityCoordinates(mapUid, mapCoordinates.Position), rotation: _xform.GetWorldRotation(xform.Coordinates.EntityId));

        ent.Comp.GridUid = grid.Value;
        //_xform.SetCoordinates(grid.Value, xform.Coordinates);
        //_xform.SetLocalRotation(grid.Value, xform.LocalRotation);

        //_joint.CreateWeldJoint(ent, grid.Value, ent.Comp.TrainGridJoint);

        return true;
    }

    public void CreateLocomotors(Entity<TrainCompositeVehicleComponent> ent)
    {
        if (!TryComp<TrainVehicleComponent>(ent, out var vehicle))
        {
            return;
        }

        var xform = Transform(ent);

        var forwardCoords = xform.Coordinates.Offset(new Vector2(0f, ent.Comp.JointBaseLenght));
        var fowardJoint = SpawnAttachedTo(ent.Comp.JointPrototype, forwardCoords);
        //var fj = _joint.CreateRevoluteJoint(fowardJoint, ent, ent.Comp.ForwardLocomotorJoint);
        //fj.LocalAnchorA = new Vector2(0f, ent.Comp.JointBaseLenght);

        ent.Comp.ForwardLocomotorJointUid = fowardJoint;

        if (TryComp<TrainVehicleLocomotorComponent>(fowardJoint, out var fowardJointComp))
        {
            fowardJointComp.TrainBody = ent;
            _locomotor.TryAttachToTrack((fowardJoint, fowardJointComp), (ent.Owner, vehicle));
        }

        var rearCoords = xform.Coordinates.Offset(new Vector2(0f, -ent.Comp.JointBaseLenght));
        var rearJoint = SpawnAttachedTo(ent.Comp.JointPrototype, rearCoords);
        //var rj = _joint.CreateRevoluteJoint(rearJoint, ent, ent.Comp.RearLocomotorJoint);
        //rj.LocalAnchorA = new Vector2(0f, -ent.Comp.JointBaseLenght);

        ent.Comp.RearLocomotorJointUid = rearJoint;

        if (TryComp<TrainVehicleLocomotorComponent>(rearJoint, out var rearJointComp))
        {
            rearJointComp.TrainBody = ent;
            _locomotor.TryAttachToTrack((rearJoint, rearJointComp), (ent.Owner, vehicle));
        }

        Dirty(ent);
    }

    public override void Update(float dt)
    {
        var query = EntityQueryEnumerator<TrainCompositeVehicleComponent, TrainVehicleComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var comp, out var vec, out var meta))
        {
            if (Paused(uid, meta))
                return;

            UpdateTrainLocomotor((uid, comp, vec));
        }
    }

    private void UpdateTrainLocomotor(Entity<TrainCompositeVehicleComponent, TrainVehicleComponent> ent)
    {
        // Apply a linear velocity to the vehicle, directing
        // it toward the next piece of track on its route
        var entCoords = _xform.GetMapCoordinates(_xformQuery.GetComponent(ent));
        var origin = _xform.GetMapCoordinates(_xformQuery.GetComponent(ent.Comp1.ForwardLocomotorJointUid));
        var destination = _xform.GetMapCoordinates(_xformQuery.GetComponent(ent.Comp1.RearLocomotorJointUid));

        // How far off are we from our destination?
        var target = (destination.Position + origin.Position) / 2;

        // How far off are we from our destination?
        var entDestDiff = target - entCoords.Position;

        _xform.SetWorldRotation(ent, (origin.Position - destination.Position).Normalized().ToAngle() + new Angle(Math.PI / 2));
        _xform.SetWorldRotation(ent.Comp1.GridUid, (origin.Position - destination.Position).Normalized().ToAngle() + new Angle(Math.PI / 2));

        // If we're really close, don't bother updating our velocity,
        // just move to the next piece of track
        if (entDestDiff.Length() > 1e-3)
        {
            // Set velocity
            var velocity = entDestDiff.Normalized() * _trainVehicle.GetEffectiveSpeed((ent, ent.Comp2));
            _physics.SetLinearVelocity(ent, velocity);
            _physics.SetLinearVelocity(ent.Comp1.GridUid, velocity);
        }
        else
        {
            _physics.SetLinearVelocity(ent, Vector2.Zero);
            _physics.SetLinearVelocity(ent.Comp1.GridUid, Vector2.Zero);
        }
    }
}
