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
    public Dictionary<Vector2i, AtmosPipeChunk> AtmosPipeChunks = new();

    /// <summary>
    /// A list of all the atmos devices that will be used to populate the nav map
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public HashSet<AtmosDeviceNavMapData> AtmosDevices = new();

    /// <summary>
    /// The current entity of interest (selected on the console UI)
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public EntityUid? FocusDevice;

    /// <summary>
    /// A list of all the air alarms that have had their alerts silenced
    /// </summary>
    [ViewVariables]
    public HashSet<NetEntity> SilencedAlerts = new();
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
public struct AtmosDeviceNavMapData
{
    /// <summary>
    /// The entity in question
    /// </summary>
    public NetEntity NetEntity;

    /// <summary>
    /// Location of the entity
    /// </summary>
    public NetCoordinates NetCoordinates;

    /// <summary>
    /// Used to determine what map icons to use
    /// </summary>
    public AtmosMonitoringConsoleGroup Group;

    /// <summary>
    /// Pipe color (if applicable)
    /// </summary>
    public Color? Color = null;

    /// <summary>
    /// Populate the atmos monitoring console nav map with a single entity
    /// </summary>
    public AtmosDeviceNavMapData(NetEntity netEntity, NetCoordinates netCoordinates, AtmosMonitoringConsoleGroup group)
    {
        NetEntity = netEntity;
        NetCoordinates = netCoordinates;
        Group = group;
    }
}

[Serializable, NetSerializable]
public struct AtmosFocusDeviceData
{
    /// <summary>
    /// Focus entity
    /// </summary>
    public NetEntity NetEntity;

    /// <summary>
    /// Temperature (K) and related alert state
    /// </summary>
    public (float, AtmosAlarmType) TemperatureData;

    /// <summary>
    /// Pressure (kPA) and related alert state
    /// </summary>
    public (float, AtmosAlarmType) PressureData;

    /// <summary>
    /// Moles, percentage, and related alert state, for all detected gases 
    /// </summary>
    public Dictionary<Gas, (float, float, AtmosAlarmType)> GasData;

    /// <summary>
    /// Populates the atmos monitoring console focus entry with atmospheric data
    /// </summary>
    public AtmosFocusDeviceData
        (NetEntity netEntity,
        (float, AtmosAlarmType) temperatureData,
        (float, AtmosAlarmType) pressureData,
        Dictionary<Gas, (float, float, AtmosAlarmType)> gasData)
    {
        NetEntity = netEntity;
        TemperatureData = temperatureData;
        PressureData = pressureData;
        GasData = gasData;
    }
}

[Serializable, NetSerializable]
public sealed class AtmosMonitoringConsoleBoundInterfaceState : BoundUserInterfaceState
{
    /// <summary>
    /// A list of all air alarms
    /// </summary>
    public AtmosMonitoringConsoleEntry[] AirAlarms;

    /// <summary>
    /// Data for the UI focus (if applicable)
    /// </summary>
    public AtmosFocusDeviceData? FocusData;

    /// <summary>
    /// Sends data from the server to the client to populate the atmos monitoring console UI
    /// </summary>
    public AtmosMonitoringConsoleBoundInterfaceState(AtmosMonitoringConsoleEntry[] airAlarms, AtmosFocusDeviceData? focusData)
    {
        AirAlarms = airAlarms;
        FocusData = focusData;
    }
}

[Serializable, NetSerializable]
public struct AtmosMonitoringConsoleEntry
{
    /// <summary>
    /// The entity in question
    /// </summary>
    public NetEntity Entity;

    /// <summary>
    /// Location of the entity
    /// </summary>
    public NetCoordinates Coordinates;

    /// <summary>
    /// Current alarm state
    /// </summary>
    public AtmosAlarmType AlarmState;

    /// <summary>
    /// Device network address
    /// </summary>
    public string Address;

    /// <summary>
    /// Used to populate the atmos monitoring console UI with data from a single air alarm
    /// </summary>
    public AtmosMonitoringConsoleEntry
        (NetEntity entity,
        NetCoordinates coordinates,
        AtmosAlarmType alarmState,
        string address)
    {
        Entity = entity;
        Coordinates = coordinates;
        AlarmState = alarmState;
        Address = address;
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

    /// <summary>
    /// Contains four bitmasks for a single chunk of pipes, one for each cardinal direction 
    /// </summary>
    public AtmosPipeData()
    {

    }
}

[Serializable, NetSerializable]
public sealed class AtmosMonitoringConsoleMessage : BoundUserInterfaceMessage
{
    public NetEntity? FocusDevice;

    /// <summary>
    /// Used to inform the server that the focus for the specified atmos monitoring console has been changed by the client
    /// </summary>
    public AtmosMonitoringConsoleMessage(NetEntity? focusDevice)
    {
        FocusDevice = focusDevice;
    }
}

/// <summary>
/// List of all the different atmos device groups
/// </summary>
public enum AtmosMonitoringConsoleGroup
{
    Invalid,
    GasVentScrubber,
    GasVentPump,
    AirAlarm,
}

/// <summary>
/// UI key associated with the atmos monitoring console
/// </summary>
[Serializable, NetSerializable]
public enum AtmosMonitoringConsoleUiKey
{
    Key
}
