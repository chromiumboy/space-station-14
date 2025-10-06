using Content.Shared.ActionBlocker;
using Content.Shared.Administration.Logs;
using Content.Shared.Climbing.Systems;
using Content.Shared.Construction.EntitySystems;
using Content.Shared.Containers;
using Content.Shared.Database;
using Content.Shared.Destructible;
using Content.Shared.Disposal.Components;
using Content.Shared.Disposal.Unit.Events;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Emag.Systems;
using Content.Shared.Explosion;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Movement.Events;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Storage.Components;
using Content.Shared.Throwing;
using Content.Shared.Train.Station;
using Content.Shared.Train.Track;
using Content.Shared.Train.Vehicle;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using System.Linq;

namespace Content.Shared.Train.Station;

/// <summary>
/// This system handles all operations relating to disposal units.
/// </summary>
public abstract class SharedTrainStationSystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _actionBlockerSystem = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelistSystem = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly ClimbSystem _climb = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedJointSystem _joints = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTrainVehicleSystem _trainVehicle = default!;
    [Dependency] private readonly TrainTrackSystem _trainTrack = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TrainStationComponent, DestructionEventArgs>(OnDestruction);
        SubscribeLocalEvent<TrainStationComponent, BeforeExplodeEvent>(OnExploded);

        SubscribeLocalEvent<TrainStationComponent, PreventCollideEvent>(OnPreventCollide);
        SubscribeLocalEvent<TrainStationComponent, CanDropTargetEvent>(OnCanDragDropOn);
        SubscribeLocalEvent<TrainStationComponent, GetVerbsEvent<InteractionVerb>>(AddInsertVerb);
        SubscribeLocalEvent<TrainStationComponent, GetVerbsEvent<AlternativeVerb>>(AddAltVerbs);
        SubscribeLocalEvent<TrainStationComponent, GetVerbsEvent<Verb>>(AddEnterOrExitVerb);

        SubscribeLocalEvent<TrainStationComponent, DisposalDoAfterEvent>(OnDoAfter);

        SubscribeLocalEvent<TrainStationComponent, BeforeThrowInsertEvent>(OnThrowInsert);

        SubscribeLocalEvent<TrainStationComponent, GotEmaggedEvent>(OnEmagged);
        SubscribeLocalEvent<TrainStationComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<TrainStationComponent, PowerChangedEvent>(OnPowerChange);
        SubscribeLocalEvent<TrainStationComponent, ComponentInit>(OnComponentInit);

        SubscribeLocalEvent<TrainStationComponent, AfterInteractUsingEvent>(OnAfterInteractUsing);
        SubscribeLocalEvent<TrainStationComponent, DragDropTargetEvent>(OnDragDropOn);
        SubscribeLocalEvent<TrainStationComponent, ContainerRelayMovementEntityEvent>(OnMovement);

        SubscribeLocalEvent<TrainStationComponent, ContainerIsInsertingAttemptEvent>(OnInsertAttempt);
    }

    private void OnDestruction(Entity<TrainStationComponent> ent, ref DestructionEventArgs args)
    {
        EjectContents(ent);
    }

    private void OnExploded(Entity<TrainStationComponent> ent, ref BeforeExplodeEvent args)
    {
        args.Contents.AddRange(GetContainedEntities(ent));
    }

    private void AddAltVerbs(Entity<TrainStationComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        // Behavior for if the disposals bin has items in it
        if (GetContainedEntityCount(ent) > 0)
        {
            // Verbs to flush the unit
            AlternativeVerb flushVerb = new()
            {
                Act = () => ManualEngage(ent),
                Text = Loc.GetString("disposal-flush-verb-get-data-text"),
                Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/delete_transparent.svg.192dpi.png")),
                Priority = 1,
            };
            args.Verbs.Add(flushVerb);

            // Verb to eject the contents
            AlternativeVerb ejectVerb = new()
            {
                Act = () => EjectContents(ent),
                Category = VerbCategory.Eject,
                Text = Loc.GetString("disposal-eject-verb-get-data-text")
            };
            args.Verbs.Add(ejectVerb);
        }
    }

    private void AddInsertVerb(Entity<TrainStationComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null || args.Using == null)
            return;

        if (!_actionBlockerSystem.CanDrop(args.User))
            return;

        if (ent.Comp.Container == null || !_containers.CanInsert(args.Using.Value, ent.Comp.Container))
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

    private void OnDoAfter(Entity<TrainStationComponent> ent, ref DisposalDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Args.Target == null || args.Args.Used == null)
            return;

        Insert(ent, args.Args.Target.Value, args.Args.User, doInsert: true);

        args.Handled = true;
    }

    private void OnThrowInsert(Entity<TrainStationComponent> ent, ref BeforeThrowInsertEvent args)
    {
        if (ent.Comp.Container == null || !_containers.CanInsert(args.ThrownEntity, ent.Comp.Container))
        {
            args.Cancelled = true;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<TrainStationComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var unit, out var metadata))
        {
            Update((uid, unit), metadata);
        }
    }

    // TODO: This should just use the same thing as entity storage?
    private void OnMovement(Entity<TrainStationComponent> ent, ref ContainerRelayMovementEntityEvent args)
    {
        var currentTime = _timing.CurTime;

        if (!_actionBlockerSystem.CanMove(args.Entity))
            return;

        if (!TryComp(args.Entity, out HandsComponent? hands) ||
            hands.Count == 0 ||
            currentTime < ent.Comp.LastExitAttempt + ent.Comp.ExitAttemptDelay)
            return;

        Remove(ent, args.Entity);
        ent.Comp.LastExitAttempt = currentTime;

        Dirty(ent);
    }

    private void OnAfterInteractUsing(Entity<TrainStationComponent> ent, ref AfterInteractUsingEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        if (ent.Comp.Container == null ||
            !_containers.CanInsert(args.Used, ent.Comp.Container) ||
            !_handsSystem.TryDropIntoContainer(args.User, args.Used, ent.Comp.Container))
        {
            return;
        }

        _adminLog.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(args.User):player} inserted {ToPrettyString(args.Used)} into {ToPrettyString(ent)}");
        Insert(ent, args.Used, args.User);
        args.Handled = true;
    }

    protected virtual void OnComponentInit(Entity<TrainStationComponent> ent, ref ComponentInit args)
    {
        ent.Comp.Container = _containers.EnsureContainer<Container>(ent, nameof(TrainStationComponent));
    }

    private void OnPowerChange(Entity<TrainStationComponent> ent, ref PowerChangedEvent args)
    {
        if (!ent.Comp.Running)
            return;

        UpdateVisualState(ent);

        if (!args.Powered)
        {
            ent.Comp.NextFlush = null;
            Dirty(ent);

            return;
        }

        if (ent.Comp.Engaged)
        {
            // Run ManualEngage to recalculate a new flush time
            ManualEngage(ent);
        }
    }

    private void OnAnchorChanged(Entity<TrainStationComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (Terminating(ent))
            return;

        UpdateVisualState(ent);

        if (!args.Anchored)
            EjectContents(ent);
    }

    private void OnDragDropOn(Entity<TrainStationComponent> ent, ref DragDropTargetEvent args)
    {
        args.Handled = TryInsert(ent, args.Dragged, args.User);
    }

    /// <summary>
    /// Returns the estimated time when the disposal unit will be back to full pressure.
    /// </summary>
    /// <param name="ent">The disposal unit.</param>
    /// <returns>The estimated time.</returns>
    public TimeSpan EstimatedFullPressure(Entity<TrainStationComponent> ent)
    {
        if (ent.Comp.NextPressurized < _timing.CurTime)
            return TimeSpan.Zero;

        return ent.Comp.NextPressurized;
    }

    /// <summary>
    /// Checks whether a disposal unit can flush.
    /// </summary>
    /// <param name="ent">The disposal unit.</param>
    /// <returns>True if the disposal unit can flush.</returns>
    public bool CanFlush(Entity<TrainStationComponent> ent)
    {
        return GetState(ent) == TrainStationState.Ready
               && _power.IsPowered(ent.Owner)
               && Comp<TransformComponent>(ent).Anchored;
    }

    /// <summary>
    /// Remove an entity from a disposal unit.
    /// </summary>
    /// <param name="ent">The disposal unit.</param>
    /// <param name="toRemove">The entity to remove.</param>
    public void Remove(Entity<TrainStationComponent> ent, EntityUid toRemove)
    {
        if (_timing.ApplyingState)
            return;

        if (ent.Comp.Container == null || !_containers.Remove(toRemove, ent.Comp.Container))
            return;

        // If not manually engaged then reset the flushing entirely.
        if (ent.Comp.Container.ContainedEntities.Count == 0 &&
            !ent.Comp.Engaged)
        {
            ent.Comp.NextFlush = null;
            Dirty(ent);
        }

        _climb.Climb(toRemove, toRemove, ent, silent: true);

        UpdateVisualState(ent);
    }

    /// <summary>
    /// Updates the appearance data of a disposal unit.
    /// </summary>
    /// <param name="ent">The disposal unit.</param>
    public void UpdateVisualState(Entity<TrainStationComponent> ent)
    {
        if (!TryComp(ent, out AppearanceComponent? appearance))
            return;

        var isAnchored = Transform(ent).Anchored;
        _appearance.SetData(ent, AnchorVisuals.Anchored, isAnchored, appearance);

        if (!isAnchored)
            return;

        var state = GetState(ent);
        _appearance.SetData(ent, DisposalUnitVisuals.IsReady, state == TrainStationState.Ready, appearance);
        _appearance.SetData(ent, DisposalUnitVisuals.IsFlushing, state == TrainStationState.Flushed, appearance);
        _appearance.SetData(ent, DisposalUnitVisuals.IsEngaged, ent.Comp.Engaged, appearance);

        if (!_power.IsPowered(ent.Owner))
            return;

        _appearance.SetData(ent, DisposalUnitVisuals.IsFull, GetContainedEntityCount(ent) > 0, appearance);
        _appearance.SetData(ent, DisposalUnitVisuals.IsCharging, state != TrainStationState.Ready, appearance);
    }

    /// <summary>
    /// Gets the current pressure state of a disposals unit.
    /// </summary>
    /// <param name="ent">The disposal unit.</param>
    /// <param name="metadata"></param>
    /// <returns>The disposal unit's pressure state.</returns>
    public TrainStationState GetState(Entity<TrainStationComponent> ent, MetaDataComponent? metadata = null)
    {
        var nextPressure = ent.Comp.NextPressurized - _timing.CurTime;
        var pressurizeTime = 1f / ent.Comp.PressurePerSecond;
        var pressurizeDuration = pressurizeTime - ent.Comp.FlushDelay.TotalSeconds;

        if (nextPressure.TotalSeconds > pressurizeDuration)
        {
            return TrainStationState.Flushed;
        }

        if (nextPressure > TimeSpan.Zero)
        {
            return TrainStationState.Pressurizing;
        }

        return TrainStationState.Ready;
    }

    protected void OnPreventCollide(Entity<TrainStationComponent> ent, ref PreventCollideEvent args)
    {
        var otherBody = args.OtherEntity;

        // Items dropped shouldn't collide but items thrown should
        if (HasComp<ItemComponent>(otherBody) && !HasComp<ThrownItemComponent>(otherBody))
        {
            args.Cancelled = true;
        }
    }

    protected void OnCanDragDropOn(Entity<TrainStationComponent> ent, ref CanDropTargetEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.Container == null)
            return;

        args.CanDrop = _containers.CanInsert(args.Dragged, ent.Comp.Container);
        args.Handled = true;
    }

    protected void OnEmagged(Entity<TrainStationComponent> ent, ref GotEmaggedEvent args)
    {
        ent.Comp.DisablePressure = true;
        args.Handled = true;
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

    /// <summary>
    /// Insert an entity into a disposal unit.
    /// </summary>
    /// <param name="ent">The disposal unit.</param>
    /// <param name="toInsert">The entity to insert.</param>
    /// <param name="user">The one inserting the entity.</param>
    public void DoInsertDisposalUnit(Entity<TrainStationComponent> ent, EntityUid toInsert, EntityUid user)
    {
        if (ent.Comp.Container == null || !_containers.Insert(toInsert, ent.Comp.Container))
            return;

        _adminLog.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(user):player} inserted {ToPrettyString(toInsert)} into {ToPrettyString(ent)}");
        Insert(ent, toInsert, user);
    }

    /// <summary>
    /// Handles the actual insertion of an entity into a disposal unit.
    /// </summary>
    /// <param name="ent">The disposal unit.</param>
    /// <param name="inserted">The entity inserted.</param>
    /// <param name="user">The one who inserted the entity.</param>
    /// <param name="doInsert">Do the insertion now.</param>
    public void Insert(Entity<TrainStationComponent> ent,
        EntityUid inserted,
        EntityUid? user = null,
        bool doInsert = false)
    {
        if (doInsert && (ent.Comp.Container == null || !_containers.Insert(inserted, ent.Comp.Container)))
            return;

        if (_timing.CurTime >= ent.Comp.NextAllowedInsertSound)
        {
            _audio.PlayPredicted(ent.Comp.InsertSound, ent, user: user);
            ent.Comp.NextAllowedInsertSound = _timing.CurTime + ent.Comp.InsertSoundDelay;
        }

        if (user != inserted && user != null)
            _adminLog.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(user.Value):player} inserted {ToPrettyString(inserted)} into {ToPrettyString(ent)}");

        QueueAutomaticEngage(ent);

        // Maybe do pullable instead? Eh still fine.
        _joints.RecursiveClearJoints(inserted);
        UpdateVisualState(ent);
    }

    /// <summary>
    /// Tries to insert an entity into a disposal unit.
    /// </summary>
    /// <param name="ent">The disposal unit.</param>
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

        if (ent.Comp.Container == null || !_containers.CanInsert(toInsert, ent.Comp.Container))
            return false;

        bool insertingSelf = user == toInsert;

        var delay = insertingSelf ? ent.Comp.EntryDelay : ent.Comp.DraggedEntryDelay;

        if (user != null && !insertingSelf)
            _popupSystem.PopupEntity(Loc.GetString("disposal-unit-being-inserted", ("user", Identity.Entity((EntityUid)user, EntityManager))), toInsert, toInsert, PopupType.Large);

        if (delay <= 0 || user == null)
        {
            Insert(ent, toInsert, user, doInsert: true);
            return true;
        }

        // Can't check if our target AND disposals moves currently so we'll just check target.
        // if you really want to check if disposals moves then add a predicate.
        var doAfterArgs = new DoAfterArgs(EntityManager, user.Value, delay, new DisposalDoAfterEvent(), ent, target: toInsert, used: ent)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = false,
        };

        _doAfterSystem.TryStartDoAfter(doAfterArgs);
        return true;
    }

    private void UpdateState(Entity<TrainStationComponent> ent, TrainStationState state)
    {
        if (ent.Comp.State == state)
            return;

        ent.Comp.State = state;

        UpdateVisualState(ent);
        Dirty(ent);

        if (state == TrainStationState.Ready)
        {
            ent.Comp.NextPressurized = TimeSpan.Zero;

            // Manually engaged
            if (ent.Comp.Engaged)
            {
                ent.Comp.NextFlush = _timing.CurTime + ent.Comp.ManualFlushTime;
            }
            else if (GetContainedEntityCount(ent) > 0)
            {
                ent.Comp.NextFlush = _timing.CurTime + ent.Comp.AutomaticEngageTime;
            }
            else
            {
                ent.Comp.NextFlush = null;
            }
        }
    }

    /// <summary>
    /// Work out if we can stop updating this disposals component
    /// (i.e. full pressure and nothing colliding).
    /// </summary>
    private void Update(Entity<TrainStationComponent> ent, MetaDataComponent metadata)
    {
        var state = GetState(ent, metadata);

        // Pressurizing, just check if we need a state update.
        if (ent.Comp.NextPressurized > _timing.CurTime)
        {
            UpdateState(ent, state);
            return;
        }

        // Check if we need to flush
        if (ent.Comp.NextFlush != null &&
            ent.Comp.NextFlush.Value < _timing.CurTime)
        {
            TryFlush(ent);
        }

        UpdateState(ent, state);
    }

    /// <summary>
    /// Try to flush the disposal unit.
    /// </summary>
    /// <param name="ent">The disposal unit.</param>
    /// <returns>True if the flush was successful.</returns>
    public bool TryFlush(Entity<TrainStationComponent> ent)
    {
        if (!CanFlush(ent))
        {
            return false;
        }

        if (ent.Comp.NextFlush != null)
            ent.Comp.NextFlush = ent.Comp.NextFlush.Value + ent.Comp.AutomaticEngageTime;

        var beforeFlushArgs = new BeforeDisposalFlushEvent();
        RaiseLocalEvent(ent, beforeFlushArgs);

        if (beforeFlushArgs.Cancelled)
        {
            Disengage(ent);
            return false;
        }

        var xform = Transform(ent);
        if (!TryComp(xform.GridUid, out MapGridComponent? grid))
            return false;

        TrainTrackComponent? tubeComp = null;

        var coords = xform.Coordinates;
        var tubeUid = _map.GetLocal(xform.GridUid.Value, grid, coords)
            .FirstOrNull(x => TryComp(x, out tubeComp));

        if (tubeUid == null || tubeComp == null)
        {
            ent.Comp.Engaged = false;

            UpdateVisualState(ent);
            Dirty(ent);

            return false;
        }

        // Try to transfer entities from the unit into disposals.
        // If successful, take in the local atmos and pass it
        // to the spawned disposals holder.
        if (ent.Comp.Container != null &&
            _trainTrack.TryInsert
                ((tubeUid.Value, tubeComp),
                ent.Comp.Container.ContainedEntities.ToArray(),
                ent.Comp.HolderPrototypeId,
                out var holderEnt,
                beforeFlushArgs.Tags))
        {
            IntakeAir(ent, xform);
            _trainVehicle.TransferAtmos(holderEnt.Value, ent);
        }

        ent.Comp.NextPressurized = _timing.CurTime;
        if (!ent.Comp.DisablePressure)
            ent.Comp.NextPressurized += TimeSpan.FromSeconds(1f / ent.Comp.PressurePerSecond);

        ent.Comp.Engaged = false;
        ent.Comp.NextFlush = null;

        UpdateVisualState(ent);
        Dirty(ent);

        return true;
    }

    /// <summary>
    /// Sets a disposal unit to move towards flushing itself.
    /// </summary>
    /// <param name="ent">The disposal unit.</param>
    /// <param name="metadata">The disposal unit's metadata.</param>
    public void ManualEngage(Entity<TrainStationComponent> ent, MetaDataComponent? metadata = null)
    {
        ent.Comp.Engaged = true;

        UpdateVisualState(ent);
        Dirty(ent);

        if (!CanFlush(ent))
            return;

        if (!Resolve(ent, ref metadata))
            return;

        var pauseTime = _metaData.GetPauseTime(ent, metadata);
        var nextEngage = _timing.CurTime - pauseTime + ent.Comp.ManualFlushTime;
        ent.Comp.NextFlush = TimeSpan.FromSeconds(Math.Min((ent.Comp.NextFlush ?? TimeSpan.MaxValue).TotalSeconds, nextEngage.TotalSeconds));
    }

    /// <summary>
    /// Sets a disposal unit so it is no longer moving towards flushing itself.
    /// </summary>
    /// <param name="ent">The disposal unit.</param>
    public void Disengage(Entity<TrainStationComponent> ent)
    {
        ent.Comp.Engaged = false;

        if (GetContainedEntityCount(ent) == 0)
        {
            ent.Comp.NextFlush = null;
        }

        UpdateVisualState(ent);
        Dirty(ent);
    }

    /// <summary>
    /// Remove all entities currently in a disposal unit.
    /// </summary>
    /// <param name="ent">The disposal unit.</param>
    public void EjectContents(Entity<TrainStationComponent> ent)
    {
        foreach (var toRemove in GetContainedEntities(ent))
        {
            Remove(ent, toRemove);
        }
    }

    /// <summary>
    /// Primes a disposal unit to automatically flush sometime in the future.
    /// </summary>
    /// <param name="ent">The disposal unit.</param>
    /// <param name="metadata">The disposal unit's metadata.</param>
    public void QueueAutomaticEngage(Entity<TrainStationComponent> ent, MetaDataComponent? metadata = null)
    {
        if (ent.Comp.Deleted || !ent.Comp.AutomaticEngage || !_power.IsPowered(ent.Owner) && GetContainedEntityCount(ent) == 0)
        {
            return;
        }

        var pauseTime = _metaData.GetPauseTime(ent, metadata);
        var automaticTime = _timing.CurTime + ent.Comp.AutomaticEngageTime - pauseTime;
        var flushTime = TimeSpan.FromSeconds(Math.Min((ent.Comp.NextFlush ?? TimeSpan.MaxValue).TotalSeconds, automaticTime.TotalSeconds));

        ent.Comp.NextFlush = flushTime;
        Dirty(ent);
    }

    /// <summary>
    /// Toggles a disposal unit between 'engaged' and 'disengaged'.
    /// </summary>
    /// <param name="ent">The disposal unit.</param>
    public void ToggleEngage(Entity<TrainStationComponent> ent)
    {
        ent.Comp.Engaged ^= true;

        if (ent.Comp.Engaged)
        {
            ManualEngage(ent);
        }
        else
        {
            Disengage(ent);
        }
    }

    private void AddEnterOrExitVerb(Entity<TrainStationComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (ent.Comp.Container == null)
            return;

        // This is not an interaction, activation, or alternative verb type because unfortunately most users are
        // unwilling to accept that this is where they belong and don't want to accidentally climb inside.
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
            if (!_containers.CanInsert(args.User, ent.Comp.Container))
                return;

            // Verb for climbing in
            verb.Act = () => TryInsert(ent, verbData.User, verbData.User);
            verb.Text = Loc.GetString("verb-common-enter");
            verb.Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/close.svg.192dpi.png"));
        }
        else
        {
            // Verb for climbing out
            verb.Act = () => Remove(ent, verbData.User);
            verb.Text = Loc.GetString("verb-common-exit");
            verb.Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/open.svg.192dpi.png"));
        }

        args.Verbs.Add(verb);
    }

    /// <summary>
    /// All entities contained in a disposal unit.
    /// </summary>
    /// <param name="ent">The disposal unit.</param>
    /// <returns>A copy of the disposal unit's ContainedEntities list.</returns>
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
    /// Takes the atmos surrounding the station into itself.
    /// </summary>
    /// <param name="ent">The station.</param>
    /// <param name="xform">The station's transform.</param>
    protected virtual void IntakeAir(Entity<TrainStationComponent> ent, TransformComponent xform)
    {
        // Handled by the server
    }
}
