using Content.Server.Administration.Logs;
using Content.Server.Atmos.Components;
using Content.Server.Station.Systems;
using Content.Server.Warps;
using Content.Shared.Atmos;
using Content.Shared.Database;
using Content.Shared.Doors.Components;
using Content.Shared.Examine;
using Content.Shared.Maps;
using Content.Shared.Pinpointer;
using Content.Shared.Tag;
using Robust.Server.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;

namespace Content.Server.Pinpointer;

/// <summary>
/// Handles data to be used for in-grid map displays.
/// </summary>
public sealed partial class NavMapSystem : SharedNavMapSystem
{
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly MapSystem _map = default!;

    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<TagComponent> _tagQuery;

    public override void Initialize()
    {
        base.Initialize();

        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _tagQuery = GetEntityQuery<TagComponent>();

        SubscribeLocalEvent<AnchorStateChangedEvent>(OnAnchorStateChanged);

        SubscribeLocalEvent<StationGridAddedEvent>(OnStationInit);
        SubscribeLocalEvent<NavMapComponent, ComponentStartup>(OnNavMapStartup);
        SubscribeLocalEvent<NavMapComponent, ComponentGetState>(OnGetState);
        SubscribeLocalEvent<GridSplitEvent>(OnNavMapSplit);


        // Beacon events
        SubscribeLocalEvent<NavMapBeaconComponent, AnchorStateChangedEvent>(OnNavMapBeaconAnchor);
        SubscribeLocalEvent<ConfigurableNavMapBeaconComponent, NavMapBeaconConfigureBuiMessage>(OnConfigureMessage);
        SubscribeLocalEvent<ConfigurableNavMapBeaconComponent, MapInitEvent>(OnConfigurableMapInit);
        SubscribeLocalEvent<ConfigurableNavMapBeaconComponent, ExaminedEvent>(OnConfigurableExamined);

        // Region events
        SubscribeLocalEvent<GridStartupEvent>(OnGridStartUp);
        SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);
    }

    private void OnStationInit(StationGridAddedEvent ev)
    {
        var comp = EnsureComp<NavMapComponent>(ev.GridId);
        RefreshGrid(ev.GridId, comp, Comp<MapGridComponent>(ev.GridId));
    }

    private void OnNavMapBeaconAnchor(EntityUid uid, NavMapBeaconComponent component, ref AnchorStateChangedEvent args)
    {
        UpdateBeaconEnabledVisuals((uid, component));
        RefreshNavGrid(uid);
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
        Dirty(ent, navMap);
        RefreshNavGrid(ent);
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

    private void UpdateBeaconEnabledVisuals(Entity<NavMapBeaconComponent> ent)
    {
        _appearance.SetData(ent, NavMapBeaconVisuals.Enabled, ent.Comp.Enabled && Transform(ent).Anchored);
    }

    /// <summary>
    /// Refreshes the grid for the corresponding beacon.
    /// </summary>
    private void RefreshNavGrid(EntityUid uid)
    {
        var xform = Transform(uid);

        if (!TryComp<NavMapComponent>(xform.GridUid, out var navMap))
            return;

        Dirty(xform.GridUid.Value, navMap);
    }

    private bool CanBeacon(EntityUid uid, TransformComponent? xform = null)
    {
        if (!Resolve(uid, ref xform))
            return false;

        return xform.GridUid != null && xform.Anchored;
    }

    private void OnNavMapStartup(EntityUid uid, NavMapComponent component, ComponentStartup args)
    {
        if (!TryComp<MapGridComponent>(uid, out var grid))
            return;

        RefreshGrid(uid, component, grid);
    }

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

    private void RefreshGrid(EntityUid uid, NavMapComponent component, MapGridComponent grid)
    {

    }

    private Dictionary<(NavMapChunkType, Vector2i), Dictionary<AtmosDirection, ushort>> GetPackagedChunkData(Dictionary<(NavMapChunkType, Vector2i), NavMapChunk> chunks)
    {
        var data = new Dictionary<(NavMapChunkType, Vector2i), Dictionary<AtmosDirection, ushort>>(chunks.Count);

        foreach (var ((category, origin), chunk) in chunks)
        {
            var datum = new Dictionary<AtmosDirection, ushort>(chunk.TileData.Count);

            foreach (var (direction, tileData) in chunk.TileData)
                datum[direction] = tileData;

            data.Add((category, origin), datum);
        }

        return data;
    }

    private void OnGetState(EntityUid uid, NavMapComponent component, ref ComponentGetState args)
    {
        var chunkData = new Dictionary<(NavMapChunkType, Vector2i), Dictionary<AtmosDirection, ushort>>(component.Chunks.Count);

        foreach (var ((category, origin), chunk) in component.Chunks)
        {
            var chunkDatum = new Dictionary<AtmosDirection, ushort>(chunk.TileData.Count);

            foreach (var (direction, tileData) in chunk.TileData)
                chunkDatum[direction] = tileData;

            chunkData.Add((category, origin), chunkDatum);
        }

        var beaconQuery = AllEntityQuery<NavMapBeaconComponent, TransformComponent>();
        var beacons = new List<NavMapBeacon>();

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

            beacons.Add(new NavMapBeacon(beacon.Color, name, xform.LocalPosition));
        }

        var regionsData = new Dictionary<NetEntity, HashSet<Vector2i>>(component.RegionProperties.Count);

        foreach (var (netEntity, seeds) in component.RegionProperties)
            regionsData.Add(netEntity, seeds);

        // TODO: Diffs
        args.State = new NavMapComponentState()
        {
            ChunkData = chunkData,
            Beacons = beacons,
            RegionProperties = regionsData,
        };
    }

    private void RefreshTile(EntityUid uid, MapGridComponent grid, NavMapComponent component, NavMapChunk chunk, Vector2i tile)
    {

    }

    private ref Dictionary<Vector2i, NavMapChunk> GetChunkReference(EntityUid uid, NavMapComponent component, NavMapChunkType category)
    {
        switch (category)
        {
            case NavMapChunkType.Wall: return ref component.WallChunks;
            case NavMapChunkType.VisibleDoor: return ref component.AirlockChunks;
            case NavMapChunkType.NonVisibleDoor: return ref component.FirelockChunks;
            default: return ref component.FloorChunks;
        }
    }

    private void OnAnchorStateChanged(ref AnchorStateChangedEvent ev)
    {
        var gridUid = ev.Transform.GridUid;

        if (gridUid == null)
            return;

        if (!TryComp<NavMapComponent>(gridUid, out var navMap) ||
            !TryComp<MapGridComponent>(gridUid, out var mapGrid))
            return;

        // Airtight entities only - floor tiles are considered elsewhere
        if (!TryComp<AirtightComponent>(ev.Entity, out var airtight))
            return;

        var blockedDirection = airtight.AirBlockedDirection;
        var category = NavMapChunkType.Invalid;

        if (TryComp<NavMapDoorComponent>(ev.Entity, out var navMapDoor))
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

        var chunkData = GetChunkReference(gridUid.Value, navMap, category);

        var tile = CoordinatesToTile(ev.Transform.LocalPosition, mapGrid);
        var chunkOrigin = SharedMapSystem.GetChunkIndices(tile, ChunkSize);
        var relative = SharedMapSystem.GetChunkRelative(tile, ChunkSize);

        if (!chunkData.TryGetValue(chunkOrigin, out var chunk))
            chunk = new(chunkOrigin);

        var flag = (ushort) GetFlag(relative);
        var invFlag = (ushort) ~flag;

        foreach (var (direction, _) in chunk.TileData)
        {
            if (!ev.Anchored || (direction & blockedDirection) == 0)
                chunk.TileData[direction] &= invFlag;

            else if (ev.Anchored)
                chunk.TileData[direction] |= flag;
        }

        chunkData[chunkOrigin] = chunk;

        RaiseNetworkEvent(new NavMapChunkChangedEvent(GetNetEntity(gridUid.Value), category, chunkOrigin, chunk.TileData));
    }

    private void OnTileChanged(ref TileChangedEvent ev)
    {
        if (!TryComp<NavMapComponent>(ev.NewTile.GridUid, out var navMapRegions))
            return;

        var tile = ev.NewTile.GridIndices;
        var chunkOrigin = SharedMapSystem.GetChunkIndices(tile, ChunkSize);
        var relative = SharedMapSystem.GetChunkRelative(tile, ChunkSize);

        if (!navMapRegions.FloorChunks.TryGetValue(chunkOrigin, out var chunk))
            chunk = new(chunkOrigin);

        var flag = (ushort) GetFlag(relative);
        var invFlag = (ushort) ~flag;

        if (!ev.NewTile.IsSpace())
            chunk.TileData[AtmosDirection.All] |= flag;

        else
            chunk.TileData[AtmosDirection.All] &= invFlag;

        navMapRegions.FloorChunks[chunkOrigin] = chunk;

        RaiseNetworkEvent(new NavMapChunkChangedEvent(GetNetEntity(ev.NewTile.GridUid), NavMapChunkType.Floor, chunkOrigin, chunk.TileData));
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
        Dirty(uid, comp);

        RefreshNavGrid(uid);
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
}
