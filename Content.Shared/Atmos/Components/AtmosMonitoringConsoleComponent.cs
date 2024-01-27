//using Content.Server.Atmos.Monitor.Systems;
using Content.Shared.Atmos.Monitor;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Serialization;


namespace Content.Shared.Atmos.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
//[Access(typeof(AtmosMonitoringConsoleSystem))]
public sealed partial class AtmosMonitoringConsoleComponent : Component
{
    /// <summary>
    /// A dictionary of the all the nav map chunks that contain anchored atmos pipes
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public Dictionary<Vector2i, AtmosPipeChunk> AllChunks = new();

    [ViewVariables, AutoNetworkedField]
    public List<AtmosMonitorData> AtmosMonitors = new();
}

[Serializable, NetSerializable]
public struct AtmosPipeChunk
{
    /// <summary>
    /// Chunk position
    /// </summary>
    public readonly Vector2i Origin;

    /// <summary>
    /// Bitmask dictionary for atmos pipes, 1 for occupied and 0 for empty.
    /// </summary>
    public Dictionary<string, AtmosPipeData> AtmosPipeData = new();

    public AtmosPipeChunk(Vector2i origin)
    {
        Origin = origin;
    }
}

[Serializable, NetSerializable]
public struct AtmosMonitorData
{
    public NetEntity NetEntity;
    public NetCoordinates NetCoordinates;
    public AtmosMonitoringConsoleGroup Group;
    public Color? Color = null;

    public AtmosMonitorData(NetEntity netEntity, NetCoordinates netCoordinates, AtmosMonitoringConsoleGroup group)
    {
        NetEntity = netEntity;
        NetCoordinates = netCoordinates;
        Group = group;
    }
}

[Serializable, NetSerializable]
public struct AtmosPipeData
{
    /// <summary>
    /// Tiles with a north facing pipe on a specific chunk
    /// </summary>
    public ushort NorthFacing = 0;

    /// <summary>
    /// Tiles with a south facing pipe on a specific chunk
    /// </summary>
    public ushort SouthFacing = 0;

    /// <summary>
    /// Tiles with an east facing pipe on a specific chunk
    /// </summary>
    public ushort EastFacing = 0;

    /// <summary>
    /// Tiles with a west facing pipe on a specific chunk
    /// </summary>
    public ushort WestFacing = 0;

    public AtmosPipeData()
    {

    }
}

[Serializable, NetSerializable]
public struct AtmosMonitoringConsoleEntry
{
    public NetEntity NetEntity;

    public AtmosMonitoringConsoleEntry(NetEntity netEntity)
    {
        NetEntity = netEntity;
    }
}

public enum AtmosMonitoringConsoleGroup
{
    GasVentScrubber,
    GasVentPump,
    AirSensor,
    AirAlarm,
}

/// <summary>
///     UI key associated with the power monitoring console
/// </summary>
[Serializable, NetSerializable]
public enum AtmosMonitoringConsoleUiKey
{
    Key
}
