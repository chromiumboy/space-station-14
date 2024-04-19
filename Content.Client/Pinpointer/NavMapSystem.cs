using Content.Shared.Pinpointer;
using Robust.Shared.GameStates;
using System.Collections.Generic;
using System.Linq;

namespace Content.Client.Pinpointer;

public sealed partial class NavMapSystem : SharedNavMapSystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NavMapComponent, ComponentHandleState>(OnHandleState);
    }

    private void OnHandleState(EntityUid uid, NavMapComponent component, ref ComponentHandleState args)
    {
        if (args.Current is not NavMapComponentState state)
            return;

        if (!state.FullState)
        {
            foreach (var index in component.Chunks.Keys)
            {
                if (!state.AllChunks!.Contains(index))
                    component.Chunks.Remove(index);
            }

            foreach (var beacon in component.Beacons)
            {
                if (!state.AllBeacons!.Contains(beacon))
                    component.Beacons.Remove(beacon);
            }

            foreach (var region in component.RegionProperties)
            {
                if (!state.AllRegions!.Any(x => x.Owner == region.Value.Owner))
                    component.RegionProperties.Remove(region.Key);
            }
        }

        else
        {
            foreach (var index in component.Chunks.Keys)
            {
                if (!state.Chunks.ContainsKey(index))
                    component.Chunks.Remove(index);
            }

            foreach (var beacon in component.Beacons)
            {
                if (!state.Beacons.Contains(beacon))
                    component.Beacons.Remove(beacon);
            }

            foreach (var region in component.RegionProperties)
            {
                if (!state.Regions.Any(x => x.Owner == region.Value.Owner))
                    component.RegionProperties.Remove(region.Key);
            }
        }

        foreach (var ((category, origin), chunk) in state.Chunks)
        {
            var newChunk = new NavMapChunk(origin);

            foreach (var (atmosDirection, value) in chunk)
                newChunk.TileData[atmosDirection] = value;

            component.Chunks[(category, origin)] = newChunk;

            // If the affected chunk intersects one or more regions, re-flood them
            if (!_chunkToRegionOwnerTable.TryGetValue(origin, out var affectedOwners))
                continue;

            foreach (var affectedOwner in affectedOwners)
            {
                if (!component.RegionProperties.ContainsKey(affectedOwner))
                    continue;

                if (!component.QueuedRegionsToFlood.Contains(affectedOwner))
                    component.QueuedRegionsToFlood.Enqueue(affectedOwner);
            }
        }

        foreach (var beacon in state.Beacons)
            component.Beacons.Add(beacon);

        foreach (var region in state.Regions)
        {
            component.RegionProperties[region.Owner] = region;

            if (!component.QueuedRegionsToFlood.Contains(region.Owner))
                component.QueuedRegionsToFlood.Enqueue(region.Owner);
        }
    }
}
