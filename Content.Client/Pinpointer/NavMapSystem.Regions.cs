using Content.Shared.Atmos;
using Content.Shared.Pinpointer;
using System.Linq;

namespace Content.Client.Pinpointer;

public sealed partial class NavMapSystem
{
    public const int RegionMaxSize = 625;

    private Dictionary<Vector2i, HashSet<NetEntity>> _chunkToRegionOwnerTable = new();
    private Dictionary<NetEntity, HashSet<Vector2i>> _regionOwnerToChunkTable = new();

    private (AtmosDirection, Vector2i, AtmosDirection)[] _regionPropagationTable =
    {
        (AtmosDirection.East, new Vector2i(1, 0), AtmosDirection.West),
        (AtmosDirection.West, new Vector2i(-1, 0), AtmosDirection.East),
        (AtmosDirection.North, new Vector2i(0, 1), AtmosDirection.South),
        (AtmosDirection.South, new Vector2i(0, -1), AtmosDirection.North),
    };

    #region: Event handling

    private void OnRegionPropertiesChanged(NavMapRegionPropertiesChangedEvent ev)
    {
        var gridUid = GetEntity(ev.Grid);

        if (!TryComp<NavMapComponent>(gridUid, out var component))
            return;

        component.RegionProperties[ev.RegionOwner] = ev.RegionProperties;

        if (ev.FloodRegion)
            component.QueuedRegionsToFlood.Enqueue(ev.RegionOwner);
    }

    private void OnRegionRemoved(NavMapRegionRemovedEvent ev)
    {
        var gridUid = GetEntity(ev.Grid);

        if (!TryComp<NavMapComponent>(gridUid, out var component))
            return;

        component.RegionProperties.Remove(ev.RegionOwner);
    }

    #endregion

    public override void Update(float frameTime)
    {
        // To prevent compute spikes, only one region is flood filled per frame 
        var query = AllEntityQuery<NavMapComponent>();

        while (query.MoveNext(out var ent, out var entNavMapRegions))
            FloodFillNextEnqueuedRegion(ent, entNavMapRegions);
    }

    private void FloodFillNextEnqueuedRegion(EntityUid uid, NavMapComponent component)
    {
        if (!component.QueuedRegionsToFlood.Any())
            return;

        var regionOwner = component.QueuedRegionsToFlood.Dequeue();

        // If the region is no longer valid, flood the next one in the queue
        if (!component.RegionProperties.TryGetValue(regionOwner, out var regionProperties) ||
            !regionProperties.Seeds.Any())
        {
            FloodFillNextEnqueuedRegion(uid, component);
            return;
        }

        // Get the tiles and chunks affected by the flood fill and assign the tiles to the component
        var (floodedTiles, floodedChunks) = FloodFillRegion(uid, component, regionProperties.Seeds, RegionMaxSize);
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

    private (HashSet<Vector2i>, HashSet<Vector2i>) FloodFillRegion(EntityUid uid, NavMapComponent component, HashSet<Vector2i> regionSeeds, int regionMaxSize = 100)
    {
        if (!regionSeeds.Any())
            return (new(), new());

        var visitedChunks = new HashSet<Vector2i>();
        var visitedTiles = new HashSet<Vector2i>();
        var tilesToVisit = new Stack<Vector2i>();

        foreach (var regionSeed in regionSeeds)
        {
            tilesToVisit.Push(regionSeed);

            while (tilesToVisit.Count > 0)
            {
                // If the max region size is hit, exit
                if (visitedTiles.Count > regionMaxSize)
                    return (new(), new());

                var current = tilesToVisit.Pop();

                var chunkOrigin = SharedMapSystem.GetChunkIndices(current, ChunkSize);
                var relative = SharedMapSystem.GetChunkRelative(current, ChunkSize);
                var flag = (ushort) GetFlag(relative);

                if (visitedTiles.Contains(current))
                    continue;

                if (!component.Chunks.TryGetValue((NavMapChunkType.Floor, chunkOrigin), out var floorChunk))
                    continue;

                var combinedFloorChunk = GetCombinedEdgesForChunk(floorChunk.TileData);

                if ((combinedFloorChunk & flag) == 0)
                    continue;

                var regionBlockingTileData = GetRegionBlockingTileData(uid, component, current);

                if (AllTileEdgesAreOccupied(regionBlockingTileData, relative))
                    continue;

                // Tile can be included in this region
                visitedTiles.Add(current);
                visitedChunks.Add(chunkOrigin);

                // Determine if we can propagate the region into its cardinally adjacent neighbors
                // To propagate to a neighbor, movement into the neighbors closest edge must not be 
                // blocked, and vice versa.

                foreach (var (direction, tileOffset, reverseDirection) in _regionPropagationTable)
                {
                    if (!regionBlockingTileData.TryGetValue(direction, out var directionFlag) || (directionFlag & flag) == 0)
                    {
                        var neighbor = current + tileOffset;
                        var neighborBlockingTileData = GetRegionBlockingTileData(uid, component, neighbor);

                        if (CanMoveIntoTile(neighborBlockingTileData, neighbor, reverseDirection))
                            tilesToVisit.Push(neighbor);
                    }
                }
            }
        }

        return (visitedTiles, visitedChunks);
    }

    private Dictionary<AtmosDirection, ushort> GetRegionBlockingTileData(EntityUid uid, NavMapComponent component, Vector2i tile)
    {
        var chunkOrigin = SharedMapSystem.GetChunkIndices(tile, ChunkSize);

        var regionBlockTileData = new Dictionary<AtmosDirection, ushort>()
        {
            [AtmosDirection.North] = 0,
            [AtmosDirection.East] = 0,
            [AtmosDirection.South] = 0,
            [AtmosDirection.West] = 0,
        };

        foreach (var regionBlockingChunkType in RegionBlockingChunkTypes)
        {
            if (component.Chunks.TryGetValue((regionBlockingChunkType, chunkOrigin), out var blockerChunk))
            {
                foreach (var (direction, blockerFlag) in blockerChunk.TileData)
                {
                    if (!regionBlockTileData.ContainsKey(direction))
                        continue;

                    regionBlockTileData[direction] |= blockerFlag;
                }
            }
        }

        return regionBlockTileData;
    }

    private bool CanMoveIntoTile(Dictionary<AtmosDirection, ushort> tileData, Vector2i tile, AtmosDirection direction)
    {
        var relative = SharedMapSystem.GetChunkRelative(tile, ChunkSize);
        var flag = GetFlag(relative);

        if (tileData.TryGetValue(direction, out var directionFlag) && (directionFlag & flag) == 0)
            return true;

        return false;
    }
}
