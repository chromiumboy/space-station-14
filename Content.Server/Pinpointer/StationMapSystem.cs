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
        SubscribeLocalEvent<NavMapRegionMarkerComponent, MapInitEvent>(OnNavMapBeaconInit);
        SubscribeLocalEvent<NavMapRegionMarkerComponent, AnchorStateChangedEvent>(OnNavMapBeaconAnchor);

        Subs.BuiEvents<StationMapComponent>(StationMapUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnStationMapOpened);
            subs.Event<BoundUIClosedEvent>(OnStationMapClosed);
        });
    }

    #region: Event handling

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

    private void OnNavMapBeaconInit(EntityUid uid, NavMapRegionMarkerComponent component, ref MapInitEvent ev)
    {
        var xform = Transform(uid);

        if (!TryComp<NavMapBeaconComponent>(uid, out var beacon) ||
            !TryComp<NavMapComponent>(xform.GridUid, out var navMap))
            return;

        var regionProperties = GetNavMapRegionProperties(uid, beacon);

        if (regionProperties != null)
            _navMapSystem.AddOrUpdateNavMapRegion(xform.GridUid.Value, navMap, GetNetEntity(uid), regionProperties);
    }

    private void OnNavMapBeaconAnchor(EntityUid uid, NavMapRegionMarkerComponent component, ref AnchorStateChangedEvent ev)
    {
        var xform = Transform(uid);

        if (!TryComp<NavMapBeaconComponent>(uid, out var beacon) ||
            !TryComp<NavMapComponent>(xform.GridUid, out var navMap))
            return;

        if (ev.Anchored)
        {
            var regionProperties = GetNavMapRegionProperties(uid, beacon);

            if (regionProperties != null)
                _navMapSystem.AddOrUpdateNavMapRegion(xform.GridUid.Value, navMap, GetNetEntity(uid), regionProperties);
        }

        else
        {
            _navMapSystem.RemoveNavMapRegion(xform.GridUid.Value, navMap, GetNetEntity(uid));
        }
    }

    #endregion

    private NavMapRegionProperties? GetNavMapRegionProperties(EntityUid uid, NavMapBeaconComponent component)
    {
        var xform = Transform(uid);

        if (!TryComp<MapGridComponent>(xform.GridUid, out var mapGrid))
            return null;

        NavMapChunkType[] propagatingChunkTypes = { NavMapChunkType.Floor };
        NavMapChunkType[] constraintChunkTypes = { NavMapChunkType.Wall, NavMapChunkType.VisibleDoor };
        var seeds = new HashSet<Vector2i>() { _mapSystem.CoordinatesToTile(xform.GridUid.Value, mapGrid, _transformSystem.GetMapCoordinates(uid, xform)) };
        var regionProperties = new NavMapRegionProperties(GetNetEntity(uid), propagatingChunkTypes, constraintChunkTypes, seeds, component.Color);

        return regionProperties;
    }
}
