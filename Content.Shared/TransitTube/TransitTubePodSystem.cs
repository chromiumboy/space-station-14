using Content.Shared.Train.Vehicle;

namespace Content.Shared.TransitTube;

public sealed partial class TransitTubePodSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TransitTubePodComponent, TrainVehicleApproachingStationEvent>(OnArrival);
        SubscribeLocalEvent<TransitTubePodComponent, AfterTrainVehicleDerailmentEvent>(OnDerailment);
    }

    //_xform.SetLocalRotationNoLerp(ent, ent.Comp.CurrentDirection.ToAngle());

    private void OnArrival(Entity<TransitTubePodComponent> ent, ref TrainVehicleApproachingStationEvent args)
    {
        if (ent.Comp.Permanent)
            return;

        Despawn(ent);
    }

    private void OnDerailment(Entity<TransitTubePodComponent> ent, ref AfterTrainVehicleDerailmentEvent args)
    {
        Despawn(ent);
    }

    /// <summary>
    /// Causes a transit tube pod to immediately despawn.
    /// </summary>
    /// <param name="ent">The transit tube pod.</param>
    public void Despawn(Entity<TransitTubePodComponent> ent)
    {
        if (ent.Comp.DespawnEffect != null)
        {
            var effect = Spawn(ent.Comp.DespawnEffect, Transform(ent).Coordinates);
            Transform(effect).LocalRotation = Transform(ent).LocalRotation;
        }

        PredictedQueueDel(ent);
    }
}
