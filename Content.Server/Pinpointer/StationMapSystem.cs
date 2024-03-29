using Content.Server.PowerCell;
using Content.Shared.Pinpointer;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;

namespace Content.Server.Pinpointer;

public sealed class StationMapSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly PowerCellSystem _cell = default!;
    [Dependency] private readonly SharedNavMapSystem _navMapSystem = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StationMapUserComponent, EntParentChangedMessage>(OnUserParentChanged);
        SubscribeLocalEvent<AnchorStateChangedEvent>(OnNavMapBeaconAnchor);
        //SubscribeLocalEvent<NavMapBeaconComponent, MapInitEvent>(OnNavMapBeaconMapInit);

        Subs.BuiEvents<StationMapComponent>(StationMapUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnStationMapOpened);
            subs.Event<BoundUIClosedEvent>(OnStationMapClosed);
        });
    }

    private void OnNavMapBeaconMapInit(EntityUid uid, NavMapBeaconComponent component, MapInitEvent args)
    {
        var xform = Transform(uid);

        if (!TryComp<NavMapComponent>(xform.GridUid, out var navMap) ||
            !TryComp<MapGridComponent>(xform.GridUid, out var mapGrid))
            return;

        if (navMap == null)
            return;

        //var position = _mapSystem.CoordinatesToTile(xform.GridUid.Value, mapGrid, _transformSystem.GetMapCoordinates(uid, xform));
        //_navMapSystem.AddNavMapRegion(xform.GridUid.Value, navMap, GetNetEntity(uid), new HashSet<Vector2i>() { position });
    }

    private void OnStationMapOpened(EntityUid uid, StationMapComponent component, BoundUIOpenedEvent args)
    {
        if (args.Session.AttachedEntity == null)
            return;

        if (!_cell.TryUseActivatableCharge(uid))
            return;

        var comp = EnsureComp<StationMapUserComponent>(args.Session.AttachedEntity.Value);
        comp.Map = uid;
    }

    private void OnStationMapClosed(EntityUid uid, StationMapComponent component, BoundUIClosedEvent args)
    {
        if (!Equals(args.UiKey, StationMapUiKey.Key) || args.Session.AttachedEntity == null)
            return;

        RemCompDeferred<StationMapUserComponent>(args.Session.AttachedEntity.Value);
    }

    private void OnUserParentChanged(EntityUid uid, StationMapUserComponent component, ref EntParentChangedMessage args)
    {
        if (TryComp<ActorComponent>(uid, out var actor))
        {
            _ui.TryClose(component.Map, StationMapUiKey.Key, actor.PlayerSession);
        }
    }

    private void OnNavMapBeaconAnchor(ref AnchorStateChangedEvent ev)
    {
        if (!HasComp<NavMapBeaconComponent>(ev.Entity) ||
            !TryComp<NavMapComponent>(ev.Transform.GridUid, out var navMap) ||
            !TryComp<MapGridComponent>(ev.Transform.GridUid, out var mapGrid))
            return;

        if (navMap == null)
            return;

        if (ev.Anchored)
        {
            var position = _mapSystem.CoordinatesToTile(ev.Transform.GridUid.Value, mapGrid, _transformSystem.GetMapCoordinates(ev.Entity, ev.Transform));
            _navMapSystem.AddNavMapRegion(ev.Transform.GridUid.Value, navMap, GetNetEntity(ev.Entity), new HashSet<Vector2i>() { position });
        }

        else
        {
            _navMapSystem.RemoveNavMapRegion(ev.Transform.GridUid.Value, navMap, GetNetEntity(ev.Entity));
        }
    }
}
