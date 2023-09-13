using System.Text;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.PowerCell.Components;
using Content.Shared.Rejuvenate;
using Content.Shared.Tools.Components;
using Robust.Shared.Containers;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.PowerCell;

public abstract class SharedPowerCellSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PowerCellSlotComponent, RejuvenateEvent>(OnRejuventate);
        SubscribeLocalEvent<PowerCellSlotComponent, EntInsertedIntoContainerMessage>(OnCellInserted);
        SubscribeLocalEvent<PowerCellSlotComponent, EntRemovedFromContainerMessage>(OnCellRemoved);
        SubscribeLocalEvent<PowerCellSlotComponent, ContainerIsInsertingAttemptEvent>(OnCellInsertAttempt);
        SubscribeLocalEvent<PowerCellSlotComponent, ContainerIsRemovingAttemptEvent>(OnCellRemovalAttempt);

        SubscribeLocalEvent<PowerCellSlotCoverComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<PowerCellSlotCoverComponent, TogglePowerCellSlotCoverEvent>(OnTogglePowerCellSlotCover);
        SubscribeLocalEvent<PowerCellSlotCoverComponent, TogglePowerCellSlotCoverLockEvent>(OnTogglePowerCellSlotCoverLock);
        SubscribeLocalEvent<PowerCellSlotCoverComponent, ExaminedEvent>(OnExamine);
    }
    private void OnRejuventate(EntityUid uid, PowerCellSlotComponent component, RejuvenateEvent args)
    {
        if (!_itemSlots.TryGetSlot(uid, component.CellSlotId, out var itemSlot) || !itemSlot.Item.HasValue)
            return;

        // charge entity batteries and remove booby traps.
        RaiseLocalEvent(itemSlot.Item.Value, args);
    }

    private void OnCellInsertAttempt(EntityUid uid, PowerCellSlotComponent component, ContainerIsInsertingAttemptEvent args)
    {
        if (!component.Initialized)
            return;

        if (args.Container.ID != component.CellSlotId)
            return;

        if (!HasComp<PowerCellComponent>(args.EntityUid))
        {
            args.Cancel();
        }

        if (TryComp<PowerCellSlotCoverComponent>(args.Container.Owner, out var cover) &&
            cover.CoverState == PowerCellCoverState.Closed &&
            EntityManager.CurrentTick > component.CreationTick) // Required so that batteries can be inserted when the entity initializes 
        {
            args.Cancel();
        }
    }

    private void OnCellRemovalAttempt(EntityUid uid, PowerCellSlotComponent component, ContainerIsRemovingAttemptEvent args)
    {
        if (args.Container.ID != component.CellSlotId)
            return;

        if (TryComp<PowerCellSlotCoverComponent>(args.Container.Owner, out var cover) &&
            cover.CoverState == PowerCellCoverState.Closed)
        {
            args.Cancel();
        }
    }

    private void OnCellInserted(EntityUid uid, PowerCellSlotComponent component, EntInsertedIntoContainerMessage args)
    {
        if (!component.Initialized)
            return;

        if (args.Container.ID != component.CellSlotId)
            return;
        _appearance.SetData(uid, PowerCellSlotVisuals.Enabled, true);
        RaiseLocalEvent(uid, new PowerCellChangedEvent(false), false);
    }

    protected virtual void OnCellRemoved(EntityUid uid, PowerCellSlotComponent component, EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != component.CellSlotId)
            return;
        _appearance.SetData(uid, PowerCellSlotVisuals.Enabled, false);
        RaiseLocalEvent(uid, new PowerCellChangedEvent(true), false);
    }

    private void OnInteractUsing(EntityUid uid, PowerCellSlotCoverComponent component, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (TryComp<ToolComponent>(args.Used, out var tool) && tool.Qualities.Contains("Prying"))
        {
            if (component.LockState == PowerCellCoverLockState.Engaged)
            {
                _popup.PopupClient(Loc.GetString("power-cell-slot-cover-lock-engaged"),
                    uid, args.User, PopupType.Small);

                args.Handled = true;
                return;
            }

            var doAfterEventArgs = new DoAfterArgs(EntityManager, args.User, component.CoverPryingDelay, new TogglePowerCellSlotCoverEvent(), uid, target: uid, used: args.Target)
            {
                BreakOnTargetMove = true,
                BreakOnUserMove = true,
            };

            _doAfter.TryStartDoAfter(doAfterEventArgs);
        }
    }

    private void OnTogglePowerCellSlotCover(EntityUid uid, PowerCellSlotCoverComponent component, TogglePowerCellSlotCoverEvent args)
    {
        if (component.LockState == PowerCellCoverLockState.Engaged)
            return;

        switch (component.CoverState)
        {
            case PowerCellCoverState.Closed:
                component.CoverState = PowerCellCoverState.Open;
                break;
            case PowerCellCoverState.Open:
                component.CoverState = PowerCellCoverState.Closed;
                break;
        }

        Dirty(uid, component);
    }

    private void OnTogglePowerCellSlotCoverLock(EntityUid uid, PowerCellSlotCoverComponent component, TogglePowerCellSlotCoverLockEvent args)
    {
        if (component.LockState == PowerCellCoverLockState.Disabled)
            return;

        switch (component.LockState)
        {
            case PowerCellCoverLockState.Disengaged:
                component.LockState = PowerCellCoverLockState.Engaged;
                break;
            case PowerCellCoverLockState.Engaged:
                component.LockState = PowerCellCoverLockState.Disengaged;
                break;
        }

        Dirty(uid, component);
    }

    private void OnExamine(EntityUid uid, PowerCellSlotCoverComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(component.CoverState == PowerCellCoverState.Closed
            ? Loc.GetString("power-cell-slot-cover-examine-closed-cover")
            : Loc.GetString("power-cell-slot-cover-examine-open-cover"));

        if (component.LockState != PowerCellCoverLockState.Disabled)
        {
            args.PushMarkup(component.LockState == PowerCellCoverLockState.Engaged
                ? Loc.GetString("power-cell-slot-cover-examine-locked-cover")
                : Loc.GetString("power-cell-slot-cover-examine-unlocked-cover"));
        }
    }
}

/// <summary>
///     Event raised when the power cell slot cover is opened/closed
/// </summary>
[Serializable, NetSerializable]
public sealed partial class TogglePowerCellSlotCoverEvent : SimpleDoAfterEvent { }

/// <summary>
///     Event raised when the power cell slot cover lock is engaged/disengaged
/// </summary>
[Serializable, NetSerializable]
public sealed partial class TogglePowerCellSlotCoverLockEvent : SimpleDoAfterEvent { }
