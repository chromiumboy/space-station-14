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
