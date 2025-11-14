using Content.Shared.Physics;
using Content.Shared.Station;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Utility;

namespace Content.Shared.Train.Grid;

public sealed partial class TrainVehicleGridSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TrainVehicleGridComponent, GridFixtureChangeEvent>(OnGridFixtureChange);
    }

    private void OnGridFixtureChange(Entity<TrainVehicleGridComponent> ent, ref GridFixtureChangeEvent args)
    {
        foreach (var (name, fixture) in args.NewFixtures)
        {
            //_physics.SetCollisionLayer(ent, name, fixture, (int)CollisionGroup.Impassable);
            //_physics.SetCollisionMask(ent, name, fixture, (int)CollisionGroup.Impassable);

            _physics.SetCollisionLayer(ent, name, fixture, 0);
            _physics.SetCollisionMask(ent, name, fixture, 0);

            _physics.SetHard(ent, fixture, false);
        }
    }

    /*private void OnTrainVehicleSpeedChange(Entity<TrainVehicleGridComponent> ent, ref TrainVehicleSpeedChangeEvent args)
    {
        if (Math.Abs(args.OriginalSpeed - args.NewSpeed) < ent.Comp.SpeedChangeKnockdownThreshold)
            return;

        while (Transform(ent).ChildEnumerator.MoveNext(out var child))
        {
            if (!_buckleQuery.TryGetComponent(child, out var buckle) || buckle.Buckled)
                continue;

            _stun.TryKnockdown(child, ent.Comp.KnockdownAmount, ent.Comp.Refresh, ent.Comp.AutoStand, ent.Comp.Drop, true);
        }
    }*/
}
