using Content.Shared.Atmos;
using Content.Shared.Pinpointer;
using System.Linq;

namespace Content.Client.Pinpointer;

public sealed partial class NavMapSystem
{
    public const int RegionMaxSize = 625; // i.e, max area of 25 * 25 tiles

    private Dictionary<Vector2i, HashSet<NetEntity>> _chunkToRegionOwnerTable = new();
    private Dictionary<NetEntity, HashSet<Vector2i>> _regionOwnerToChunkTable = new();

    private (AtmosDirection, Vector2i, AtmosDirection)[] _regionPropagationTable =
    {
        (AtmosDirection.East, new Vector2i(1, 0), AtmosDirection.West),
        (AtmosDirection.West, new Vector2i(-1, 0), AtmosDirection.East),
        (AtmosDirection.North, new Vector2i(0, 1), AtmosDirection.South),
        (AtmosDirection.South, new Vector2i(0, -1), AtmosDirection.North),
    };

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
        var (floodedTiles, floodedChunks) = FloodFillRegion(uid, component, regionProperties, RegionMaxSize);
        component.FloodedRegions[regionOwner] = (floodedTiles, regionProperties.Color);

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

    private (HashSet<Vector2i>, HashSet<Vector2i>) FloodFillRegion(EntityUid uid, NavMapComponent component, NavMapRegionProperties regionProperties, int regionMaxSize = 100)
    {
        if (!regionProperties.Seeds.Any())
            return (new(), new());

        var visitedChunks = new HashSet<Vector2i>();
        var visitedTiles = new HashSet<Vector2i>();
        var tilesToVisit = new Stack<Vector2i>();

        foreach (var regionSeed in regionProperties.Seeds)
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

                ushort combinedPropagatingChunk = 0;

                foreach (var chunkType in regionProperties.PropagatingTypes)
                {
                    if (component.Chunks.TryGetValue((chunkType, chunkOrigin), out var propagatingChunk))
                        combinedPropagatingChunk |= GetCombinedEdgesForChunk(propagatingChunk.TileData);
                }

                if ((combinedPropagatingChunk & flag) == 0)
                    continue;

                var regionBlockingTileData = GetRegionBlockingTileData(uid, component, current, regionProperties);

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
                    var neighbor = current + tileOffset;
                    var neighborOrigin = SharedMapSystem.GetChunkIndices(neighbor, ChunkSize);

                    visitedChunks.Add(neighborOrigin);

                    if (!regionBlockingTileData.TryGetValue(direction, out var directionFlag) || (directionFlag & flag) == 0)
                    {
                        var neighborBlockingTileData = GetRegionBlockingTileData(uid, component, neighbor, regionProperties);

                        if (CanMoveIntoTile(neighborBlockingTileData, neighbor, reverseDirection))
                            tilesToVisit.Push(neighbor);
                    }
                }
            }
        }

        return (visitedTiles, visitedChunks);
    }

    private Dictionary<AtmosDirection, ushort> GetRegionBlockingTileData(EntityUid uid, NavMapComponent component, Vector2i tile, NavMapRegionProperties regionProperties)
    {
        var chunkOrigin = SharedMapSystem.GetChunkIndices(tile, ChunkSize);

        var regionBlockTileData = new Dictionary<AtmosDirection, ushort>()
        {
            [AtmosDirection.North] = 0,
            [AtmosDirection.East] = 0,
            [AtmosDirection.South] = 0,
            [AtmosDirection.West] = 0,
        };

        foreach (var regionBlockingChunkType in regionProperties.ConstraintTypes)
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
