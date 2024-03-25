using Content.Shared.Atmos;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Pinpointer;

/// <summary>
/// Used to store grid data to be used for UIs.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NavMapComponent : Component
{
    /*
     * Don't need DataFields as this can be reconstructed
     */

    /// <summary>
    /// Bitmasks that represent chunked tiles.
    /// </summary>
    [ViewVariables]
    public Dictionary<(NavMapChunkType, Vector2i), NavMapChunk> Chunks = new();

    /// <summary>
    /// List of station beacons.
    /// </summary>
    [ViewVariables]
    public List<SharedNavMapSystem.NavMapBeacon> Beacons = new();

    /// <summary>
    /// Describes the properties of a region on the station.
    /// It is indexed by the entity assigned as the region owner.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<NetEntity, HashSet<Vector2i>> RegionProperties = new();

    /// <summary>
    /// All flood filled regions.
    /// It is indexed by the entity assigned as the region owner.
    /// </summary>
    /// <remarks>
    /// For client use only
    /// </remarks>
    [ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<NetEntity, HashSet<Vector2i>> FloodedRegions = new();

    /// <summary>
    /// A queue of all region owners that are waiting their associated regions to be floodfilled.
    /// </summary>
    /// <remarks>
    /// For client use only
    /// </remarks>
    [ViewVariables(VVAccess.ReadOnly)]
    public Queue<NetEntity> QueuedRegionsToFlood = new();
}

[Serializable, NetSerializable]
public sealed class NavMapChunk
{
    public readonly Vector2i Origin;

    /// <summary>
    /// Bitmask for tiles, 1 for occupied and 0 for empty.
    /// There is a bitmask for each direction, in case the
    /// entity does not fill the whole tile
    /// </summary>
    public Dictionary<AtmosDirection, ushort> TileData;

    public NavMapChunk(Vector2i origin)
    {
        Origin = origin;

        TileData = new()
        {
            [AtmosDirection.North] = 0,
            [AtmosDirection.East] = 0,
            [AtmosDirection.South] = 0,
            [AtmosDirection.West] = 0,
        };
    }
}

[Serializable, NetSerializable]
public sealed class NavMapRegionsOwnerRemovedEvent : EntityEventArgs
{
    public NetEntity Grid;
    public NetEntity RegionOwner;

    public NavMapRegionsOwnerRemovedEvent(NetEntity grid, NetEntity regionOwner)
    {
        Grid = grid;
        RegionOwner = regionOwner;
    }
};

[Serializable, NetSerializable]
public sealed class NavMapRegionsOwnerChangedEvent : EntityEventArgs
{
    public NetEntity Grid;
    public NetEntity RegionOwner;
    public HashSet<Vector2i> RegionSeeds;

    public NavMapRegionsOwnerChangedEvent(NetEntity grid, NetEntity regionOwner, HashSet<Vector2i> regionSeeds)
    {
        Grid = grid;
        RegionOwner = regionOwner;
        RegionSeeds = regionSeeds;
    }
};

[Serializable, NetSerializable]
public sealed class NavMapChunkChangedEvent : EntityEventArgs
{
    public NetEntity Grid;
    public NavMapChunkType Category;
    public Vector2i ChunkOrigin;
    public Dictionary<AtmosDirection, ushort> TileData;

    public NavMapChunkChangedEvent(NetEntity grid, NavMapChunkType category, Vector2i chunkOrigin, Dictionary<AtmosDirection, ushort> tileData)
    {
        Grid = grid;
        Category = category;
        ChunkOrigin = chunkOrigin;
        TileData = tileData;
    }
};

public enum NavMapChunkType
{
    Invalid,
    Floor,
    Wall,
    VisibleDoor,
    NonVisibleDoor,
}
