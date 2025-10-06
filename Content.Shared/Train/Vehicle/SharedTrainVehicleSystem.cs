using Content.Shared.Atmos;
using Content.Shared.Damage;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Train.Station;
using Content.Shared.Train.Track;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Physics.Systems;

namespace Content.Shared.Train.Vehicle;

/// <summary>
/// This sytem handles the insertion, movement, and exiting of entities
/// through the disposals system.
/// </summary>
public abstract partial class SharedTrainVehicleSystem : EntitySystem
{
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly SharedPhysicsSystem _physicsSystem = default!;
    [Dependency] private readonly SharedTransformSystem _xformSystem = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;

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
        ent.Comp.Container = _containerSystem.EnsureContainer<Container>(ent, nameof(TrainVehicleComponent));
    }

    /// <summary>
    /// Causes a train vehicle to derail, removing it from any track, and rendering it unable to move.
    /// Entities aboard the vehicle may also be ejected.
    /// </summary>
    /// <param name="ent">The vehicle.</param>
    public void Derail(Entity<TrainVehicleComponent> ent)
    {
        if (Terminating(ent))
            return;

        // Raise before derailment event
        var beforeEv = new BeforeTrainVehicleDerailmentEvent();
        RaiseLocalEvent(ent, ref beforeEv);

        // Eject contens
        if (ent.Comp.EjectContentsOnDerailment)
        {
            // Get the vehicle and grid transforms
            var xform = _xformQuery.GetComponent(ent);
            var gridUid = xform.GridUid;

            _xformQuery.TryGetComponent(gridUid, out var gridXform);

            // Determine the exit angle of ejected entities
            var exitDirection = ent.Comp.CurrentDirection;
            Angle? exitAngle = exitDirection != Direction.Invalid ? exitDirection.ToAngle() : null;

            // Update the exit angle to account for the grid's rotation
            if (exitAngle != null && gridXform != null)
            {
                exitAngle += _xformSystem.GetWorldRotation(gridXform);
            }

            // We're purposely iterating over all the holder's children
            // because the holder might have something teleported into it,
            // outside the usual container insertion logic.

            var children = xform.ChildEnumerator;
            while (children.MoveNext(out var held))
            {
                DetachEntity(held);

                // Remove the entity
                if (ent.Comp.Container != null && ent.Comp.Container.Contains(held))
                {
                    var meta = _metaQuery.GetComponent(held);
                    _containerSystem.Remove((held, null, meta), ent.Comp.Container, force: true);
                }

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
        if (station.Comp.Container == null)
            return false;

        // Move all children into the station
        var xform = _xformQuery.GetComponent(ent);
        var children = xform.ChildEnumerator;

        while (children.MoveNext(out var held))
        {
            var xformHeld = _xformQuery.GetComponent(held);
            var metaHeld = _metaQuery.GetComponent(held);

            if (_containerSystem.Insert((held, xformHeld, metaHeld), station.Comp.Container))
            {
                DetachEntity(held);
            }
        }

        ent.Comp.CurrentSpeed = 0;
        ent.Comp.CurrentTrack = ent.Comp.NextTrack;
        ent.Comp.CurrentStation = station;
        Dirty(ent);

        return true;
    }

    public void DepartStation(Entity<TrainVehicleComponent> ent)
    {
        ent.Comp.CurrentStation = null;

        if (ent.Comp.Automatic)
        {
            SetSpeed(ent, ent.Comp.TraversalSpeed);
        }

        Dirty(ent);

        if (TryComp<TrainTrackComponent>(ent.Comp.NextTrack, out var track))
        {
            TryEnterTrack(ent, (ent.Comp.NextTrack.Value, track), true);
            return;
        }

        Derail(ent);
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
        if (ent.Comp.CurrentTrack == track)
            return false;

        // If the next section of track is a station, try to enter it
        if (!ignoreStations && TryComp<TrainStationComponent>(track, out var station))
        {
            return TryEnterStation(ent, (track, station));
        }

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
        return true;
    }

    /// <summary>
    /// Adds an entity to a train vehicle.
    /// </summary>
    /// <param name="ent">The vehicle.</param>
    /// <param name="uid">The entity being added.</param>
    public void AttachEntity(Entity<TrainVehicleComponent> ent, EntityUid uid)
    {
        var comp = EnsureComp<AboardTrainComponent>(uid);

        if (comp.TrainVehicle == ent.Owner)
            return;

        comp.TrainVehicle = ent;
        Dirty(uid, comp);
    }

    /// <summary>
    /// Removes an entity from the train vehicle it is on.
    /// </summary>
    /// <param name="uid">The entity being removed.</param>
    public void DetachEntity(EntityUid uid)
    {
        RemComp<AboardTrainComponent>(uid);
    }

    /// <summary>
    /// Sets the current speed of a train vehicle.
    /// </summary>
    /// <param name="ent">The vehicle</param>
    /// <param name="speed">The new speed.</param>
    public void SetSpeed(Entity<TrainVehicleComponent> ent, float speed)
    {
        ent.Comp.CurrentSpeed = Math.Clamp(speed, 0, ent.Comp.TraversalSpeed);
        Dirty(ent);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<TrainVehicleComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var holder, out var xform))
        {
            UpdateTrainVehicle((uid, holder));
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

        // If the track/grid is removed, derail the vehicle
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

        if (ent.Comp.CurrentSpeed == 0)
            return;

        // Update the entity's rotation
        var xform = _xformQuery.GetComponent(ent);
        xform.LocalRotation = ent.Comp.CurrentDirection.ToAngle();

        // Apply a linear velocity to the vehicle, directing
        // it toward the next piece of track on its route
        var gridRotation = _xformSystem.GetWorldRotation(gridUid.Value);
        var origin = _xformQuery.GetComponent(current.Value).Coordinates;
        var destination = _xformQuery.GetComponent(next.Value).Coordinates;
        var entCoords = _xformQuery.GetComponent(ent).Coordinates;

        // How far off are we from our destination?
        var entDestDiff = destination.Position - entCoords.Position;

        // If we're really close, don't bother updating our velocity
        if (entDestDiff.Length() > 1e-6)
        {
            // Set velocity
            var velocity = gridRotation.RotateVec(entDestDiff.Normalized() * ent.Comp.CurrentSpeed);
            _physicsSystem.SetLinearVelocity(ent, velocity);

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
