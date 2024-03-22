using Content.Shared.Atmos;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Pinpointer;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedNavMapRegionsSystem))]
public sealed partial class NavMapRegionsComponent : Component
{
    /// <summary>
    /// This dictionary contains tile chunk bit masks which indicate how regions can propagate over them.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<Vector2i, NavMapRegionsChunk> RegionPropagationTiles = new();

    /// <summary>
    /// This dictionary contains a list of seeds from which regions are propagated.
    /// It is indexed by the region owners. Each owner can be assigned multiple seeds.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<NetEntity, HashSet<Vector2i>> RegionOwners = new();

    /// <summary>
    /// This dictionary contains all flood filled regions. It is indexed by the region owners.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<NetEntity, HashSet<Vector2i>> FloodedRegions = new();

    /// <summary>
    /// A queue of all region owners that are waiting their regions to be floodfilled.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public Queue<NetEntity> QueuedRegionsToFlood = new();
}

[Serializable, NetSerializable]
public sealed class NavMapRegionsComponentState : ComponentState
{
    public Dictionary<Vector2i, NavMapRegionsChunk> RegionPropagationTiles = new();
    public Dictionary<NetEntity, HashSet<Vector2i>> RegionOwners = new();
}

[Serializable, NetSerializable]
public sealed class NavMapRegionsChunk
{
    public readonly Vector2i Origin;
    public Dictionary<AtmosDirection, int> TileData = new();

    public NavMapRegionsChunk(Vector2i origin)
    {
        Origin = origin;
        TileData[AtmosDirection.All] = 0;
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
public sealed class NavMapRegionsChunkChangedEvent : EntityEventArgs
{
    public NetEntity Grid;
    public Vector2i ChunkOrigin;
    public Dictionary<AtmosDirection, int> TileData;

    public NavMapRegionsChunkChangedEvent(NetEntity grid, Vector2i chunkOrigin, Dictionary<AtmosDirection, int> tileData)
    {
        Grid = grid;
        ChunkOrigin = chunkOrigin;
        TileData = tileData;
    }
};
