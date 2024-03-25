using System.Linq;
using System.Numerics;
using Content.Shared.Atmos;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Pinpointer;

public abstract class SharedNavMapSystem : EntitySystem
{
    public const byte ChunkSize = 4;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NavMapBeaconComponent, MapInitEvent>(OnNavMapBeaconMapInit);
    }

    /// <summary>
    /// Converts the chunk's tile into a bitflag for the slot.
    /// </summary>
    public static int GetFlag(Vector2i relativeTile)
    {
        return 1 << (relativeTile.X * ChunkSize + relativeTile.Y);
    }

    /// <summary>
    /// Converts the chunk's tile into a bitflag for the slot.
    /// </summary>
    public static Vector2i GetTile(int flag)
    {
        var value = Math.Log2(flag);
        var x = (int) value / ChunkSize;
        var y = (int) value % ChunkSize;
        var result = new Vector2i(x, y);

        DebugTools.Assert(GetFlag(result) == flag);

        return new Vector2i(x, y);
    }

    private void OnNavMapBeaconMapInit(EntityUid uid, NavMapBeaconComponent component, MapInitEvent args)
    {
        component.Text ??= string.Empty;
        component.Text = Loc.GetString(component.Text);
        Dirty(uid, component);
    }

    public bool RegionOwnerIsValid(EntityUid uid, NavMapComponent component, NetEntity regionOwner)
    {
        return component.RegionProperties.ContainsKey(regionOwner);
    }

    public void AddRegionOwner(EntityUid uid, NavMapComponent component, NetEntity regionOwner, HashSet<Vector2i> regionSeeds)
    {
        var ev = new NavMapRegionsOwnerChangedEvent(GetNetEntity(uid), regionOwner, regionSeeds);

        if (!component.RegionProperties.TryGetValue(regionOwner, out var oldSeeds))
            RaiseNetworkEvent(ev);

        else if (!oldSeeds.SequenceEqual(regionSeeds))
            RaiseNetworkEvent(ev);

        component.RegionProperties[regionOwner] = regionSeeds;
    }

    [Serializable, NetSerializable]
    protected sealed class NavMapComponentState : ComponentState
    {
        public Dictionary<(NavMapChunkType, Vector2i), Dictionary<AtmosDirection, ushort>> ChunkData = new();
        public List<NavMapBeacon> Beacons = new();
        public Dictionary<NetEntity, HashSet<Vector2i>> RegionProperties = new();
    }

    [Serializable, NetSerializable]
    public readonly record struct NavMapBeacon(Color Color, string Text, Vector2 Position);

    [Serializable, NetSerializable]
    public readonly record struct NavMapAirlock(Vector2 Position, bool Visible = true);
}
