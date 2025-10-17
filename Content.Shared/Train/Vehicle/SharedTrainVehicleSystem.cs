using Content.Shared.Atmos;
using Content.Shared.Damage;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Train.Station;
using Content.Shared.Train.Track;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using System.Linq;

namespace Content.Shared.Train.Vehicle;

/// <summary>
/// This system handles vehicles that run on train tracks.
/// </summary>
public abstract partial class SharedTrainVehicleSystem : EntitySystem
{
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;

    private EntityQuery<MetaDataComponent> _metaQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();

        _metaQuery = GetEntityQuery<MetaDataComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();

        SubscribeLocalEvent<TrainVehicleComponent, ComponentStartup>(OnComponentStartup);
    }

    private void OnComponentStartup(Entity<TrainVehicleComponent> ent, ref ComponentStartup args)
    {
        // Ensure container
        ent.Comp.Container = _container.EnsureContainer<Container>(ent, nameof(TrainVehicleComponent));

        // Try to attach to a piece of track
        TryAttachToTrack(ent);
    }

    /// <summary>
    /// Tries to attach a train vehicle to a piece of track. If no track is specified,
    /// it will attempt to attach itself to a peice of track under it.
    /// </summary>
    /// <param name="ent"></param>
    /// <param name="track"></param>
    public bool TryAttachToTrack(Entity<TrainVehicleComponent> ent, Entity<TrainTrackComponent>? track = null)
    {
        var xform = _xformQuery.GetComponent(ent);

        // Try to find a piece of track to attach the vehicle
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

        // Attach the vehicle to the found track
        ent.Comp.CurrentDirection = track.Value.Comp.Directions.FirstOrDefault().Key;
        ent.Comp.IsDerailed = false;

        _xform.SetCoordinates(ent, Transform(track.Value).Coordinates);
        _xform.SetLocalRotation(ent, ent.Comp.CurrentDirection.ToAngle());

        if (TryComp<PhysicsComponent>(ent, out var body))
        {
            _physics.SetBodyType(ent, BodyType.Kinematic, null, body, xform);
        }

        TryEnterTrack(ent, track.Value);

        return true;
    }

    /// <summary>
    /// Causes a train vehicle to derail, removing it from any track, and rendering it unable to move.
    /// Entities aboard the vehicle may also be ejected.
    /// </summary>
    /// <param name="ent">The vehicle.</param>
    public void Derail(Entity<TrainVehicleComponent> ent)
    {
        if (ent.Comp.IsDerailed)
            return;

        // Raise before derailment event
        var beforeEv = new BeforeTrainVehicleDerailmentEvent();
        RaiseLocalEvent(ent, ref beforeEv);

        var xform = _xformQuery.GetComponent(ent);

        // Eject contens
        if (ent.Comp.EjectContentsOnDerailment)
        {
            // Get the vehicle and grid transforms
            var gridUid = xform.GridUid;

            _xformQuery.TryGetComponent(gridUid, out var gridXform);

            // Determine the exit angle of ejected entities
            var exitDirection = ent.Comp.CurrentDirection;
            Angle? exitAngle = exitDirection != Direction.Invalid ? exitDirection.ToAngle() : null;

            // Update the exit angle to account for the grid's rotation
            if (exitAngle != null && gridXform != null)
            {
                exitAngle += _xform.GetWorldRotation(gridXform);
            }

            // We're purposely iterating over all the holder's children
            // because the holder might have something teleported into it,
            // outside the usual container insertion logic.

            var children = xform.ChildEnumerator;
            while (children.MoveNext(out var held))
            {
                // Remove the entity
                DetrainEntity(held);

                // Knockdown the entity
                if (ent.Comp.DerailmentStunDuration.TotalSeconds > 0)
                {
                    _stun.TryKnockdown(held, ent.Comp.DerailmentStunDuration, force: true);
                }

                // Damage the entity
                if (ent.Comp.DerailmentDamage.GetTotal() > 0)
                {
                    _damageable.TryChangeDamage(held, ent.Comp.DerailmentDamage);
                }

                // Throw the entity
                if (exitAngle != null)
                {
                    _throwing.TryThrow(held,
                        exitAngle.Value.ToWorldVec() * ent.Comp.ExitDistanceMultiplier,
                        ent.Comp.CurrentSpeed * ent.Comp.DerailmentSpeedMultiplier);
                }
            }
        }

        // Alter physics
        if (TryComp<PhysicsComponent>(ent, out var body))
        {
            var velocity = body.LinearVelocity.Normalized() * ent.Comp.CurrentSpeed;
            _physics.SetBodyType(ent, BodyType.Dynamic, null, body, xform);
            _physics.SetLinearVelocity(ent, velocity);
        }

        // Remove the vehicle from the track
        ent.Comp.IsDerailed = true;
        ent.Comp.CurrentStation = null;
        ent.Comp.CurrentTrack = null;
        ent.Comp.NextTrack = null;
        ent.Comp.CurrentDirection = Direction.Invalid;
        ent.Comp.CurrentSpeed = 0;
        Dirty(ent);

        // Expel contained atmosphere
        ExpelAtmos(ent);

        // Raise after derailment event
        var afterEv = new AfterTrainVehicleDerailmentEvent();
        RaiseLocalEvent(ent, ref afterEv);
    }

    /// <summary>
    /// Have a train vehicle try to enter a train station, transferring all
    /// contained entities into it.
    /// </summary>
    /// <param name="ent">The vehicle.</param>
    /// <param name="station">The station</param>
    /// <returns>True if the contents of the vehicle was transferred.</returns>
    public bool TryEnterStation(Entity<TrainVehicleComponent> ent, Entity<TrainStationComponent> station)
    {
        if (ent.Comp.IsDerailed)
            return false;

        ent.Comp.CurrentStation = station;
        Dirty(ent);

        var evVehicle = new TrainVehicleArrivingAtStationEvent(station);
        RaiseLocalEvent(ent, ref evVehicle);

        var evStation = new TrainStationHasVehicleArrivingEvent(ent);
        RaiseLocalEvent(station, ref evStation);

        return true;
    }

    public void DepartStation(Entity<TrainVehicleComponent> ent)
    {
        if (ent.Comp.IsDerailed)
            return;

        if (TryComp<TrainStationComponent>(ent.Comp.CurrentStation, out var trainStation))
        {
            var station = new Entity<TrainStationComponent>(ent.Comp.CurrentStation.Value, trainStation);

            var evVehicle = new TrainVehicleDepartingStationEvent(station);
            RaiseLocalEvent(ent, ref evVehicle);

            var evStation = new TrainStationHasVehicleDepartingEvent(ent);
            RaiseLocalEvent(station, ref evStation);
        }

        ent.Comp.CurrentStation = null;
        Dirty(ent);

        _xform.SetLocalRotationNoLerp(ent, ent.Comp.CurrentDirection.ToAngle());
    }

    /// <summary>
    /// Attempts to assigns a train vehicle to a new train track, updating its trajectory.
    /// </summary>
    /// <param name="ent">The vehicle.</param>
    /// <param name="track">The track the vehicle is attempting to enter.</param>
    /// <param name="ignoreStations">If true the vehicle will not attempt to
    /// enter any stations on this piece of track.</param>
    /// <returns>True if the vehicle can enter the track.</returns>
    /// <remarks>
    /// This function will call <see cref="Derail"/> on any failure.
    /// </remarks>
    public bool TryEnterTrack(Entity<TrainVehicleComponent> ent, Entity<TrainTrackComponent> track, bool ignoreStations = false)
    {
        if (ent.Comp.IsDerailed)
            return false;

        if (ent.Comp.CurrentTrack == track)
            return false;

        // Get the next direction to move
        var ev = new GetTrainVehicleNextDirectionEvent(ent);
        RaiseLocalEvent(track, ref ev);

        // If the next direction to move is invalid, derail immediately
        if (ev.Next == Direction.Invalid)
        {
            Derail(ent);
            return false;
        }

        var xform = Transform(ent);

        // Check if we are changing direction
        if (ent.Comp.CurrentDirection != ev.Next)
        {
            ent.Comp.DirectionChangeCount++;

            if (_net.IsServer)
            {
                _audio.PlayPvs(track.Comp.ChangeDirectionsSound, xform.Coordinates);
            }

            // Check if the holder can escape the current pipe
            if (TryDerailing(ent, track))
                return false;
        }

        // If at a station, depart it
        if (IsAtStation(ent))
        {
            DepartStation(ent);
        }

        // Update trajectory
        ent.Comp.CurrentDirection = ev.Next;
        ent.Comp.CurrentTrack = track;
        ent.Comp.CurrentStation = null;

        var adjacentTrack = track.Comp.AdjacentTrack;

        if (adjacentTrack.TryGetValue(ent.Comp.CurrentDirection, out var nextTrack))
        {
            ent.Comp.NextTrack = nextTrack;
        }
        else
        {
            Derail(ent);
            return false;
        }

        Dirty(ent);

        // If the next section of track is a station, try to enter it
        if (TryComp<TrainStationComponent>(track, out var station))
        {
            return TryEnterStation(ent, (track, station));
        }

        return true;
    }

    /// <summary>
    /// Adds an entity to a train vehicle.
    /// </summary>
    /// <param name="ent">The vehicle.</param>
    /// <param name="uid">The entity being added.</param>
    public bool TryBoardingEntity(Entity<TrainVehicleComponent> ent, EntityUid uid)
    {
        if (ent.Comp.IsDerailed)
            return false;

        if (ent.Comp.Container == null || !_container.Insert(uid, ent.Comp.Container))
            return false;

        var aboardTrain = EnsureComp<AboardTrainComponent>(uid);

        if (aboardTrain.TrainVehicle == ent.Owner)
            return false;

        aboardTrain.TrainVehicle = ent;
        Dirty(uid, aboardTrain);

        var ev = new EntityBoardedTrainVehicleEvent(ent);
        RaiseLocalEvent(uid, ref ev);

        return true;
    }

    /// <summary>
    /// Removes an entity from the train vehicle it is on.
    /// </summary>
    /// <param name="uid">The entity being removed.</param>
    public void DetrainEntity(EntityUid uid)
    {
        if (!TryComp<AboardTrainComponent>(uid, out var aboardTrain))
            return;

        if (TryComp<TrainVehicleComponent>(aboardTrain.TrainVehicle, out var vehicle) &&
            vehicle.Container != null &&
            vehicle.Container.Contains(uid))
        {
            var meta = _metaQuery.GetComponent(uid);
            _container.Remove((uid, null, meta), vehicle.Container, force: true);

            var ev = new EntityLeftTrainVehicleEvent((aboardTrain.TrainVehicle.Value, vehicle));
            RaiseLocalEvent(uid, ref ev);
        }

        RemComp<AboardTrainComponent>(uid);
    }

    /// <summary>
    /// Sets the current speed of a train vehicle.
    /// </summary>
    /// <param name="ent">The vehicle.</param>
    /// <param name="speed">The new speed.</param>
    public void SetSpeed(Entity<TrainVehicleComponent> ent, float speed)
    {
        ent.Comp.CurrentSpeed = Math.Clamp(speed, 0, ent.Comp.TraversalSpeed);

        if (ent.Comp.IsDerailed)
        {
            speed = 0;
        }

        Dirty(ent);

        if (!TryComp<PhysicsComponent>(ent, out var body))
            return;

        var velocity = body.LinearVelocity;

        if (velocity.Length() > 0)
        {
            velocity = body.LinearVelocity.Normalized() * ent.Comp.CurrentSpeed;
        }

        _physics.SetLinearVelocity(ent, velocity);
    }

    /// <summary>
    /// Sets a train's DirectionChangeCount to zero.
    /// </summary>
    /// <param name="ent">The vehicle.</param>
    public void ResetDirectionChangeCounter(Entity<TrainVehicleComponent> ent)
    {
        ent.Comp.DirectionChangeCount = 0;
        Dirty(ent);
    }

    /// <summary>
    /// Returns whether a train vehicle is at a station. If a station entity is also supplied,
    /// this function will return whether the train is at the specified station.
    /// </summary>
    /// <param name="ent">The vehicle.</param>
    /// <param name="station">The station (optional).</param>
    public bool IsAtStation(Entity<TrainVehicleComponent> ent, Entity<TrainStationComponent>? station = null)
    {
        if (station != null)
            return ent.Comp.CurrentStation == station.Value.Owner;

        return ent.Comp.CurrentStation != null;
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<TrainVehicleComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var trainVehicle, out var meta))
        {
            if (Paused(uid, meta))
                return;

            UpdateTrainVehicle((uid, trainVehicle));
        }
    }

    /// <summary>
    /// Runs an update on the trajectory of a train vehicle.
    /// </summary>
    /// <param name="ent">The vehicle.</param>
    private void UpdateTrainVehicle(Entity<TrainVehicleComponent> ent)
    {
        if (ent.Comp.IsDerailed)
            return;

        // If the track/grid was removed, derail the vehicle
        var current = ent.Comp.CurrentTrack;
        var next = ent.Comp.NextTrack;

        if (!Exists(current) || !Exists(next))
        {
            Derail(ent);
            return;
        }

        var gridUid = _xformQuery.GetComponent(current.Value).GridUid;

        if (gridUid == null)
        {
            Derail(ent);
            return;
        }

        if (ent.Comp.IsDerailed || ent.Comp.CurrentSpeed == 0)
            return;

        // Update the entity's rotation
        var xform = _xformQuery.GetComponent(ent);
        _xform.SetLocalRotation(ent, ent.Comp.CurrentDirection.ToAngle());

        // Apply a linear velocity to the vehicle, directing
        // it toward the next piece of track on its route
        var gridRotation = _xform.GetWorldRotation(gridUid.Value);
        var origin = _xformQuery.GetComponent(current.Value).Coordinates;
        var destination = _xformQuery.GetComponent(next.Value).Coordinates;
        var entCoords = _xformQuery.GetComponent(ent).Coordinates;

        // How far off are we from our destination?
        var entDestDiff = destination.Position - entCoords.Position;

        // If we're really close, don't bother updating our velocity,
        // just move to the next piece of track
        if (entDestDiff.Length() > 1e-6)
        {
            // Set velocity
            var velocity = gridRotation.RotateVec(entDestDiff.Normalized() * ent.Comp.CurrentSpeed);
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
            TryEnterTrack(ent, (next.Value, track)))
        {
            UpdateTrainVehicle(ent);
        }
    }

    /// <summary>
    /// Expels the atmos of a train vehicle back into its surrounding environment.
    /// </summary>
    /// <param name="ent">The vehicle.</param>
    protected virtual void ExpelAtmos(Entity<TrainVehicleComponent> ent)
    {
        // Handled by the server
    }

    /// <summary>
    /// Transfer the atmos of a station into the vehicle that is departing it.
    /// </summary>
    /// <param name="ent">The vehicle.</param>
    /// <param name="station">The station.</param>
    public virtual void TransferAtmos(Entity<TrainVehicleComponent> ent, Entity<TrainStationComponent> station)
    {
        // Handled by the server
    }

    /// <summary>
    /// A train vehicle is attempts to derail itself.
    /// </summary>
    /// <param name="ent">The vehicle.</param>
    /// <param name="track">The track the vehicle is on.</param>
    /// <returns>True if the vehicle derailed.</returns>
    protected virtual bool TryDerailing(Entity<TrainVehicleComponent> ent, Entity<TrainTrackComponent> track)
    {
        // Handled by the server

        return false;
    }
}
