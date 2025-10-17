using Content.Shared.Train.Station;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Shared.Train.Vehicle;

public sealed partial class TrainAutomatedSystem : EntitySystem
{
    [Dependency] private readonly SharedTrainVehicleSystem _trainVehicle = default!;
    [Dependency] private readonly SharedTrainStationSystem _trainStation = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private EntityQuery<MetaDataComponent> _metaQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();

        _metaQuery = GetEntityQuery<MetaDataComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();

        SubscribeLocalEvent<TrainAutomatedComponent, TrainVehicleArrivingAtStationEvent>(OnArriving);
        SubscribeLocalEvent<TrainAutomatedComponent, TrainVehicleDepartingStationEvent>(OnDeparture);
    }

    private void OnArriving(Entity<TrainAutomatedComponent> ent, ref TrainVehicleArrivingAtStationEvent args)
    {
        if (!TryComp<TrainVehicleComponent>(ent, out var trainVehicle))
            return;

        if (args.Station.Comp.Container == null)
            return;

        // Move all children into the station
        var xform = _xformQuery.GetComponent(ent);
        var children = xform.ChildEnumerator;

        while (children.MoveNext(out var held))
        {
            var xformHeld = _xformQuery.GetComponent(held);
            var metaHeld = _metaQuery.GetComponent(held);

            if (_container.Insert((held, xformHeld, metaHeld), args.Station.Comp.Container))
            {
                _trainVehicle.DetrainEntity(held);
            }
        }

        // Set the train's next departure time
        ent.Comp.AutomaticDepatureTime = _timing.CurTime + ent.Comp.AutomaticDelayAtStations;
        Dirty(ent);

        // Set the train's speed and position
        var train = new Entity<TrainVehicleComponent>(ent, trainVehicle);

        _trainVehicle.ResetDirectionChangeCounter(train);
        _trainVehicle.SetSpeed(train, 0);
        _xform.SetCoordinates(ent, Transform(args.Station).Coordinates);
    }

    private void OnDeparture(Entity<TrainAutomatedComponent> ent, ref TrainVehicleDepartingStationEvent args)
    {
        if (!TryComp<TrainVehicleComponent>(ent, out var vehicle))
            return;

        _trainStation.TryTransfer(args.Station, (ent, vehicle));
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<TrainAutomatedComponent, TrainVehicleComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var trainAutomated, out var trainVehicle, out var meta))
        {
            if (Paused(uid, meta))
                return;

            UpdateTrain((uid, trainAutomated, trainVehicle));
        }
    }

    private void UpdateTrain(Entity<TrainAutomatedComponent, TrainVehicleComponent> ent)
    {
        var (uid, trainAutomated, trainVehicle) = ent;
        var vehicle = new Entity<TrainVehicleComponent>(uid, trainVehicle);

        // Check if currently at a station and whether we should depart it
        if (_trainVehicle.IsAtStation(vehicle))
        {
            if (_timing.CurTime >= trainAutomated.AutomaticDepatureTime)
            {
                _trainVehicle.SetSpeed(vehicle, trainVehicle.TraversalSpeed);
                _trainVehicle.DepartStation(vehicle);
            }

            return;
        }

        // If not at a station, set the train to its max speed
        _trainVehicle.SetSpeed(vehicle, trainVehicle.TraversalSpeed);
    }
}
