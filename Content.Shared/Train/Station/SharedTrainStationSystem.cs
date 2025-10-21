using Content.Shared.ActionBlocker;
using Content.Shared.Administration.Logs;
using Content.Shared.Climbing.Systems;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Explosion;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Movement.Events;
using Content.Shared.Popups;
using Content.Shared.Train.Vehicle;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using System.Linq;

namespace Content.Shared.Train.Station;

/// <summary>
/// This system handles all operations relating to train stations.
/// </summary>
public abstract class SharedTrainStationSystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _actionBlockerSystem = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelistSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly ClimbSystem _climb = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedJointSystem _joints = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private readonly SharedTrainVehicleSystem _trainVehicle = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TrainStationComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<TrainStationComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<TrainStationComponent, BeforeExplodeEvent>(OnExploded);

        SubscribeLocalEvent<TrainStationComponent, GetVerbsEvent<InteractionVerb>>(AddInsertVerb);
        SubscribeLocalEvent<TrainStationComponent, GetVerbsEvent<AlternativeVerb>>(AddAltVerbs);
        SubscribeLocalEvent<TrainStationComponent, GetVerbsEvent<Verb>>(AddEnterOrExitVerb);
        SubscribeLocalEvent<TrainStationComponent, CanDropTargetEvent>(OnCanDragDropOn);
        SubscribeLocalEvent<TrainStationComponent, TrainStationDoAfterEvent>(OnDoAfter);

        SubscribeLocalEvent<TrainStationComponent, AnchorStateChangedEvent>(OnAnchorChanged);

        SubscribeLocalEvent<TrainStationComponent, ContainerIsInsertingAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<TrainStationComponent, AfterInteractUsingEvent>(OnAfterInteractUsing);
        SubscribeLocalEvent<TrainStationComponent, DragDropTargetEvent>(OnDragDropOn);
        SubscribeLocalEvent<TrainStationComponent, ContainerRelayMovementEntityEvent>(OnMovement);

        SubscribeLocalEvent<TrainStationComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<TrainStationComponent, EndCollideEvent>(OnEndCollide);
    }

    protected virtual void OnInit(Entity<TrainStationComponent> ent, ref ComponentInit args)
    {
        // Ensures that the station has a container
        ent.Comp.Container = _container.EnsureContainer<Container>(ent, nameof(TrainStationComponent));
    }

    private void OnShutdown(Entity<TrainStationComponent> ent, ref ComponentShutdown args)
    {
        // Eject contents on shutdown
        EjectContents(ent);
    }

    private void OnExploded(Entity<TrainStationComponent> ent, ref BeforeExplodeEvent args)
    {
        // Transfer explosion damage to contained entities
        args.Contents.AddRange(GetContainedEntities(ent));
    }

    private void AddAltVerbs(Entity<TrainStationComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        // Add verb to eject contents

        if (!args.CanAccess || !args.CanInteract || args.Hands == null || args.Using == null)
            return;

        if (GetContainedEntityCount(ent) == 0)
            return;

        AlternativeVerb ejectVerb = new()
        {
            Act = () => EjectContents(ent),
            Category = VerbCategory.Eject,
            Text = Loc.GetString("disposal-eject-verb-get-data-text")
        };

        args.Verbs.Add(ejectVerb);
    }

    private void AddInsertVerb(Entity<TrainStationComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        // Verb to insert an entity

        if (!args.CanAccess || !args.CanInteract || args.Hands == null || args.Using == null)
            return;

        if (!_actionBlockerSystem.CanDrop(args.User))
            return;

        if (ent.Comp.Container == null || !_container.CanInsert(args.Using.Value, ent.Comp.Container))
            return;

        var verbData = args;

        InteractionVerb insertVerb = new()
        {
            Text = Name(args.Using.Value),
            Category = VerbCategory.Insert,
            Act = () =>
            {
                _handsSystem.TryDropIntoContainer((verbData.User, verbData.Hands), verbData.Using.Value, ent.Comp.Container, checkActionBlocker: false);
                _adminLog.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(verbData.User):player} inserted {ToPrettyString(verbData.Using.Value)} into {ToPrettyString(ent)}");
                Insert(ent, verbData.Using.Value, verbData.User);
            }
        };

        args.Verbs.Add(insertVerb);
    }

    private void AddEnterOrExitVerb(Entity<TrainStationComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        // Add a verb to enter or exit a station

        if (ent.Comp.Container == null)
            return;

        if (!args.CanAccess ||
            !args.CanInteract ||
            !_actionBlockerSystem.CanMove(args.User))
        {
            return;
        }

        var verbData = args;
        var verb = new Verb()
        {
            DoContactInteraction = true
        };

        if (!GetContainedEntities(ent).Contains(args.User))
        {
            if (!_container.CanInsert(args.User, ent.Comp.Container))
                return;

            // Verb for entering
            verb.Act = () => TryInsert(ent, verbData.User, verbData.User);
            verb.Text = Loc.GetString("verb-common-enter");
            verb.Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/close.svg.192dpi.png"));
        }
        else
        {
            // Verb for exiting
            verb.Act = () => Remove(ent, verbData.User);
            verb.Text = Loc.GetString("verb-common-exit");
            verb.Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/open.svg.192dpi.png"));
        }

        args.Verbs.Add(verb);
    }

    protected void OnCanDragDropOn(Entity<TrainStationComponent> ent, ref CanDropTargetEvent args)
    {
        // Allow things to be drag n' dropped into the station

        if (args.Handled)
            return;

        if (ent.Comp.Container == null)
            return;

        args.CanDrop = _container.CanInsert(args.Dragged, ent.Comp.Container);
        args.Handled = true;
    }

    private void OnDoAfter(Entity<TrainStationComponent> ent, ref TrainStationDoAfterEvent args)
    {
        // Inserts entity when a do=after finishes

        if (args.Handled || args.Cancelled || args.Args.Target == null || args.Args.Used == null)
            return;

        Insert(ent, args.Args.Target.Value, args.Args.User, doInsert: true);

        args.Handled = true;
    }

    private void OnAnchorChanged(Entity<TrainStationComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (!args.Anchored)
        {
            EjectContents(ent);
        }
    }

    private void OnInsertAttempt(Entity<TrainStationComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (GetContainedEntityCount(ent) >= ent.Comp.MaxCapacity)
        {
            // TODO: If ContainerIsInsertingAttemptEvent ever ends up having the user
            // attached to the event, we'll be able to predict the pop up
            _popupSystem.PopupPredicted(Loc.GetString("disposal-unit-is-full"), ent, null);

            args.Cancel();
            return;
        }

        if (!Transform(ent).Anchored)
        {
            args.Cancel();
            return;
        }

        if (_whitelistSystem.IsBlacklistPass(ent.Comp.Blacklist, args.EntityUid) ||
            _whitelistSystem.IsWhitelistFail(ent.Comp.Whitelist, args.EntityUid))
        {
            args.Cancel();
            return;
        }
    }

    private void OnAfterInteractUsing(Entity<TrainStationComponent> ent, ref AfterInteractUsingEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        if (ent.Comp.Container == null ||
            !_container.CanInsert(args.Used, ent.Comp.Container) ||
            !_handsSystem.TryDropIntoContainer(args.User, args.Used, ent.Comp.Container))
        {
            return;
        }

        _adminLog.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(args.User):player} inserted {ToPrettyString(args.Used)} into {ToPrettyString(ent)}");
        Insert(ent, args.Used, args.User);
        args.Handled = true;
    }

    private void OnDragDropOn(Entity<TrainStationComponent> ent, ref DragDropTargetEvent args)
    {
        args.Handled = TryInsert(ent, args.Dragged, args.User);
    }

    private void OnMovement(Entity<TrainStationComponent> ent, ref ContainerRelayMovementEntityEvent args)
    {
        var currentTime = _timing.CurTime;

        if (!_actionBlockerSystem.CanMove(args.Entity))
            return;

        if (currentTime < ent.Comp.LastExitAttempt + ent.Comp.ExitAttemptDelay)
            return;

        Remove(ent, args.Entity);
        ent.Comp.LastExitAttempt = currentTime;

        Dirty(ent);
    }

    private void OnStartCollide(Entity<TrainStationComponent> ent, ref StartCollideEvent args)
    {
        if (!TryComp<TrainVehicleComponent>(args.OtherEntity, out var trainVehicle))
            return;

        if (trainVehicle.IsDerailed)
            return;

        var train = new Entity<TrainVehicleComponent>(args.OtherEntity, trainVehicle);

        var evVehicle = new TrainVehicleApproachingStationEvent(ent);
        RaiseLocalEvent(train, ref evVehicle);

        var evStation = new TrainStationHasVehicleApproachingEvent(train);
        RaiseLocalEvent(ent, ref evStation);
    }

    private void OnEndCollide(Entity<TrainStationComponent> ent, ref EndCollideEvent args)
    {
        if (!TryComp<TrainVehicleComponent>(args.OtherEntity, out var trainVehicle))
            return;

        if (trainVehicle.IsDerailed)
            return;

        var train = new Entity<TrainVehicleComponent>(args.OtherEntity, trainVehicle);

        var evVehicle = new TrainVehicleDepartingStationEvent(ent);
        RaiseLocalEvent(train, ref evVehicle);

        var evStation = new TrainStationHasVehicleDepartingEvent(train);
        RaiseLocalEvent(ent, ref evStation);
    }

    /// <summary>
    /// Remove all entities currently in a train station.
    /// </summary>
    /// <param name="ent">The station.</param>
    public void EjectContents(Entity<TrainStationComponent> ent)
    {
        foreach (var toRemove in GetContainedEntities(ent))
        {
            Remove(ent, toRemove);
        }
    }

    /// <summary>
    /// Remove an entity from a train station.
    /// </summary>
    /// <param name="ent">The station.</param>
    /// <param name="toRemove">The entity to remove.</param>
    public void Remove(Entity<TrainStationComponent> ent, EntityUid toRemove)
    {
        if (_timing.ApplyingState)
            return;

        if (ent.Comp.Container == null || !_container.Remove(toRemove, ent.Comp.Container))
            return;

        _climb.Climb(toRemove, toRemove, ent, silent: true);
    }

    /// <summary>
    /// Tries to insert an entity into a train station.
    /// </summary>
    /// <param name="ent">The station.</param>
    /// <param name="toInsert">The entity to insert.</param>
    /// <param name="user">The one inserting the entity.</param>
    /// <returns>True if the entity can be inserted.</returns>
    public bool TryInsert(Entity<TrainStationComponent> ent, EntityUid toInsert, EntityUid? user)
    {
        if (user.HasValue && !HasComp<HandsComponent>(user) && toInsert != user) // Mobs like mouse can Jump inside even with no hands
        {
            _popupSystem.PopupEntity(Loc.GetString("disposal-unit-no-hands"), user.Value, user.Value, PopupType.SmallCaution);
            return false;
        }

        if (ent.Comp.Container == null || !_container.CanInsert(toInsert, ent.Comp.Container))
            return false;

        bool insertingSelf = user == toInsert;

        var delay = insertingSelf ? ent.Comp.EntryDelay : ent.Comp.DraggedEntryDelay;

        if (user != null && !insertingSelf)
        {
            _popupSystem.PopupEntity(Loc.GetString("disposal-unit-being-inserted",
                ("user", Identity.Entity((EntityUid)user, EntityManager))),
                toInsert,
                toInsert,
                PopupType.Large);
        }

        if (delay <= 0 || user == null)
        {
            Insert(ent, toInsert, user, doInsert: true);
            return true;
        }

        var doAfterArgs = new DoAfterArgs(EntityManager, user.Value, delay, new TrainStationDoAfterEvent(), ent, target: toInsert, used: ent)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = false,
            AttemptFrequency = AttemptFrequency.StartAndEnd,
        };

        _doAfterSystem.TryStartDoAfter(doAfterArgs);
        return true;
    }


    /// <summary>
    /// Handles the actual insertion of an entity into a train station.
    /// </summary>
    /// <param name="ent">The station.</param>
    /// <param name="inserted">The entity inserted.</param>
    /// <param name="user">The one who inserted the entity.</param>
    /// <param name="doInsert">Do the insertion now.</param>
    public void Insert(Entity<TrainStationComponent> ent,
        EntityUid inserted,
        EntityUid? user = null,
        bool doInsert = false)
    {
        if (doInsert && (ent.Comp.Container == null || !_container.Insert(inserted, ent.Comp.Container)))
            return;

        if (user != inserted && user != null)
            _adminLog.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(user.Value):player} inserted {ToPrettyString(inserted)} into {ToPrettyString(ent)}");

        // Maybe do pullable instead? Eh still fine.
        _joints.RecursiveClearJoints(inserted);
    }

    /// <summary>
    /// Tries to transfer any entities stored in a train station into a vehicle on it.
    /// </summary>
    /// <param name="ent">The station.</param>
    /// <param name="vehicle">The vehicle.</param>
    /// <returns>True if the transfer was successful.</returns>
    public bool TryTransfer(Entity<TrainStationComponent> ent, Entity<TrainVehicleComponent> vehicle)
    {
        if (!_trainVehicle.IsAtStation(vehicle, out var station) || station != ent)
            return false;

        if (vehicle.Comp.Container == null)
            return false;

        foreach (var uid in GetContainedEntities(ent))
        {
            _trainVehicle.TryBoardingEntity(vehicle, uid);
        }

        if (vehicle.Comp.Airtight)
        {
            IntakeAir(ent, Transform(ent));
            _trainVehicle.TransferAtmos(vehicle, ent);
        }

        return true;
    }

    /// <summary>
    /// All entities contained in a train station.
    /// </summary>
    /// <param name="ent">The station.</param>
    /// <returns>A copy of the stations's ContainedEntities list.</returns>
    public IReadOnlyList<EntityUid> GetContainedEntities(Entity<TrainStationComponent> ent)
    {
        if (ent.Comp.Container == null)
            return new List<EntityUid>();

        return ent.Comp.Container.ContainedEntities.ToArray();
    }

    /// <summary>
    /// The number of entities current inside the station.
    /// </summary>
    /// <param name="ent">The station.</param>
    /// <returns>The entity count.</returns>
    public int GetContainedEntityCount(Entity<TrainStationComponent> ent)
    {
        return GetContainedEntities(ent).Count;
    }

    /// <summary>
    /// Returns whether any train vehicles are current at a station.
    /// </summary>
    /// <param name="ent">The station.</param>
    /// <returns>True if a vehicle is at a station.</returns>
    public bool IsOccupied(Entity<TrainStationComponent> ent, EntityUid? ignored = null)
    {
        var xform = Transform(ent);

        if (!TryComp(xform.GridUid, out MapGridComponent? mapGrid))
            return false;

        var foundUid = _map.GetLocal(xform.GridUid.Value, mapGrid, xform.Coordinates).
            FirstOrNull(x => x != ignored && HasComp<TrainVehicleComponent>(x));

        if (foundUid == null)
            return false;

        return true;
    }

    /// <summary>
    /// Takes the atmos surrounding the station into itself.
    /// </summary>
    /// <param name="ent">The station.</param>
    /// <param name="xform">The station's transform.</param>
    protected virtual void IntakeAir(Entity<TrainStationComponent> ent, TransformComponent xform)
    {
        // Handled by the server
    }
}
