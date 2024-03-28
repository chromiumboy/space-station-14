using Content.Server.Administration.Logs;
using Content.Server.Atmos.Components;
using Content.Server.Station.Systems;
using Content.Server.Warps;
using Content.Shared.Atmos;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Maps;
using Content.Shared.Pinpointer;
using Robust.Shared.GameStates;
using Robust.Shared.Map.Components;

namespace Content.Server.Pinpointer;

/// <summary>
/// Handles data to be used for in-grid map displays.
/// </summary>
public sealed partial class NavMapSystem : SharedNavMapSystem
{
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Initialization events
        SubscribeLocalEvent<StationGridAddedEvent>(OnStationInit);

        // Grid change events
        SubscribeLocalEvent<GridSplitEvent>(OnNavMapSplit);
        SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);
        SubscribeLocalEvent<AnchorStateChangedEvent>(OnAnchorStateChanged);

        // Beacon events
        SubscribeLocalEvent<NavMapBeaconComponent, AnchorStateChangedEvent>(OnNavMapBeaconAnchor);
        SubscribeLocalEvent<ConfigurableNavMapBeaconComponent, NavMapBeaconConfigureBuiMessage>(OnConfigureMessage);
        SubscribeLocalEvent<ConfigurableNavMapBeaconComponent, MapInitEvent>(OnConfigurableMapInit);
        SubscribeLocalEvent<ConfigurableNavMapBeaconComponent, ExaminedEvent>(OnConfigurableExamined);

        // Data handling events
        SubscribeLocalEvent<NavMapComponent, ComponentGetState>(OnGetState);
    }

    #region: Initialization event handling
    private void OnStationInit(StationGridAddedEvent ev)
    {
        var comp = EnsureComp<NavMapComponent>(ev.GridId);
        RefreshGrid(ev.GridId, comp, Comp<MapGridComponent>(ev.GridId));
    }

    #endregion

    #region: Grid change event handling

    private void OnNavMapSplit(ref GridSplitEvent args)
    {
        if (!TryComp(args.Grid, out NavMapComponent? comp))
            return;

        var gridQuery = GetEntityQuery<MapGridComponent>();

        foreach (var grid in args.NewGrids)
        {
            var newComp = EnsureComp<NavMapComponent>(grid);
            RefreshGrid(grid, newComp, gridQuery.GetComponent(grid));
        }

        RefreshGrid(args.Grid, comp, gridQuery.GetComponent(args.Grid));
    }

    private void OnTileChanged(ref TileChangedEvent ev)
    {
        if (!TryComp<NavMapComponent>(ev.NewTile.GridUid, out var navMapRegions))
            return;

        var tile = ev.NewTile.GridIndices;
        var chunkOrigin = SharedMapSystem.GetChunkIndices(tile, ChunkSize);

        if (!navMapRegions.Chunks.TryGetValue((NavMapChunkType.Floor, chunkOrigin), out var chunk))
            chunk = new(chunkOrigin);

        // This could be easily replaced in the future to accommodate diagonal tiles
        if (ev.NewTile.IsSpace())
            chunk = UnsetAllEdgesForChunkTile(chunk, tile);

        else
            chunk = SetAllEdgesForChunkTile(chunk, tile);

        // Update the component on the server side
        navMapRegions.Chunks[(NavMapChunkType.Floor, chunkOrigin)] = chunk;

        // Update the component on the client side
        RaiseNetworkEvent(new NavMapChunkChangedEvent(GetNetEntity(ev.NewTile.GridUid), NavMapChunkType.Floor, chunkOrigin, chunk.TileData));
    }

    private void OnAnchorStateChanged(ref AnchorStateChangedEvent ev)
    {
        var gridUid = ev.Transform.GridUid;

        if (gridUid == null)
            return;

        if (!TryComp<NavMapComponent>(gridUid, out var navMap) ||
            !TryComp<MapGridComponent>(gridUid, out var mapGrid))
            return;

        // Update nav map regions
        if (!ev.Anchored)
            RemoveNavMapRegion(gridUid.Value, navMap, GetNetEntity(ev.Entity));

        // We are only concerned with airtight entities (walls, doors, etc) from this point
        if (!HasComp<AirtightComponent>(ev.Entity))
            return;

        // Refresh the affected tile
        var tile = _mapSystem.CoordinatesToTile(gridUid.Value, mapGrid, _transformSystem.GetMapCoordinates(ev.Entity, ev.Transform));
        var chunkOrigin = SharedMapSystem.GetChunkIndices(tile, ChunkSize);

        RefreshTile(gridUid.Value, navMap, mapGrid, chunkOrigin, tile);

        // Update potentially affected chunks (i.e., walls and doors)
        foreach (NavMapChunkType category in RegionBlockingChunkTypes)
        {
            if (!navMap.Chunks.TryGetValue((category, chunkOrigin), out var chunk))
                continue;

            // Update the component on the server side
            navMap.Chunks[(category, chunkOrigin)] = chunk;

            // Update the component on the client side
            RaiseNetworkEvent(new NavMapChunkChangedEvent(GetNetEntity(gridUid.Value), category, chunkOrigin, chunk.TileData));
        }
    }

    #endregion

    #region: Beacon event handling
    private void OnNavMapBeaconAnchor(EntityUid uid, NavMapBeaconComponent component, ref AnchorStateChangedEvent args)
    {
        UpdateBeaconEnabledVisuals((uid, component));
    }

    private void OnConfigureMessage(Entity<ConfigurableNavMapBeaconComponent> ent, ref NavMapBeaconConfigureBuiMessage args)
    {
        if (args.Session.AttachedEntity is not { } user)
            return;

        if (!TryComp<NavMapBeaconComponent>(ent, out var navMap))
            return;

        if (navMap.Text == args.Text &&
            navMap.Color == args.Color &&
            navMap.Enabled == args.Enabled)
            return;

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(user):player} configured NavMapBeacon \'{ToPrettyString(ent):entity}\' with text \'{args.Text}\', color {args.Color.ToHexNoAlpha()}, and {(args.Enabled ? "enabled" : "disabled")} it.");

        if (TryComp<WarpPointComponent>(ent, out var warpPoint))
        {
            warpPoint.Location = args.Text;
        }

        navMap.Text = args.Text;
        navMap.Color = args.Color;
        navMap.Enabled = args.Enabled;
        UpdateBeaconEnabledVisuals((ent, navMap));
    }

    private void OnConfigurableMapInit(Entity<ConfigurableNavMapBeaconComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<NavMapBeaconComponent>(ent, out var navMap))
            return;

        // We set this on mapinit just in case the text was edited via VV or something.
        if (TryComp<WarpPointComponent>(ent, out var warpPoint))
        {
            warpPoint.Location = navMap.Text;
        }

        UpdateBeaconEnabledVisuals((ent, navMap));
    }

    private void OnConfigurableExamined(Entity<ConfigurableNavMapBeaconComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || !TryComp<NavMapBeaconComponent>(ent, out var navMap))
            return;

        args.PushMarkup(Loc.GetString("nav-beacon-examine-text",
            ("enabled", navMap.Enabled),
            ("color", navMap.Color.ToHexNoAlpha()),
            ("label", navMap.Text ?? string.Empty)));
    }

    #endregion

    #region: State event handling

    private void OnGetState(EntityUid uid, NavMapComponent component, ref ComponentGetState args)
    {
        // Get the chunk data
        var chunkData = new Dictionary<(NavMapChunkType, Vector2i), Dictionary<AtmosDirection, ushort>>(component.Chunks.Count);

        foreach (var ((category, origin), chunk) in component.Chunks)
        {
            var chunkDatum = new Dictionary<AtmosDirection, ushort>(chunk.TileData.Count);

            foreach (var (direction, tileData) in chunk.TileData)
                chunkDatum[direction] = tileData;

            chunkData.Add((category, origin), chunkDatum);
        }

        // Get the station beacons
        var beacons = new List<NavMapBeacon>();
        var beaconQuery = AllEntityQuery<NavMapBeaconComponent, TransformComponent>();

        while (beaconQuery.MoveNext(out var beaconUid, out var beacon, out var xform))
        {
            if (!beacon.Enabled || xform.GridUid != uid || !CanBeacon(beaconUid, xform))
                continue;

            // TODO: Make warp points use metadata name instead.
            string? name = beacon.Text;

            if (string.IsNullOrEmpty(name))
            {
                if (TryComp<WarpPointComponent>(beaconUid, out var warpPoint) && warpPoint.Location != null)
                    name = warpPoint.Location;

                else
                    name = MetaData(beaconUid).EntityName;
            }

            beacons.Add(new NavMapBeacon(GetNetEntity(beaconUid), beacon.Color, name, xform.LocalPosition));
        }

        // Get the region properties
        var regionsData = new Dictionary<NetEntity, HashSet<Vector2i>>(component.RegionProperties.Count);

        foreach (var (netEntity, seeds) in component.RegionProperties)
            regionsData.Add(netEntity, seeds);

        // Set the state
        args.State = new NavMapComponentState()
        {
            ChunkData = chunkData,
            Beacons = beacons,
            RegionProperties = regionsData,
        };
    }

    #endregion

    #region: Grid functions

    private void RefreshGrid(EntityUid uid, NavMapComponent component, MapGridComponent mapGrid)
    {
        var tileRefs = _mapSystem.GetAllTiles(uid, mapGrid);

        foreach (var tileRef in tileRefs)
        {
            var tile = tileRef.GridIndices;
            var chunkOrigin = SharedMapSystem.GetChunkIndices(tile, ChunkSize);

            if (!component.Chunks.TryGetValue((NavMapChunkType.Floor, chunkOrigin), out var chunk))
                chunk = new(chunkOrigin);

            component.Chunks[(NavMapChunkType.Floor, chunkOrigin)] = SetAllEdgesForChunkTile(chunk, tile);

            // Refresh the contents of the tile
            RefreshTile(uid, component, mapGrid, chunkOrigin, tile);
        }

        Dirty(uid, component);
    }

    private void RefreshTile(EntityUid uid, NavMapComponent component, MapGridComponent mapGrid, Vector2i chunkOrigin, Vector2i tile)
    {
        var relative = SharedMapSystem.GetChunkRelative(tile, ChunkSize);
        var flag = (ushort) GetFlag(relative);
        var invFlag = (ushort) ~flag;

        // Clear stale data from the tile across all associated chunks
        foreach (var category in RegionBlockingChunkTypes)
        {
            if (!component.Chunks.TryGetValue((category, chunkOrigin), out var chunk))
                chunk = new(chunkOrigin);

            foreach (var (direction, _) in chunk.TileData)
                chunk.TileData[direction] &= invFlag;

            component.Chunks[(category, chunkOrigin)] = chunk;
        }

        // Update the tile data based on what entities are still anchored to the tile
        var enumerator = _mapSystem.GetAnchoredEntitiesEnumerator(uid, mapGrid, tile);

        while (enumerator.MoveNext(out var ent))
        {
            if (!TryComp<AirtightComponent>(ent, out var entAirtight))
                continue;

            var category = GetAssociatedChunkType(ent.Value);

            if (!component.Chunks.TryGetValue((category, chunkOrigin), out var chunk))
                continue;

            foreach (var (direction, _) in chunk.TileData)
            {
                if ((direction & entAirtight.AirBlockedDirection) > 0)
                    chunk.TileData[direction] |= flag;
            }

            component.Chunks[(category, chunkOrigin)] = chunk;
        }

        // Remove walls that intersect with doors (unless they can both physically fit on the same tile)
        if (component.Chunks.TryGetValue((NavMapChunkType.Wall, chunkOrigin), out var wallChunk))
        {
            foreach (var door in DoorChunkTypes)
            {
                if (!component.Chunks.TryGetValue((door, chunkOrigin), out var doorChunk))
                    continue;

                foreach (var (direction, _) in wallChunk.TileData)
                {
                    var doorInvFlag = (ushort) ~doorChunk.TileData[direction];
                    wallChunk.TileData[direction] &= doorInvFlag;
                }
            }

            component.Chunks[(NavMapChunkType.Wall, chunkOrigin)] = wallChunk;
        }
    }

    private NavMapChunkType GetAssociatedChunkType(EntityUid uid)
    {
        var category = NavMapChunkType.Invalid;

        if (TryComp<NavMapDoorComponent>(uid, out var navMapDoor))
        {
            switch (navMapDoor.Visible)
            {
                case true: category = NavMapChunkType.VisibleDoor; break;
                case false: category = NavMapChunkType.NonVisibleDoor; break;
            }
        }

        else
        {
            category = NavMapChunkType.Wall;
        }

        return category;
    }

    #endregion

    #region: Beacon functions

    private void UpdateBeaconEnabledVisuals(Entity<NavMapBeaconComponent> ent)
    {
        _appearance.SetData(ent, NavMapBeaconVisuals.Enabled, ent.Comp.Enabled && Transform(ent).Anchored);
    }

    private bool CanBeacon(EntityUid uid, TransformComponent? xform = null)
    {
        if (!Resolve(uid, ref xform))
            return false;

        return xform.GridUid != null && xform.Anchored;
    }

    /// <summary>
    /// Sets the beacon's Enabled field and refreshes the grid.
    /// </summary>
    public void SetBeaconEnabled(EntityUid uid, bool enabled, NavMapBeaconComponent? comp = null)
    {
        if (!Resolve(uid, ref comp) || comp.Enabled == enabled)
            return;

        comp.Enabled = enabled;
        UpdateBeaconEnabledVisuals((uid, comp));
    }

    /// <summary>
    /// Toggles the beacon's Enabled field and refreshes the grid.
    /// </summary>
    public void ToggleBeacon(EntityUid uid, NavMapBeaconComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return;

        SetBeaconEnabled(uid, !comp.Enabled, comp);
    }

    #endregion
}
