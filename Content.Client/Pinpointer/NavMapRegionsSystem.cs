using Content.Shared.Atmos;
using Content.Shared.Pinpointer;
using Robust.Shared.GameStates;
using System.Linq;

namespace Content.Client.Pinpointer;

public sealed class NavMapRegionsSystem : EntitySystem
{
    public const int RegionMaxSize = 625;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NavMapRegionsComponent, ComponentHandleState>(OnHandleState);
    }

    private void OnHandleState(EntityUid uid, NavMapRegionsComponent component, ref ComponentHandleState args)
    {
        if (args.Current is not NavMapRegionsComponentState state)
            return;

        // Clear stale values
        component.RegionPropagationTiles.Clear();
        component.RegionPropagationSeeds.Clear();
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
        foreach (var (regionOwner, regionSeeds) in state.RegionPropagationSeeds)
        {
            component.RegionPropagationSeeds.Add(regionOwner, regionSeeds);
            component.QueuedRegionsToFlood.Enqueue((regionOwner, regionSeeds));
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

        (var netEntity, var seeds) = component.QueuedRegionsToFlood.Dequeue();

        if (!TryComp<NavMapRegionsComponent>(uid, out var navMapRegions))
            return;

        navMapRegions.FloodedRegions[netEntity] = FloodFillRegion(seeds, navMapRegions, RegionMaxSize);
    }

    private List<Vector2i> FloodFillRegion(List<Vector2i> regionSeeds, NavMapRegionsComponent component, int regionMaxSize = 100)
    {
        if (!regionSeeds.Any())
            return new();

        List<Vector2i> visited = new();
        Stack<Vector2i> toVisit = new Stack<Vector2i>();

        foreach (var regionSeed in regionSeeds)
        {
            toVisit.Push(regionSeed);

            while (toVisit.Count > 0)
            {
                // If the max region size is hit, exit
                if (visited.Count > regionMaxSize)
                    return new();

                var current = toVisit.Pop();

                var chunkOrigin = SharedMapSystem.GetChunkIndices(current, SharedNavMapSystem.ChunkSize);
                var relative = SharedMapSystem.GetChunkRelative(current, SharedNavMapSystem.ChunkSize);
                var flag = SharedNavMapSystem.GetFlag(relative);

                if (visited.Contains(current))
                    continue;

                if (!component.RegionPropagationTiles.TryGetValue(chunkOrigin, out var chunk))
                    continue;

                if (!chunk.TileData.TryGetValue(AtmosDirection.All, out var all) || (all & flag) == 0)
                    continue;

                // Tile can be included in this region
                visited.Add(current);

                // Determine if we can propagate the region into its cardinally adjacent neighbors
                // To propagate to a neighbor, movement in towards that neighbor must not be blocked,
                // and movement from the neighbor to the current tile must not be blocked.

                // These considerations are generally only necessary for tiles containing thin entities.

                if (!chunk.TileData.TryGetValue(AtmosDirection.West, out var east) || (east & flag) == 0)
                {
                    var tile = new Vector2i(current.X - 1, current.Y);

                    if (CanMoveIntoTile(component.RegionPropagationTiles, tile, AtmosDirection.East))
                        toVisit.Push(tile);
                }

                if (!chunk.TileData.TryGetValue(AtmosDirection.East, out var west) || (west & flag) == 0)
                {
                    var tile = new Vector2i(current.X + 1, current.Y);

                    if (CanMoveIntoTile(component.RegionPropagationTiles, tile, AtmosDirection.West))
                        toVisit.Push(tile);
                }

                if (!chunk.TileData.TryGetValue(AtmosDirection.South, out var south) || (south & flag) == 0)
                {
                    var tile = new Vector2i(current.X, current.Y - 1);

                    if (CanMoveIntoTile(component.RegionPropagationTiles, tile, AtmosDirection.North))
                        toVisit.Push(tile);
                }

                if (!chunk.TileData.TryGetValue(AtmosDirection.North, out var north) || (north & flag) == 0)
                {
                    var tile = new Vector2i(current.X, current.Y + 1);

                    if (CanMoveIntoTile(component.RegionPropagationTiles, tile, AtmosDirection.South))
                        toVisit.Push(tile);
                }
            }
        }

        return visited;
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
