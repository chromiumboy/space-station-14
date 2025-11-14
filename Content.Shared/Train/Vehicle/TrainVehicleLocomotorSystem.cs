using Content.Shared.Train.Station;
using Content.Shared.Train.Track;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Utility;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Content.Shared.Train.Vehicle;

public sealed partial class TrainVehicleLocomotorSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTrainVehicleSystem _trainVehicle = default!;

    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();

        _xformQuery = GetEntityQuery<TransformComponent>();

        SubscribeLocalEvent<TrainVehicleLocomotorComponent, ComponentStartup>(OnComponentStartup);
    }

    private void OnComponentStartup(Entity<TrainVehicleLocomotorComponent> ent, ref ComponentStartup args)
    {
        if (TryGetTrainVehicle(ent, out var trainEnt))
            TryAttachToTrack(ent, trainEnt.Value);
    }

    /// <summary>
    /// Tries to attach a locomotor to a piece of track. If no track is specified,
    /// it will attempt to attach itself to a piece of track under it.
    /// </summary>
    /// <param name="ent"></param>
    /// <param name="track"></param>
    public bool TryAttachToTrack(Entity<TrainVehicleLocomotorComponent> ent, Entity<TrainVehicleComponent> entTrain, Entity<TrainTrackComponent>? track = null)
    {
        var xform = _xformQuery.GetComponent(ent);

        // Try to find a piece of track to attach the locomotor
        if (track == null)
        {
            if (!TryComp<MapGridComponent>(xform.GridUid, out var grid))
                return false;

            TrainTrackComponent? trackComp = null;

            var trackUid = _map.GetLocal(xform.GridUid.Value, grid, xform.Coordinates)
                .FirstOrNull(x => TryComp(x, out trackComp));

            if (trackUid == null || trackComp == null)
                return false;

            track = (trackUid.Value, trackComp);
        }

        // Check that the track is valid
        var direction = track.Value.Comp.Directions.FirstOrNull();

        if (direction == null)
            return false;

        // Attach the locomotor to the found track
        ent.Comp.CurrentDirection = track.Value.Comp.Directions.FirstOrDefault().Key.GetOpposite();

        _xform.SetCoordinates(ent, Transform(track.Value).Coordinates);
        _xform.SetLocalRotation(ent, ent.Comp.CurrentDirection.ToAngle());

        return TryEnterTrack(ent, entTrain, track.Value);
    }

    /// <summary>
    /// Attempts to assigns a locomotor to a new train track, updating its trajectory.
    /// </summary>
    /// <param name="ent">The locomotor.</param>
    /// <param name="track">The track the locomotor is attempting to enter.</param>
    /// <returns>True if the locomotor can enter the track.</returns>
    /// <remarks>
    /// This function will call <see cref="SharedTrainVehicleSystem.Derail"/> on any failure.
    /// </remarks>
    public bool TryEnterTrack(Entity<TrainVehicleLocomotorComponent> ent, Entity<TrainVehicleComponent> entTrain, Entity<TrainTrackComponent> track)
    {
        if (entTrain.Comp.IsDerailed)
            return false;

        if (ent.Comp.CurrentTrack == track)
            return false;

        // TODO make event
        if (TryComp<TrainStationComponent>(track, out var station) && station.ReverseTrainOnEntry)
        {
            _trainVehicle.ReverseTraversalDirection(entTrain);

            var evEnteredNewTrack2 = new TrainVehicleEnteredNewTrackEvent(track);
            RaiseLocalEvent(ent, ref evEnteredNewTrack2);

            return true;
        }

        // Get the next direction to move
        var evNextDirection = new GetTrainVehicleLocomotorNextDirectionEvent(ent);
        RaiseLocalEvent(track, ref evNextDirection);

        // If the next direction to move is invalid, derail immediately
        if (evNextDirection.Next == Direction.Invalid)
        {
            _trainVehicle.Derail(entTrain);
            return false;
        }

        var xform = Transform(ent);

        // Check if we are changing direction
        if (ent.Comp.CurrentDirection != evNextDirection.Next)
        {
            ent.Comp.DirectionChangeCount++;

            if (_net.IsServer)
            {
                _audio.PlayPvs(track.Comp.ChangeDirectionsSound, xform.Coordinates);
            }

            // Check if the holder can escape the current pipe
            if (_trainVehicle.TryDerailing(entTrain, track))
                return false;
        }

        // Update trajectory
        ent.Comp.CurrentDirection = evNextDirection.Next;
        ent.Comp.CurrentTrack = track;

        var adjacentTrack = track.Comp.AdjacentTrack;

        if (adjacentTrack.TryGetValue(ent.Comp.CurrentDirection, out var nextTrack))
        {
            ent.Comp.NextTrack = nextTrack;
        }
        else
        {
            _trainVehicle.Derail(entTrain);
            return false;
        }

        Dirty(ent);

        var evEnteredNewTrack = new TrainVehicleEnteredNewTrackEvent(track);
        RaiseLocalEvent(ent, ref evEnteredNewTrack);

        return true;
    }

    public bool TryGetTrainVehicle(Entity<TrainVehicleLocomotorComponent> ent, [NotNullWhen(true)] out Entity<TrainVehicleComponent>? trainEnt)
    {
        trainEnt = null;

        if (TryComp<TrainVehicleComponent>(ent, out var train))
        {
            trainEnt = new Entity<TrainVehicleComponent>(ent, train);
            return true;
        }

        var parent = _xformQuery.GetComponent(ent).ParentUid;

        if (TryComp(ent, out train))
        {
            trainEnt = new Entity<TrainVehicleComponent>(parent, train);
            return true;
        }

        if (TryComp<TrainVehicleComponent>(ent.Comp.TrainBody, out var vec))
        {
            trainEnt = new Entity<TrainVehicleComponent>(ent.Comp.TrainBody, vec);
            return true;
        }

        return false;
    }

    public void SetSpeed(Entity<TrainVehicleLocomotorComponent> ent, Entity<TrainVehicleComponent> trainEnt)
    {
        // Need to reset the train's physics or it'll overshoot stations
        if (!TryComp<PhysicsComponent>(ent, out var body))
            return;

        var velocity = body.LinearVelocity;

        if (velocity.Length() > 0)
        {
            velocity = body.LinearVelocity.Normalized() * _trainVehicle.GetEffectiveSpeed(trainEnt);
        }

        _physics.SetLinearVelocity(ent, velocity);
    }

    /// <summary>
    /// Sets a train's DirectionChangeCount to zero.
    /// </summary>
    /// <param name="ent">The vehicle.</param>
    public void ResetDirectionChangeCounter(Entity<TrainVehicleLocomotorComponent> ent)
    {
        ent.Comp.DirectionChangeCount = 0;
        Dirty(ent);
    }

    public void ReverseTraversalDirection(Entity<TrainVehicleLocomotorComponent> ent)
    {
        // Reverse current direction
        ent.Comp.CurrentDirection = ent.Comp.CurrentDirection.GetOpposite();

        // Switch current and target tracks
        var targetTrack = ent.Comp.NextTrack;
        ent.Comp.NextTrack = ent.Comp.CurrentTrack;
        ent.Comp.CurrentTrack = targetTrack;

        Dirty(ent);
    }

    public override void Update(float dt)
    {
        var query = EntityQueryEnumerator<TrainVehicleLocomotorComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var locomotor, out var meta))
        {
            if (Paused(uid, meta))
                return;

            var locomotorEnt = new Entity<TrainVehicleLocomotorComponent>(uid, locomotor);

            if (TryGetTrainVehicle(locomotorEnt, out var trainEnt))
                UpdateTrainLocomotor(locomotorEnt, trainEnt.Value, dt);
        }
    }

    /// <summary>
    /// Runs an update on the trajectory of a train vehicle.
    /// </summary>
    /// <param name="ent">The vehicle.</param>
    private void UpdateTrainLocomotor(Entity<TrainVehicleLocomotorComponent> ent, Entity<TrainVehicleComponent> trainEnt, float dt)
    {
        if (trainEnt.Comp.IsDerailed)
            return;

        // If the track/grid was removed, derail the vehicle
        var current = ent.Comp.CurrentTrack;
        var next = ent.Comp.NextTrack;

        if (!Exists(current) || !Exists(next))
        {
            _trainVehicle.Derail(trainEnt);
            return;
        }

        var gridUid = _xformQuery.GetComponent(current.Value).GridUid;

        if (gridUid == null)
        {
            _trainVehicle.Derail(trainEnt);
            return;
        }

        if (trainEnt.Comp.IsDerailed || trainEnt.Comp.CurrentSpeed == 0)
            return;

        // Update the entity's rotation
        var xform = _xformQuery.GetComponent(ent);
        _xform.SetLocalRotation(ent, trainEnt.Comp.IsReversing ? ent.Comp.CurrentDirection.GetOpposite().ToAngle() : ent.Comp.CurrentDirection.ToAngle());

        // Apply a linear velocity to the vehicle, directing
        // it toward the next piece of track on its route
        var origin = _xform.GetMapCoordinates(_xformQuery.GetComponent(current.Value));
        var destination = _xform.GetMapCoordinates(_xformQuery.GetComponent(next.Value));
        var entCoords = _xform.GetMapCoordinates(_xformQuery.GetComponent(ent));

        // How far off are we from our destination?
        var entDestDiff = destination.Position - entCoords.Position;

        // If we're really close, don't bother updating our velocity,
        // just move to the next piece of track
        if (entDestDiff.Length() > 1e-3)
        {
            // Set velocity
            var velocity = entDestDiff.Normalized() * _trainVehicle.GetEffectiveSpeed(trainEnt);
            _physics.SetLinearVelocity(ent, velocity);

            // Determine whether the vehicle should update its route,
            // based on its current position with respect to its target and origin
            var originDestDiff = destination.Position - origin.Position;
            var originEntDiff = entCoords.Position - origin.Position;

            if (originEntDiff.Length() < originDestDiff.Length())
                return;
        }

        // Attempt to enter the next track
        if (TryComp<TrainTrackComponent>(next, out var track) &&
            TryEnterTrack(ent, trainEnt, (next.Value, track)))
        {
            UpdateTrainLocomotor(ent, trainEnt, 0);
        }
    }
}
