using Content.Shared.Pinpointer;
using Robust.Shared.GameStates;

namespace Content.Client.Pinpointer;

public sealed partial class NavMapSystem : SharedNavMapSystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NavMapComponent, ComponentHandleState>(OnHandleState);

        SubscribeNetworkEvent<NavMapRegionRemovedEvent>(OnRegionRemoved);
        SubscribeNetworkEvent<NavMapRegionPropertiesChangedEvent>(OnRegionPropertiesChanged);
        SubscribeNetworkEvent<NavMapChunkChangedEvent>(OnChunkChanged);
    }

    private void OnHandleState(EntityUid uid, NavMapComponent component, ref ComponentHandleState args)
    {
        if (args.Current is not NavMapComponentState state)
            return;

        component.Chunks.Clear();

        foreach (var ((category, origin), chunk) in state.ChunkData)
        {
            var newChunk = new NavMapChunk(origin);

            foreach (var (atmosDirection, value) in chunk)
                newChunk.TileData[atmosDirection] = value;

            component.Chunks[(category, origin)] = newChunk;
        }

        component.Beacons.Clear();
        component.Beacons.AddRange(state.Beacons);

        // Clear stale values
        component.RegionProperties.Clear();
        component.QueuedRegionsToFlood.Clear();

        // Update the lists of region owners and their seeds and enqueue them for flood filling
        foreach (var (regionOwner, regionSeeds) in state.RegionProperties)
        {
            component.RegionProperties[regionOwner] = regionSeeds;
            component.QueuedRegionsToFlood.Enqueue(regionOwner);
        }
    }

    private void OnChunkChanged(NavMapChunkChangedEvent ev)
    {
        var gridUid = GetEntity(ev.Grid);

        if (!TryComp<NavMapComponent>(gridUid, out var component))
            return;

        var newChunk = new NavMapChunk(ev.ChunkOrigin);

        foreach (var (atmosDirection, value) in ev.TileData)
            newChunk.TileData[atmosDirection] = value;

        component.Chunks[(ev.Category, ev.ChunkOrigin)] = newChunk;

        if (!_chunkToRegionOwnerTable.TryGetValue(ev.ChunkOrigin, out var affectedOwners))
            return;

        foreach (var affectedOwner in affectedOwners)
        {
            if (!component.RegionProperties.ContainsKey(affectedOwner))
                continue;

            if (!component.QueuedRegionsToFlood.Contains(affectedOwner))
                component.QueuedRegionsToFlood.Enqueue(affectedOwner);
        }
    }
}
