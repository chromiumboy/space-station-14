using System.Linq;
using System.Numerics;
using Content.Shared.Atmos;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Pinpointer;

public abstract class SharedNavMapSystem : EntitySystem
{
    public const byte ChunkSize = 4;

    public readonly NavMapChunkType[] RegionBlockingChunkTypes =
    {
        NavMapChunkType.Wall,
        NavMapChunkType.VisibleDoor,
        NavMapChunkType.NonVisibleDoor,
    };

    public readonly NavMapChunkType[] DoorChunkTypes =
    {
        NavMapChunkType.VisibleDoor,
        NavMapChunkType.NonVisibleDoor,
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NavMapBeaconComponent, MapInitEvent>(OnNavMapBeaconMapInit);
    }

    #region: Event handling
    private void OnNavMapBeaconMapInit(EntityUid uid, NavMapBeaconComponent component, MapInitEvent args)
    {
        component.Text ??= string.Empty;
        component.Text = Loc.GetString(component.Text);

        Dirty(uid, component);
    }

    #endregion

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

    public NavMapChunk SetAllEdgesForChunkTile(NavMapChunk chunk, Vector2i tile)
    {
        var relative = SharedMapSystem.GetChunkRelative(tile, ChunkSize);
        var flag = (ushort) GetFlag(relative);

        foreach (var (direction, _) in chunk.TileData)
            chunk.TileData[direction] |= flag;

        return chunk;
    }

    public NavMapChunk UnsetAllEdgesForChunkTile(NavMapChunk chunk, Vector2i tile)
    {
        var relative = SharedMapSystem.GetChunkRelative(tile, ChunkSize);
        var flag = (ushort) GetFlag(relative);
        var invFlag = (ushort) ~flag;

        foreach (var (direction, _) in chunk.TileData)
            chunk.TileData[direction] &= invFlag;

        return chunk;
    }

    public ushort GetCombinedEdgesForChunk(Dictionary<AtmosDirection, ushort> tile)
    {
        ushort combined = 0;

        foreach (var (_, value) in tile)
            combined |= value;

        return combined;
    }

    public bool AllTileEdgesAreOccupied(Dictionary<AtmosDirection, ushort> tileData, Vector2i tile)
    {
        var flag = (ushort) GetFlag(tile);

        foreach (var (direction, _) in tileData)
        {
            if ((tileData[direction] & flag) == 0)
                return false;
        }

        return true;
    }

    public bool AddNavMapRegion(EntityUid uid, NavMapComponent component, NetEntity regionOwner, HashSet<Vector2i> regionSeeds)
    {
        if (!component.RegionProperties.TryGetValue(regionOwner, out var oldSeeds) ||
            !oldSeeds.SequenceEqual(regionSeeds))
        {
            component.RegionProperties[regionOwner] = regionSeeds;
            RaiseNetworkEvent(new NavMapRegionsOwnerChangedEvent(GetNetEntity(uid), regionOwner, regionSeeds));

            return true;
        }

        return false;
    }

    public bool RemoveNavMapRegion(EntityUid uid, NavMapComponent component, NetEntity regionOwner)
    {
        if (component.RegionProperties.ContainsKey(regionOwner))
        {
            component.RegionProperties.Remove(regionOwner);
            RaiseNetworkEvent(new NavMapRegionsOwnerRemovedEvent(GetNetEntity(uid), regionOwner));

            return true;
        }

        return false;
    }

    #region: System messages

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

    #endregion
}
