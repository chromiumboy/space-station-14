using Content.Server.Atmos.Components;
using Content.Shared.Atmos;
using Content.Shared.Maps;
using Content.Shared.Pinpointer;
using Robust.Shared.GameStates;
using Robust.Shared.Map.Components;
using System.Numerics;

namespace Content.Server.Pinpointer;

public sealed partial class NavMapSystem
{
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;

    private List<AtmosDirection> _atmosDirections = new List<AtmosDirection>()
    {
        AtmosDirection.North, AtmosDirection.East, AtmosDirection.South, AtmosDirection.West
    };

    private void OnGridStartUp(GridStartupEvent ev)
    {
        if (!TryComp<MapGridComponent>(ev.EntityUid, out var mapGrid))
            return;

        // Attach NavMapRegionsComponent to grid
        var navMapRegions = EnsureComp<NavMapComponent>(ev.EntityUid);

        // Record all non-spaced tiles on the grid
        var tileRefs = _mapSystem.GetAllTiles(ev.EntityUid, mapGrid);

        foreach (var tileRef in tileRefs)
        {
            var tile = tileRef.GridIndices;
            var chunkOrigin = SharedMapSystem.GetChunkIndices(tile, ChunkSize);
            var relative = SharedMapSystem.GetChunkRelative(tile, ChunkSize);

            if (!navMapRegions.RegionPropagationTiles.TryGetValue(chunkOrigin, out var chunk))
                chunk = new(chunkOrigin);

            var flag = (ushort) GetFlag(relative);
            chunk.TileData[AtmosDirection.All] |= flag;

            navMapRegions.RegionPropagationTiles[chunkOrigin] = chunk;
        }

        Dirty(ev.EntityUid, navMapRegions);
    }

    

    private NavMapChunk AddPropagationTilesToChunk(NavMapChunk chunk, AtmosDirection blockedDirection, ushort flag)
    {
        foreach (var direction in _atmosDirections)
        {
            if ((direction & blockedDirection) == 0)
                continue;

            chunk.TileData[direction] |= flag;
        }

        return chunk;
    }

    private NavMapChunk RemovePropagationTilesFromChunk(NavMapChunk chunk, AtmosDirection blockedDirection, ushort flag)
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
