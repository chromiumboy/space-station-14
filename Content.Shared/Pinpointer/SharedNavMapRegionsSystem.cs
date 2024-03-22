using System.Linq;

namespace Content.Shared.Pinpointer;

public abstract class SharedNavMapRegionsSystem : EntitySystem
{
    public bool RegionOwnerIsValid(EntityUid uid, NavMapRegionsComponent component, NetEntity regionOwner)
    {
        return component.RegionOwners.ContainsKey(regionOwner);
    }

    public void AddRegionOwner(EntityUid uid, NavMapRegionsComponent component, NetEntity regionOwner, HashSet<Vector2i> regionSeeds)
    {
        var ev = new NavMapRegionsOwnerChangedEvent(GetNetEntity(uid), regionOwner, regionSeeds);

        if (!component.RegionOwners.TryGetValue(regionOwner, out var oldSeeds))
            RaiseNetworkEvent(ev);

        else if (!oldSeeds.SequenceEqual(regionSeeds))
            RaiseNetworkEvent(ev);

        component.RegionOwners[regionOwner] = regionSeeds;
    }
}
