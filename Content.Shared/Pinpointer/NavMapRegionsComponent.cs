using Content.Shared.Atmos;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Pinpointer;

[RegisterComponent, NetworkedComponent]
public sealed partial class NavMapRegionsComponent : Component
{
    /// <summary>
    /// This dictionary contains tile chunk bit masks which indicate how regions can propagate over them.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<Vector2i, NavMapRegionsChunk> RegionPropagationTiles = new();

    /// <summary>
    /// This dictionary contains a list of seeds from which regions are propagated.
    /// The dictionary keys are the region owners, and each owner can have multiple associated seeds.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<NetEntity, List<Vector2i>> RegionPropagationSeeds = new();

    /// <summary>
    /// This dictionary contains all flood filled regions. It is indexed by the region owner.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<NetEntity, List<Vector2i>> FloodedRegions = new();

    /// <summary>
    /// A queue of all regions that are waiting to be floodfilled.
    /// </summary>
    /// <remarks>
    /// The queued items consist of the region owner and their associated region seeds.
    /// </remarks>
    [ViewVariables(VVAccess.ReadOnly)]
    public Queue<(NetEntity, List<Vector2i>)> QueuedRegionsToFlood = new();
}

[Serializable, NetSerializable]
public sealed class NavMapRegionsComponentState : ComponentState
{
    public Dictionary<Vector2i, NavMapRegionsChunk> RegionPropagationTiles = new();
    public Dictionary<NetEntity, List<Vector2i>> RegionPropagationSeeds = new();
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
