using Content.Server.Atmos.EntitySystems;
using Content.Server.Body.Systems;
using Content.Shared.Train.Station;
using Content.Shared.Train.Track;
using Content.Shared.Train.Vehicle;
using Robust.Shared.Random;

namespace Content.Server.Train.Station;

/// <inheritdoc/>
public sealed partial class TrainVehicleSystem : SharedTrainVehicleSystem
{
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly SharedTransformSystem _xformSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AboardTrainComponent, AtmosExposedGetAirEvent>(OnGetAir);
        SubscribeLocalEvent<AboardTrainComponent, InhaleLocationEvent>(OnInhaleLocation);
        SubscribeLocalEvent<AboardTrainComponent, ExhaleLocationEvent>(OnExhaleLocation);
    }

    private void OnGetAir(EntityUid uid, AboardTrainComponent component, ref AtmosExposedGetAirEvent args)
    {
        if (!TryComp<TrainVehicleComponent>(component.TrainVehicle, out var vehicle) || !vehicle.Airtight)
            return;

        args.Gas = vehicle.Air;
        args.Handled = true;
    }

    private void OnInhaleLocation(EntityUid uid, AboardTrainComponent component, InhaleLocationEvent args)
    {
        if (!TryComp<TrainVehicleComponent>(component.TrainVehicle, out var vehicle) || !vehicle.Airtight)
            return;

        args.Gas = vehicle.Air;
    }

    private void OnExhaleLocation(EntityUid uid, AboardTrainComponent component, ExhaleLocationEvent args)
    {
        if (!TryComp<TrainVehicleComponent>(component.TrainVehicle, out var vehicle) || !vehicle.Airtight)
            return;

        args.Gas = vehicle.Air;
    }

    /// <inheritdoc/>
    public override void TransferAtmos(Entity<TrainVehicleComponent> ent, Entity<TrainStationComponent> unit)
    {
        if (!ent.Comp.Airtight)
            return;

        _atmos.Merge(ent.Comp.Air, unit.Comp.Air);
        unit.Comp.Air.Clear();
    }

    /// <inheritdoc/>
    protected override void ExpelAtmos(Entity<TrainVehicleComponent> ent)
    {
        if (!ent.Comp.Airtight)
            return;

        if (_atmos.GetContainingMixture(ent.Owner, false, true) is { } environment)
        {
            _atmos.Merge(environment, ent.Comp.Air);
            ent.Comp.Air.Clear();
        }
    }

    /// <inheritdoc/>
    protected override bool TryDerailing(Entity<TrainVehicleComponent> ent, Entity<TrainTrackComponent> conduit)
    {
        // Check if the vehicle should have a chance to derail yet
        if (ent.Comp.DerailmentThreshold == null ||
            ent.Comp.DirectionChangeCount < ent.Comp.DerailmentThreshold)
            return false;

        // Check if the vehicle derailed
        if (_random.NextFloat() > ent.Comp.DerailmentChance)
            return false;

        // Unanchor the train track and exit
        var xform = Transform(conduit);
        _xformSystem.Unanchor(conduit, xform);
        Derail(ent);

        return true;
    }
}
