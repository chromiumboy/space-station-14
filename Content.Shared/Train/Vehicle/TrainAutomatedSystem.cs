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

        SubscribeLocalEvent<TrainAutomatedComponent, TrainVehicleEnteredNewTrackEvent>(OnArriving);
    }

    private void OnArriving(Entity<TrainAutomatedComponent> ent, ref TrainVehicleEnteredNewTrackEvent args)
    {
        if (!TryComp<TrainVehicleComponent>(ent, out var trainVehicle))
            return;

        if (!TryComp<TrainStationComponent>(args.Track, out var trainStation))
            return;

        if (trainStation.Container == null)
            return;

        // Move all children into the station
        var xform = _xformQuery.GetComponent(ent);
        var children = xform.ChildEnumerator;

        while (children.MoveNext(out var held))
        {
            var xformHeld = _xformQuery.GetComponent(held);
            var metaHeld = _metaQuery.GetComponent(held);

            if (_container.Insert((held, xformHeld, metaHeld), trainStation.Container))
            {
                _trainVehicle.DetrainEntity(held);
            }
        }

        // Set the train's next departure time
        ent.Comp.NextBoardingTime = _timing.CurTime + ent.Comp.BoardingDelay;
        ent.Comp.NextDepartureTime = _timing.CurTime + ent.Comp.DepartureDelay;
        Dirty(ent);

        // Set the train's speed and position
        var train = new Entity<TrainVehicleComponent>(ent, trainVehicle);

        _trainVehicle.ResetDirectionChangeCounter(train);
        _trainVehicle.SetSpeed(train, 0, true);
        _xform.SetCoordinates(ent, Transform(args.Track).Coordinates);
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
        if (_trainVehicle.IsAtStation(vehicle, out var station))
        {
            if (trainAutomated.AutoBoard &&
                trainAutomated.NextBoardingTime != null &&
                _timing.CurTime >= trainAutomated.NextBoardingTime)
            {
                _trainStation.TryTransfer(station.Value, (ent, vehicle));
                trainAutomated.NextBoardingTime = null;

                var ev = new TrainAutomatedBoardingEvent(ent);
                RaiseLocalEvent(station.Value, ref ev);
            }

            if (trainAutomated.NextDepartureTime != null &&
                _timing.CurTime >= trainAutomated.NextDepartureTime)
            {
                _trainVehicle.SetTargetSpeed(vehicle, trainVehicle.IsReversing ? trainVehicle.TraversalSpeed.X : trainVehicle.TraversalSpeed.Y);
                trainAutomated.NextDepartureTime = null;

                var ev = new TrainAutomatedDepartingEvent(ent);
                RaiseLocalEvent(station.Value, ref ev);
            }

            return;
        }

        // If not at a station, set the train to its max speed
        _trainVehicle.SetTargetSpeed(vehicle, trainVehicle.IsReversing ? trainVehicle.TraversalSpeed.X : trainVehicle.TraversalSpeed.Y);
    }
}
