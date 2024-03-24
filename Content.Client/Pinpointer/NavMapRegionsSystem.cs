using Content.Shared.Atmos;
using Content.Shared.Pinpointer;
using Robust.Shared.GameStates;
using System.Linq;

namespace Content.Client.Pinpointer;

public sealed class NavMapRegionsSystem : SharedNavMapRegionsSystem
{
    public const int RegionMaxSize = 625;

    private Dictionary<Vector2i, HashSet<NetEntity>> _chunkToRegionOwnerTable = new();
    private Dictionary<NetEntity, HashSet<Vector2i>> _regionOwnerToChunkTable = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NavMapRegionsComponent, ComponentHandleState>(OnHandleState);
        SubscribeNetworkEvent<NavMapRegionsOwnerRemovedEvent>(OnRegionOwnerRemoved);
        SubscribeNetworkEvent<NavMapRegionsOwnerChangedEvent>(OnRegionOwnerChanged);
        SubscribeNetworkEvent<NavMapRegionsChunkChangedEvent>(OnRegionChunkChanged);

    }

    private void OnHandleState(EntityUid uid, NavMapRegionsComponent component, ref ComponentHandleState args)
    {
        if (args.Current is not NavMapRegionsComponentState state)
            return;

        // Clear stale values
        component.RegionPropagationTiles.Clear();
        component.RegionOwners.Clear();
        component.QueuedRegionsToFlood.Clear();

        // Update what tiles regions can propagate over
        foreach (var (origin, chunk) in state.RegionPropagationTiles)
        {
            var newChunk = new NavMapRegionsChunk(origin);

            foreach (var (atmosDirection, value) in chunk.TileData)
                newChunk.TileData[atmosDirection] = value;

            component.RegionPropagationTiles.Add(origin, newChunk);
        }

        // Update the lists of region owners and their seeds and enqueue them for flood filling
        foreach (var (regionOwner, regionSeeds) in state.RegionOwners)
        {
            component.RegionOwners[regionOwner] = regionSeeds;
            component.QueuedRegionsToFlood.Enqueue(regionOwner);
        }
    }

    private void OnRegionOwnerChanged(NavMapRegionsOwnerChangedEvent ev)
    {
        var gridUid = GetEntity(ev.Grid);

        if (!TryComp<NavMapRegionsComponent>(gridUid, out var component))
            return;

        component.RegionOwners[ev.RegionOwner] = ev.RegionSeeds;
        component.QueuedRegionsToFlood.Enqueue(ev.RegionOwner);
    }

    private void OnRegionOwnerRemoved(NavMapRegionsOwnerRemovedEvent ev)
    {
        var gridUid = GetEntity(ev.Grid);

        if (!TryComp<NavMapRegionsComponent>(gridUid, out var component))
            return;

        component.RegionOwners.Remove(ev.RegionOwner);
    }

    private void OnRegionChunkChanged(NavMapRegionsChunkChangedEvent ev)
    {
        var gridUid = GetEntity(ev.Grid);

        if (!TryComp<NavMapRegionsComponent>(gridUid, out var component))
            return;

        var chunk = new NavMapRegionsChunk(ev.ChunkOrigin);
        chunk.TileData = ev.TileData;

        component.RegionPropagationTiles[ev.ChunkOrigin] = chunk;

        if (!_chunkToRegionOwnerTable.TryGetValue(ev.ChunkOrigin, out var affectedOwners))
            return;

        foreach (var affectedOwner in affectedOwners)
        {
            if (!component.RegionOwners.ContainsKey(affectedOwner))
                continue;

            component.QueuedRegionsToFlood.Enqueue(affectedOwner);
        }
    }

    public override void Update(float frameTime)
    {
        // To prevent compute spikes, only one region is flood filled per frame 
        var query = AllEntityQuery<NavMapRegionsComponent>();

        while (query.MoveNext(out var ent, out var entNavMapRegions))
            FloodFillNextEnqueuedRegion(ent, entNavMapRegions);
    }

    private void FloodFillNextEnqueuedRegion(EntityUid uid, NavMapRegionsComponent component)
    {
        if (!component.QueuedRegionsToFlood.Any())
            return;

        var regionOwner = component.QueuedRegionsToFlood.Dequeue();

        // If the region is no longer valid, flood the next one in the queue
        if (!component.RegionOwners.TryGetValue(regionOwner, out var regionSeeds) ||
            !regionSeeds.Any())
        {
            FloodFillNextEnqueuedRegion(uid, component);
            return;
        }

        // Get the tiles and chunks affected by the flood fill and assign the tiles to the component
        var (floodedTiles, floodedChunks) = FloodFillRegion(regionSeeds, component, RegionMaxSize);
        component.FloodedRegions[regionOwner] = floodedTiles;

        // To reduce unnecessary future flood fills, track which chunks have been flooded by a region owner 

        // First remove an old assignments
        if (_regionOwnerToChunkTable.TryGetValue(regionOwner, out var oldChunks))
        {
            foreach (var chunk in oldChunks)
            {
                if (_chunkToRegionOwnerTable.TryGetValue(chunk, out var oldOwners))
                {
                    oldOwners.Remove(regionOwner);
                    _chunkToRegionOwnerTable[chunk] = oldOwners;
                }
            }
        }

        // Now update with the new assignments
        _regionOwnerToChunkTable[regionOwner] = floodedChunks;

        foreach (var chunk in floodedChunks)
        {
            if (!_chunkToRegionOwnerTable.TryGetValue(chunk, out var owners))
                owners = new();

            owners.Add(regionOwner);

            _chunkToRegionOwnerTable[chunk] = owners;
        }
    }

    private (HashSet<Vector2i>, HashSet<Vector2i>) FloodFillRegion(HashSet<Vector2i> regionSeeds, NavMapRegionsComponent component, int regionMaxSize = 100)
    {
        if (!regionSeeds.Any())
            return (new(), new());

        HashSet<Vector2i> visitedChunks = new();
        HashSet<Vector2i> visitedTiles = new();
        Stack<Vector2i> tilesToVisit = new Stack<Vector2i>();

        foreach (var regionSeed in regionSeeds)
        {
            tilesToVisit.Push(regionSeed);

            while (tilesToVisit.Count > 0)
            {
                // If the max region size is hit, exit
                if (visitedTiles.Count > regionMaxSize)
                    return (new(), new());

                var current = tilesToVisit.Pop();

                var chunkOrigin = SharedMapSystem.GetChunkIndices(current, SharedNavMapSystem.ChunkSize);
                var relative = SharedMapSystem.GetChunkRelative(current, SharedNavMapSystem.ChunkSize);
                var flag = (ushort) SharedNavMapSystem.GetFlag(relative);

                if (visitedTiles.Contains(current))
                    continue;

                if (!component.RegionPropagationTiles.TryGetValue(chunkOrigin, out var chunk))
                    continue;

                if (!chunk.TileData.TryGetValue(AtmosDirection.All, out var all) || (all & flag) == 0)
                    continue;

                // Tile can be included in this region
                visitedTiles.Add(current);
                visitedChunks.Add(chunkOrigin);

                // Determine if we can propagate the region into its cardinally adjacent neighbors
                // To propagate to a neighbor, movement in towards that neighbor must not be blocked,
                // and movement from the neighbor to the current tile must not be blocked.

                // These considerations are generally only necessary for tiles containing thin entities.

                if (!chunk.TileData.TryGetValue(AtmosDirection.West, out var east) || (east & flag) == 0)
                {
                    var tile = new Vector2i(current.X - 1, current.Y);

                    if (CanMoveIntoTile(component.RegionPropagationTiles, tile, AtmosDirection.East))
                        tilesToVisit.Push(tile);
                }

                if (!chunk.TileData.TryGetValue(AtmosDirection.East, out var west) || (west & flag) == 0)
                {
                    var tile = new Vector2i(current.X + 1, current.Y);

                    if (CanMoveIntoTile(component.RegionPropagationTiles, tile, AtmosDirection.West))
                        tilesToVisit.Push(tile);
                }

                if (!chunk.TileData.TryGetValue(AtmosDirection.South, out var south) || (south & flag) == 0)
                {
                    var tile = new Vector2i(current.X, current.Y - 1);

                    if (CanMoveIntoTile(component.RegionPropagationTiles, tile, AtmosDirection.North))
                        tilesToVisit.Push(tile);
                }

                if (!chunk.TileData.TryGetValue(AtmosDirection.North, out var north) || (north & flag) == 0)
                {
                    var tile = new Vector2i(current.X, current.Y + 1);

                    if (CanMoveIntoTile(component.RegionPropagationTiles, tile, AtmosDirection.South))
                        tilesToVisit.Push(tile);
                }
            }
        }

        return (visitedTiles, visitedChunks);
    }

    private bool CanMoveIntoTile(Dictionary<Vector2i, NavMapRegionsChunk> chunks, Vector2i tile, AtmosDirection direction)
    {
        var chunkOrigin = SharedMapSystem.GetChunkIndices(tile, SharedNavMapSystem.ChunkSize);

        if (!chunks.TryGetValue(chunkOrigin, out var chunk))
            return false;

        var relative = SharedMapSystem.GetChunkRelative(tile, SharedNavMapSystem.ChunkSize);
        var flag = SharedNavMapSystem.GetFlag(relative);

        if (chunk.TileData.TryGetValue(direction, out var value) && (value & flag) > 0)
            return false;

        return true;
    }
}
