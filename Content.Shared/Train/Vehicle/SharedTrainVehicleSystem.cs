using Content.Shared.Damage;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Train.Station;
using Content.Shared.Train.Track;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Utility;
using System.Diagnostics.CodeAnalysis;

namespace Content.Shared.Train.Vehicle;

/// <summary>
/// This system handles vehicles that run on train tracks.
/// </summary>
public abstract partial class SharedTrainVehicleSystem : EntitySystem
{
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    //[Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly TrainVehicleLocomotorSystem _locomotor = default!;

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
        ent.Comp.Container = _container.EnsureContainer<Container>(ent, nameof(TrainVehicleComponent));
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

        // Eject contents
        if (ent.Comp.EjectContentsOnDerailment)
        {
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
                    //_damageable.TryChangeDamage(held, ent.Comp.DerailmentDamage);
                }

                // Throw the entity
                _throwing.TryThrow(held,
                    _xform.GetWorldRotation(ent, _xformQuery).ToWorldVec() * ent.Comp.ExitDistanceMultiplier,
                    ent.Comp.CurrentSpeed * ent.Comp.DerailmentSpeedMultiplier);
            }

            // Expel contained atmosphere
            ExpelAtmos(ent);
        }

        // Alter physics
        if (TryComp<PhysicsComponent>(ent, out var body))
        {
            //var velocity = body.LinearVelocity.Normalized() * ent.Comp.CurrentSpeed;
            //_physics.SetBodyType(ent, BodyType.Dynamic, null, body, xform);
            //_physics.SetLinearVelocity(ent, velocity);
        }

        // Remove the vehicle from the track and set its speed to zero
        ent.Comp.IsDerailed = true;
        SetSpeed(ent, 0, true);

        Dirty(ent);

        // Raise after derailment event
        var afterEv = new AfterTrainVehicleDerailmentEvent();
        RaiseLocalEvent(ent, ref afterEv);
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
    /// Sets the target speed of a train vehicle.
    /// </summary>
    /// <param name="ent">The vehicle.</param>
    /// <param name="targetSpeed">The new target speed.</param>
    public void SetTargetSpeed(Entity<TrainVehicleComponent> ent, float targetSpeed)
    {
        if (ent.Comp.IsDerailed)
            return;

        ent.Comp.TargetSpeed = Math.Clamp(targetSpeed, ent.Comp.TraversalSpeed.X, ent.Comp.TraversalSpeed.Y);
        Dirty(ent);
    }

    /// <summary>
    /// Sets the current speed of a train vehicle.
    /// </summary>
    /// <param name="ent">The vehicle.</param>
    /// <param name="speed">The new speed.</param>
    /// <param name="updateTargetSpeed">Whether the target speed should be set to the new speed.</param>
    public void SetSpeed(Entity<TrainVehicleComponent> ent, float speed, bool updateTargetSpeed = false)
    {
        if (ent.Comp.IsDerailed)
        {
            speed = 0;
        }
        else if (ent.Comp.IsReversing)
        {
            speed = Math.Clamp(speed, ent.Comp.TraversalSpeed.X, 0);
        }
        else
        {
            speed = Math.Clamp(speed, 0, ent.Comp.TraversalSpeed.Y);
        }

        if (updateTargetSpeed)
        {
            ent.Comp.TargetSpeed = speed;
        }

        ent.Comp.CurrentSpeed = speed;
        Dirty(ent);

        if (TryComp<TrainVehicleLocomotorComponent>(ent, out var loco))
        {
            _locomotor.SetSpeed((ent, loco), ent);
            return;
        }

        if (TryComp<TrainCompositeVehicleComponent>(ent, out var composite))
        {
            if (TryComp<TrainVehicleLocomotorComponent>(composite.ForwardLocomotorJointUid, out var fLoco))
                _locomotor.SetSpeed((composite.ForwardLocomotorJointUid, fLoco), ent);

            if (TryComp<TrainVehicleLocomotorComponent>(composite.RearLocomotorJointUid, out var rLoco))
                _locomotor.SetSpeed((composite.RearLocomotorJointUid, rLoco), ent);
        }
    }

    public void ReverseTraversalDirection(Entity<TrainVehicleComponent> ent)
    {
        ent.Comp.IsReversing = !ent.Comp.IsReversing;

        if (TryComp<TrainVehicleLocomotorComponent>(ent, out var loco))
        {
            _locomotor.ReverseTraversalDirection((ent, loco));
            return;
        }

        if (TryComp<TrainCompositeVehicleComponent>(ent, out var composite))
        {
            if (TryComp<TrainVehicleLocomotorComponent>(composite.ForwardLocomotorJointUid, out var fLoco))
                _locomotor.ReverseTraversalDirection((composite.ForwardLocomotorJointUid, fLoco));

            if (TryComp<TrainVehicleLocomotorComponent>(composite.RearLocomotorJointUid, out var rLoco))
                _locomotor.ReverseTraversalDirection((composite.RearLocomotorJointUid, rLoco));
        }

        Dirty(ent);
    }

    /// <summary>
    /// Get the speed of a train vehicle for movement calculations.
    /// </summary>
    /// <param name="ent">The vehicle.</param>
    /// <returns>The vehicle's current speed.</returns>
    public float GetEffectiveSpeed(Entity<TrainVehicleComponent> ent)
    {
        return MathF.Abs(ent.Comp.CurrentSpeed);
    }

    /// <summary>
    /// Sets a train's DirectionChangeCount to zero.
    /// </summary>
    /// <param name="ent">The vehicle.</param>
    public void ResetDirectionChangeCounter(Entity<TrainVehicleComponent> ent)
    {

    }

    /// <summary>
    /// Returns whether a train vehicle is at a station.
    /// </summary>
    /// <param name="ent">The vehicle.</param>
    /// <param name="station">The station the vehicle is at.</param>
    /// <returns>True if the vehicle is at a station.</returns>
    public bool IsAtStation(Entity<TrainVehicleComponent> ent, [NotNullWhen(true)] out Entity<TrainStationComponent>? station)
    {
        station = null;

        if (ent.Comp.IsDerailed)
            return false;

        var xform = _xformQuery.GetComponent(ent);

        if (!TryComp(xform.GridUid, out MapGridComponent? mapGrid))
            return false;

        TrainStationComponent? foundTrainStation = null;
        var foundUid = _map.GetLocal(xform.GridUid.Value, mapGrid, xform.Coordinates)?
            .FirstOrNull(x => TryComp(x, out foundTrainStation));

        if (foundUid == null || foundTrainStation == null)
            return false;

        station = new Entity<TrainStationComponent>(foundUid.Value, foundTrainStation);

        return true;
    }

    public override void Update(float dt)
    {
        var query = EntityQueryEnumerator<TrainVehicleComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var trainVehicle, out var meta))
        {
            if (Paused(uid, meta))
                return;

            UpdateSpeed((uid, trainVehicle), dt);
        }
    }

    private void UpdateSpeed(Entity<TrainVehicleComponent> ent, float dt)
    {
        if (MathHelper.CloseTo(ent.Comp.CurrentSpeed, ent.Comp.TargetSpeed))
            return;

        var dV = ent.Comp.CurrentSpeed < ent.Comp.TargetSpeed
            ? ent.Comp.Acceleration.Y * dt
            : -ent.Comp.Acceleration.X * dt;

        SetSpeed(ent, ent.Comp.CurrentSpeed + dV);
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
    public virtual bool TryDerailing(Entity<TrainVehicleComponent> ent, Entity<TrainTrackComponent> track)
    {
        // Handled by the server

        return false;
    }
}
