using Content.Server.Atmos.Components;
using Content.Shared.Atmos;
using Content.Shared.Maps;
using Content.Shared.Pinpointer;
using Robust.Shared.GameStates;
using Robust.Shared.Map.Components;
using System.Linq;
using System.Numerics;

namespace Content.Server.Pinpointer;

public sealed class NavMapRegionsSystem : SharedNavMapRegionsSystem
{
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;

    private List<AtmosDirection> _atmosDirections = new List<AtmosDirection>()
    {
        AtmosDirection.North, AtmosDirection.East, AtmosDirection.South, AtmosDirection.West
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NavMapRegionsComponent, ComponentGetState>(OnGetState);
        SubscribeLocalEvent<GridStartupEvent>(OnGridStartUp);
        SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);
        SubscribeLocalEvent<AnchorStateChangedEvent>(OnAnchorStateChanged);
    }

    private void OnGetState(EntityUid uid, NavMapRegionsComponent component, ref ComponentGetState args)
    {
        if (!HasComp<MapGridComponent>(uid))
            return;

        // Collect tile data for region propagation
        var tileData = new Dictionary<Vector2i, NavMapRegionsChunk>(component.RegionPropagationTiles.Count);

        foreach (var (index, chunk) in component.RegionPropagationTiles)
        {
            var newChunk = new NavMapRegionsChunk(index);

            foreach (var (atmosDirection, value) in chunk.TileData)
                newChunk.TileData[atmosDirection] = value;

            tileData.Add(index, newChunk);
        }

        // Collect seed data for region propagation
        var seedsData = new Dictionary<NetEntity, HashSet<Vector2i>>(component.RegionOwners.Count);

        foreach (var (netEntity, seeds) in component.RegionOwners)
        {
            seedsData.Add(netEntity, seeds);
        }

        // Set state
        args.State = new NavMapRegionsComponentState()
        {
            RegionPropagationTiles = tileData,
            RegionOwners = seedsData,
        };
    }

    private void OnGridStartUp(GridStartupEvent ev)
    {
        if (!TryComp<MapGridComponent>(ev.EntityUid, out var mapGrid))
            return;

        // Attach NavMapRegionsComponent to grid
        var navMapRegions = EnsureComp<NavMapRegionsComponent>(ev.EntityUid);

        // Record all non-spaced tiles on the grid
        var tileRefs = _mapSystem.GetAllTiles(ev.EntityUid, mapGrid);

        foreach (var tileRef in tileRefs)
        {
            var tile = tileRef.GridIndices;
            var chunkOrigin = SharedMapSystem.GetChunkIndices(tile, SharedNavMapSystem.ChunkSize);
            var relative = SharedMapSystem.GetChunkRelative(tile, SharedNavMapSystem.ChunkSize);

            if (!navMapRegions.RegionPropagationTiles.TryGetValue(chunkOrigin, out var chunk))
                chunk = new(chunkOrigin);

            var flag = (ushort) SharedNavMapSystem.GetFlag(relative);
            chunk.TileData[AtmosDirection.All] |= flag;

            navMapRegions.RegionPropagationTiles[chunkOrigin] = chunk;
        }

        Dirty(ev.EntityUid, navMapRegions);
    }

    private void OnTileChanged(ref TileChangedEvent ev)
    {
        if (!TryComp<NavMapRegionsComponent>(ev.NewTile.GridUid, out var navMapRegions))
            return;

        var tile = ev.NewTile.GridIndices;
        var chunkOrigin = SharedMapSystem.GetChunkIndices(tile, SharedNavMapSystem.ChunkSize);
        var relative = SharedMapSystem.GetChunkRelative(tile, SharedNavMapSystem.ChunkSize);

        if (!navMapRegions.RegionPropagationTiles.TryGetValue(chunkOrigin, out var chunk))
            chunk = new(chunkOrigin);

        var flag = (ushort) SharedNavMapSystem.GetFlag(relative);
        var invFlag = (ushort) ~flag;

        // If the tile is not open space, regions can propagate over it
        if (!ev.NewTile.IsSpace())
            chunk.TileData[AtmosDirection.All] |= flag;

        else
            chunk.TileData[AtmosDirection.All] &= invFlag;

        navMapRegions.RegionPropagationTiles[chunkOrigin] = chunk;

        RaiseNetworkEvent(new NavMapRegionsChunkChangedEvent(GetNetEntity(ev.NewTile.GridUid), chunkOrigin, chunk.TileData));
    }

    private void OnAnchorStateChanged(ref AnchorStateChangedEvent args)
    {
        // Only consider airtight entities
        if (!TryComp<AirtightComponent>(args.Entity, out var airtight))
            return;

        var xform = Transform(args.Entity);

        if (xform.GridUid == null)
            return;

        if (!TryComp<MapGridComponent>(xform.GridUid.Value, out var mapGrid))
            return;

        var navMapRegions = EnsureComp<NavMapRegionsComponent>(xform.GridUid.Value);

        var tile = CoordinatesToTile(xform.LocalPosition, mapGrid);
        var chunkOrigin = SharedMapSystem.GetChunkIndices(tile, SharedNavMapSystem.ChunkSize);
        var relative = SharedMapSystem.GetChunkRelative(tile, SharedNavMapSystem.ChunkSize);
        var tileRef = _mapSystem.GetTileRef(xform.GridUid.Value, mapGrid, xform.Coordinates);

        if (!navMapRegions.RegionPropagationTiles.TryGetValue(chunkOrigin, out var chunk))
            chunk = new(chunkOrigin);

        var flag = (ushort) SharedNavMapSystem.GetFlag(relative);
        var invFlag = (ushort) ~flag;

        chunk.TileData[AtmosDirection.All] &= invFlag;
        chunk.TileData[airtight.AirBlockedDirection] &= invFlag;

        if (tileRef.IsSpace() || args.Anchored)
        {
            // If the entity was removed, regions can propagate over the vacated tile
            if (!args.Anchored)
                chunk.TileData[AtmosDirection.All] |= flag;

            // If the entity was attached, but it doesn't block all directions,
            // regions can propagate over the tile in all but the affected direction
            else if (args.Anchored && airtight.AirBlockedDirection != AtmosDirection.All)
            {
                chunk.TileData[AtmosDirection.All] |= flag;
                chunk.TileData[airtight.AirBlockedDirection] |= flag;
            }
        }

        navMapRegions.RegionPropagationTiles[chunkOrigin] = chunk;

        RaiseNetworkEvent(new NavMapRegionsChunkChangedEvent(GetNetEntity(xform.GridUid.Value), chunkOrigin, chunk.TileData));
    }

    private NavMapRegionsChunk AddPropagationTilesToChunk(NavMapRegionsChunk chunk, AtmosDirection blockedDirection, ushort flag)
    {
        foreach (var direction in _atmosDirections)
        {
            if ((direction & blockedDirection) == 0)
                continue;

            chunk.TileData[direction] |= flag;
        }

        return chunk;
    }

    private NavMapRegionsChunk RemovePropagationTilesFromChunk(NavMapRegionsChunk chunk, AtmosDirection blockedDirection, ushort flag)
    {
        var invFlag = (ushort) ~flag;

        foreach (var direction in _atmosDirections)
        {
            if ((direction & blockedDirection) == 0)
                continue;

            chunk.TileData[direction] &= invFlag;
        }

        return chunk;
    }

    private Vector2i CoordinatesToTile(Vector2 position, MapGridComponent grid)
    {
        var x = (int) Math.Floor(position.X / grid.TileSize);
        var y = (int) Math.Floor(position.Y / grid.TileSize);

        return new Vector2i(x, y);
    }
}
