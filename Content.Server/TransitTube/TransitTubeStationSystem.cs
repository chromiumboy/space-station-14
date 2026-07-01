using Content.Shared.DoAfter;
using Content.Shared.Train.Station;
using Content.Shared.Train.Vehicle;
using Content.Shared.TransitTube;

namespace Content.Server.TransitTube;

/// <inheritdoc/>
public sealed partial class TransitTubeStationSystem : SharedTransitTubeStationSystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedTrainStationSystem _trainStation = default!;

    // TODO: Move this to shared once the issues with animation flickering is resolved.

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TransitTubeStationComponent, DoAfterAttemptEvent<TrainStationDoAfterEvent>>(OnStartInsert);
        SubscribeLocalEvent<TransitTubeStationComponent, TrainStationHasVehicleApproachingEvent>(OnArrival);
        SubscribeLocalEvent<TransitTubeStationComponent, TrainStationHasVehicleDepartingEvent>(OnDeparture);
        SubscribeLocalEvent<TransitTubeStationComponent, TrainAutomatedBoardingEvent>(OnBoarding);
    }

    private void OnBoarding(Entity<TransitTubeStationComponent> ent, ref TrainAutomatedBoardingEvent args)
    {
        CloseStation(ent, args.Train);
    }

    private void OnStartInsert(Entity<TransitTubeStationComponent> ent, ref DoAfterAttemptEvent<TrainStationDoAfterEvent> args)
    {
        if (!TryComp<TrainStationComponent>(ent, out var trainStation))
        {
            args.Cancel();
            return;
        }

        // If needed, attempt to spawn a transit tube pod
        if (!_trainStation.IsOccupied((ent, trainStation)) &&
            ent.Comp.TransitTubePodSpawnCondition == TransitTubePodSpawnCondition.OnInsert &&
            ent.Comp.TransitTubePodPrototype != null)
        {
            Spawn(ent.Comp.TransitTubePodPrototype, Transform(ent).Coordinates);
        }

        // If there is no pod at the station, cancel the insertion
        if (!_trainStation.IsOccupied((ent, trainStation)))
        {
            args.Cancel();
        }
    }

    private void OnArrival(Entity<TransitTubeStationComponent> ent, ref TrainStationHasVehicleApproachingEvent args)
    {
        OpenStation(ent);
    }

    private void OnDeparture(Entity<TransitTubeStationComponent> ent, ref TrainStationHasVehicleDepartingEvent args)
    {
        CloseStation(ent, args.Vehicle);
    }

    private void OpenStation(Entity<TransitTubeStationComponent> ent)
    {
        if (ent.Comp.CurrentState == TransitTubeStationState.Open)
            return;

        ent.Comp.CurrentState = TransitTubeStationState.Open;
        Dirty(ent);

        _appearance.SetData(ent, TransitTubeStationVisuals.Key, TransitTubeStationState.Open);
    }

    private void CloseStation(Entity<TransitTubeStationComponent> ent, EntityUid? ignored = null)
    {
        if (ent.Comp.CurrentState == TransitTubeStationState.Closed)
            return;

        if (!TryComp<TrainStationComponent>(ent, out var trainStation))
            return;

        if (_trainStation.IsOccupied((ent, trainStation), ignored))
            return;

        ent.Comp.CurrentState = TransitTubeStationState.Closed;
        Dirty(ent);

        _appearance.SetData(ent, TransitTubeStationVisuals.Key, TransitTubeStationState.Closed);
    }
}
