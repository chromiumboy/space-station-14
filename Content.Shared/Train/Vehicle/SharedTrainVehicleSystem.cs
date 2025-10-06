using Content.Shared.Atmos;
using Content.Shared.Damage;
using Content.Shared.Maps;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Train.Station;
using Content.Shared.Train.Track;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;
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
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly SharedPhysicsSystem _physicsSystem = default!;
    [Dependency] private readonly SharedTransformSystem _xformSystem = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly TileSystem _tile = default!;
    [Dependency] private readonly SharedTrainStationSystem _trainStation = default!;
    [Dependency] private readonly TrainTrackSystem _trainTrack = default!;

    private EntityQuery<TrainStationComponent> _stationQuery;
    private EntityQuery<MetaDataComponent> _metaQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();

        _stationQuery = GetEntityQuery<TrainStationComponent>();
        _metaQuery = GetEntityQuery<MetaDataComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();

        SubscribeLocalEvent<TrainVehicleComponent, ComponentStartup>(OnComponentStartup);
    }

    private void OnComponentStartup(Entity<TrainVehicleComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.Container = _containerSystem.EnsureContainer<Container>(ent, nameof(TrainVehicleComponent));
    }

    /// <summary>
    /// Ejects all entities from a vehicle.
    /// </summary>
    /// <param name="ent">The vehicle.</param>
    public void Derail(Entity<TrainVehicleComponent> ent)
    {
        if (Terminating(ent))
            return;

        Dirty(ent);

        // Get the holder and grid transforms
        var xform = _xformQuery.GetComponent(ent);
        var gridUid = xform.GridUid;
        _xformQuery.TryGetComponent(gridUid, out var gridXform);

        // Determine the exit angle of the ejected entities
        var exitDirection = ent.Comp.CurrentDirection;
        Angle? exitAngle = exitDirection != Direction.Invalid ? exitDirection.ToAngle() : null;

        // Check for a disposal unit to throw them into and then eject them from it.
        // *This ejection also makes the target not collide with the unit.*
        // *This is on purpose.*

        EntityUid? disposalId = null;
        TrainStationComponent? disposalUnit = null;

        if (TryComp<MapGridComponent>(gridUid, out var grid))
        {
            foreach (var contentUid in _maps.GetLocal(gridUid.Value, grid, xform.Coordinates))
            {
                if (_stationQuery.TryGetComponent(contentUid, out disposalUnit))
                {
                    disposalId = contentUid;
                    break;
                }
            }

            // If no disposal unit was found, this exit will be a little messy
            if (disposalUnit == null && _net.IsServer)
            {
                // Pry up the tile that the pipe was under
                var tileRef = _maps.GetTileRef((gridUid.Value, grid), xform.Coordinates);
                _tile.PryTile(tileRef);

                // Also pry up the tile infront of the pipe
                if (exitAngle != null)
                {
                    tileRef = _maps.GetTileRef((gridUid.Value, grid), xform.Coordinates.Offset(exitAngle.Value.ToWorldVec()));
                    _tile.PryTile(tileRef);
                }
            }
        }

        // Update the exit angle here to account for the grid's rotation
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

            var meta = _metaQuery.GetComponent(held);

            if (ent.Comp.Container != null && ent.Comp.Container.Contains(held))
            {
                _containerSystem.Remove((held, null, meta), ent.Comp.Container, reparent: false, force: true);
            }

            var heldXform = _xformQuery.GetComponent(held);

            if (heldXform.ParentUid != ent.Owner)
                continue;

            // Knockdown the entity
            _stun.TryKnockdown(held, ent.Comp.DerailmentStunDuration, force: true);

            // Try to damage the entity

            // Throw the entity out of the pipe
            _xformSystem.AttachToGridOrMap(held, heldXform);

            if (exitAngle != null)
            {
                _throwing.TryThrow(held, exitAngle.Value.ToWorldVec() * ent.Comp.ExitDistanceMultiplier, ent.Comp.TraversalSpeed * ent.Comp.DerailmentSpeedMultiplier);
            }
        }

        if (disposalId != null && disposalUnit != null)
        {
            _trainStation.EjectContents((disposalId.Value, disposalUnit));
        }

        ExpelAtmos(ent);

        // Add check for whether vehicle should delete itself

        PredictedDel(ent.Owner);

        // Raise derailment event
    }

    public bool TryEnterStation(Entity<TrainVehicleComponent> ent, Entity<TrainStationComponent> station)
    {
        if (station.Comp.Container == null)
            return false;

        // We're purposely iterating over all the holder's children
        // because the holder might have something teleported into it,
        // outside the usual container insertion logic.
        var xform = _xformQuery.GetComponent(ent);
        var children = xform.ChildEnumerator;

        while (children.MoveNext(out var held))
        {
            DetachEntity(held);

            var meta = _metaQuery.GetComponent(held);

            if (ent.Comp.Container != null && ent.Comp.Container.Contains(held))
            {
                _containerSystem.Remove((held, null, meta), ent.Comp.Container, reparent: false, force: true);
            }

            var heldXform = _xformQuery.GetComponent(held);
            _containerSystem.Insert((held, heldXform, meta), station.Comp.Container);
        }

        return true;
    }

    /// <summary>
    /// Attempts to assigns a disposal holder to a new disposal tube, updating the trajectory of the holder.
    /// </summary>
    /// <param name="ent">The disposal holder.</param>
    /// <param name="tube">The tube the holder is attempting to enter.</param>
    /// <returns>True if the holder can enter the tube.</returns>
    /// <remarks>
    /// This function will call ExitDisposals on any failure that does not make an ExitDisposals impossible.
    /// </remarks>
    public bool TryEnterTrack(Entity<TrainVehicleComponent> ent, Entity<TrainTrackComponent> tube)
    {
        if (ent.Comp.CurrentTube == tube)
            return false;

        var ev = new GetTrainVehicleNextDirectionEvent(ent);
        RaiseLocalEvent(tube, ref ev);

        // If the next direction to move is invalid, exit immediately
        if (ev.Next == Direction.Invalid)
        {
            Derail(ent);
            return false;
        }

        // Ensure all contained entities are attached to the holder
        if (ent.Comp.Container != null)
        {
            foreach (var held in ent.Comp.Container.ContainedEntities)
            {
                AttachEntity(ent, held);
            }
        }

        var xform = Transform(ent);

        // Attempt to damage entities when changing direction
        if (ent.Comp.CurrentDirection != ev.Next)
        {
            ent.Comp.DirectionChangeCount++;

            if (_net.IsServer)
            {
                _audio.PlayPvs(tube.Comp.ChangeDirectionsSound, xform.Coordinates);
            }

            // Check if the holder can escape the current pipe
            if (TryDerailing(ent, tube))
                return false;
        }

        // Update trajectory
        ent.Comp.CurrentDirection = ev.Next;
        ent.Comp.CurrentTube = tube;
        ent.Comp.NextTube = _trainTrack.NextTubeFor(tube, ent.Comp.CurrentDirection);

        // Update rotation
        xform.LocalRotation = ent.Comp.CurrentDirection.ToAngle();

        Dirty(ent);
        return true;
    }

    /// <summary>
    /// Links an entity with a disposal holder.
    /// </summary>
    /// <param name="ent">The disposal holder.</param>
    /// <param name="uid">The entity being linked.</param>
    public void AttachEntity(Entity<TrainVehicleComponent> ent, EntityUid uid)
    {
        var comp = EnsureComp<AboardTrainComponent>(uid);

        if (comp.TrainVehicle == ent.Owner)
            return;

        comp.TrainVehicle = ent;
        Dirty(uid, comp);
    }

    /// <summary>
    /// Unlinks an entity from its disposal holder.
    /// </summary>
    /// <param name="uid">The entity being unlinked.</param>
    public void DetachEntity(EntityUid uid)
    {
        RemComp<AboardTrainComponent>(uid);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<TrainVehicleComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var holder, out var xform))
        {
            UpdateDisposalHolder((uid, holder));
        }
    }

    /// <summary>
    /// Runs an update on the trajectory of a disposal holder.
    /// </summary>
    /// <param name="ent">The disposal holder.</param>
    private void UpdateDisposalHolder(Entity<TrainVehicleComponent> ent)
    {
        var currentTube = ent.Comp.CurrentTube;
        var nextTube = ent.Comp.NextTube;

        if (!Exists(currentTube) || !Exists(nextTube))
        {
            Derail(ent);
            return;
        }

        var gridUid = _xformQuery.GetComponent(currentTube.Value).GridUid;

        if (gridUid == null)
            return;

        // Apply a linear velocity to a disposal holder which
        // will direct it toward the next tube on its route
        var gridRotation = _xformSystem.GetWorldRotation(gridUid.Value);
        var origin = _xformQuery.GetComponent(currentTube.Value).Coordinates;
        var destination = _xformQuery.GetComponent(nextTube.Value).Coordinates;
        var entCoords = _xformQuery.GetComponent(ent).Coordinates;

        // How far off are we from our destination?
        var entDestDiff = destination.Position - entCoords.Position;

        // If we're really close, don't bother updating our velocity
        if (entDestDiff.Length() > 1e-6)
        {
            // Set velocity
            var velocity = gridRotation.RotateVec(entDestDiff.Normalized() * ent.Comp.TraversalSpeed);
            _physicsSystem.SetLinearVelocity(ent, velocity);

            // Determine whether the disposal holder should update its route,
            // based on its current position with respect to its target and origin
            var originDestDiff = destination.Position - origin.Position;
            var originEntDiff = entCoords.Position - origin.Position;

            if (originEntDiff.Length() < originDestDiff.Length())
                return;
        }

        // Attempt to enter the next tube
        if (TryComp<TrainTrackComponent>(nextTube, out var tube) &&
            TryEnterTrack(ent, (nextTube.Value, tube)))
        {
            UpdateDisposalHolder(ent);
        }
    }

    /// <summary>
    /// Expels the atmos of a disposal holder back into its surrounding environment.
    /// </summary>
    /// <param name="ent">The disposal holder.</param>
    protected virtual void ExpelAtmos(Entity<TrainVehicleComponent> ent)
    {
        // Handled by the server
    }

    /// <summary>
    /// Transfer the atmos of a disposal unit into the disposal holder it is launching.
    /// </summary>
    /// <param name="ent">The disposal holder.</param>
    /// <param name="unit">The disposal unit.</param>
    public virtual void TransferAtmos(Entity<TrainVehicleComponent> ent, Entity<TrainStationComponent> station)
    {
        // Handled by the server
    }

    /// <summary>
    /// The disposal tube holder attempts to escape the disposals system.
    /// </summary>
    /// <param name="ent">The disposal holder.</param>
    /// <param name="tube">The disposal tube the holder is attempting to escape.</param>
    /// <returns> True if the disposal holder escaped the disposal tube.</returns>
    protected virtual bool TryDerailing(Entity<TrainVehicleComponent> ent, Entity<TrainTrackComponent> track)
    {
        // Handled by the server

        return false;
    }
}
