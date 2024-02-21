using Content.Server.GameTicking.Rules.Components;
using Content.Server.NodeContainer;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.NodeGroups;
using Content.Server.NodeContainer.Nodes;
using Content.Server.Power.Components;
using Content.Server.Power.Nodes;
using Content.Server.Power.NodeGroups;
using Content.Server.Station.Components;
using Content.Server.StationEvents.Components;
using Content.Shared.Atmos.Components;
using Content.Shared.Pinpointer;
using Content.Shared.Power;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using System.Linq;
using System.Diagnostics.CodeAnalysis;
using Content.Server.Atmos.Monitor.Components;
using Content.Server.Atmos.Piping.Components;
using Content.Shared.Atmos;
using Content.Server.Atmos.Piping.Unary.Components;

using System;
using System.Linq;
using System.Collections.Generic;
using Content.Shared.Atmos.Monitor;
using Content.Server.DeviceNetwork.Components;
using Content.Shared.Atmos.Monitor.Components;

namespace Content.Server.Atmos.Monitor.Systems;

public sealed class AtmosMonitoringConsoleSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _userInterfaceSystem = default!;
    [Dependency] private readonly SharedMapSystem _sharedMapSystem = default!;
    [Dependency] private readonly AirAlarmSystem _airAlarmSystem = default!;
    [Dependency] private readonly AtmosDeviceNetworkSystem _atmosDevNet = default!;

    // Note: this data does not need to be saved
    private Dictionary<EntityUid, Dictionary<Vector2i, AtmosPipeChunk>> _gridAtmosPipeChunks = new();
    private float _updateTimer = 1.0f;

    private const float UpdateTime = 1.0f;

    public override void Initialize()
    {
        base.Initialize();

        // Console events
        SubscribeLocalEvent<AtmosMonitoringConsoleComponent, ComponentInit>(OnConsoleInit);
        SubscribeLocalEvent<AtmosMonitoringConsoleComponent, EntParentChangedMessage>(OnConsoleParentChanged);

        SubscribeLocalEvent<AtmosMonitoringConsoleComponent, AtmosMonitoringConsoleMessage>(OnAtmosMonitoringConsoleMessage);

        /*
        // UI events
        SubscribeLocalEvent<PowerMonitoringConsoleComponent, PowerMonitoringConsoleMessage>(OnPowerMonitoringConsoleMessage);
        SubscribeLocalEvent<PowerMonitoringConsoleComponent, BoundUIOpenedEvent>(OnBoundUIOpened);

        // Grid events
        SubscribeLocalEvent<GridSplitEvent>(OnGridSplit);
        SubscribeLocalEvent<CableComponent, CableAnchorStateChangedEvent>(OnCableAnchorStateChanged);
        SubscribeLocalEvent<PowerMonitoringDeviceComponent, AnchorStateChangedEvent>(OnDeviceAnchoringChanged);
        SubscribeLocalEvent<PowerMonitoringDeviceComponent, NodeGroupsRebuilt>(OnNodeGroupRebuilt);

        // Game rule events
        SubscribeLocalEvent<GameRuleStartedEvent>(OnPowerGridCheckStarted);
        SubscribeLocalEvent<GameRuleEndedEvent>(OnPowerGridCheckEnded);*/
    }

    private void OnConsoleInit(EntityUid uid, AtmosMonitoringConsoleComponent component, ComponentInit args)
    {
        RefreshAtmosMonitoringConsole(uid, component);
    }

    private void OnConsoleParentChanged(EntityUid uid, AtmosMonitoringConsoleComponent component, EntParentChangedMessage args)
    {
        RefreshAtmosMonitoringConsole(uid, component);
    }

    private void OnAtmosMonitoringConsoleMessage(EntityUid uid, AtmosMonitoringConsoleComponent component, AtmosMonitoringConsoleMessage args)
    {
        var focusDevice = EntityManager.GetEntity(args.FocusDevice);

        // Update this if the focus device has changed
        if (component.FocusDevice != focusDevice)
        {
            component.FocusDevice = focusDevice;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _updateTimer += frameTime;

        if (_updateTimer >= UpdateTime)
        {
            _updateTimer -= UpdateTime;

            var query = AllEntityQuery<AtmosMonitoringConsoleComponent>();
            while (query.MoveNext(out var ent, out var console))
            {
                if (!_userInterfaceSystem.TryGetUi(ent, AtmosMonitoringConsoleUiKey.Key, out var bui))
                    continue;

                foreach (var session in bui.SubscribedSessions)
                    UpdateUIState(ent, console, session);
            }
        }
    }

    public void UpdateUIState(EntityUid uid, AtmosMonitoringConsoleComponent component, ICommonSession session)
    {
        if (!_userInterfaceSystem.TryGetUi(uid, AtmosMonitoringConsoleUiKey.Key, out var bui))
            return;

        var consoleXform = Transform(uid);

        if (consoleXform?.GridUid == null)
            return;

        var gridUid = consoleXform.GridUid.Value;

        if (!TryComp<MapGridComponent>(gridUid, out var mapGrid))
            return;

        // The grid must have a NavMapComponent to visualize the map in the UI
        EnsureComp<NavMapComponent>(gridUid);

        // Initializing data to be send to the client
        var allEntries = new List<AtmosMonitoringConsoleEntry>();

        // Loop over all tracked devices
        var powerMonitoringDeviceQuery = AllEntityQuery<AirAlarmComponent, TransformComponent>();
        while (powerMonitoringDeviceQuery.MoveNext(out var ent, out var device, out var xform))
        {
            if (xform.Anchored == false || xform.GridUid != gridUid)
                continue;

            // Generate a new console entry with which to populate the UI
            var entry = new AtmosMonitoringConsoleEntry(EntityManager.GetNetEntity(ent));
            allEntries.Add(entry);
        }

        var airAlarms = GetAirAlarms(gridUid);
        var focusData = GetFocusAlarmData(uid, component, gridUid);

        // Set the UI state
        _userInterfaceSystem.SetUiState(bui,
            new AtmosMonitoringConsoleBoundInterfaceState(airAlarms.ToArray(), focusData),
            session);
    }

    private List<AtmosAlarmEntry> GetAirAlarms(EntityUid gridUid)
    {
        var activeAlarms = new List<AtmosAlarmEntry>();

        var queryAirAlarms = AllEntityQuery<AirAlarmComponent, DeviceNetworkComponent, ApcPowerReceiverComponent, TransformComponent>();
        while (queryAirAlarms.MoveNext(out var ent, out var entAirAlarm, out var entDeviceNetwork, out var entAPCPower, out var entXform))
        {
            if (entXform.GridUid != gridUid)
                continue;

            if (!entXform.Anchored)
                continue;

            if (!entAPCPower.Powered)
                continue;

            var alarmState = entAirAlarm.State;

            if (alarmState == AtmosAlarmType.Emagged)
                alarmState = AtmosAlarmType.Danger;

            var entry = new AtmosAlarmEntry(GetNetEntity(ent), GetNetCoordinates(entXform.Coordinates), alarmState, entDeviceNetwork.Address);
            activeAlarms.Add(entry);
        }

        return activeAlarms;
    }

    private AtmosMonitorFocusDeviceData? GetFocusAlarmData(EntityUid uid, AtmosMonitoringConsoleComponent component, EntityUid gridUid)
    {
        if (component.FocusDevice == null)
            return null;

        var ent = component.FocusDevice.Value;
        var entXform = Transform(component.FocusDevice.Value);

        if (!entXform.Anchored ||
            entXform.GridUid != gridUid ||
            !TryComp<AirAlarmComponent>(ent, out var entAirAlarm))
        {
            component.FocusDevice = null;
            Dirty(uid, component);

            return null;
        }

        if (!_userInterfaceSystem.TryGetUi(ent, SharedAirAlarmInterfaceKey.Key, out var bui) ||
            bui.SubscribedSessions.Count == 0)
        {
            foreach ((var address, var _) in entAirAlarm.SensorData)
            {
                _atmosDevNet.Sync(component.FocusDevice.Value, address);
            }
        }

        var temperatureData = (_airAlarmSystem.CalculateTemperatureAverage(entAirAlarm), AtmosAlarmType.Normal);
        var pressureData = (_airAlarmSystem.CalculatePressureAverage(entAirAlarm), AtmosAlarmType.Normal);
        var gasData = new Dictionary<Gas, (float, float, AtmosAlarmType)>();

        foreach ((var address, var sensorData) in entAirAlarm.SensorData)
        {
            if (sensorData.TemperatureThreshold.CheckThreshold(sensorData.Temperature, out var temperatureState) &&
                (int) temperatureState > (int) temperatureData.Item2)
            {
                temperatureData = (temperatureData.Item1, temperatureState);
            }

            if (sensorData.PressureThreshold.CheckThreshold(sensorData.Pressure, out var pressureState) &&
                (int) pressureState > (int) pressureData.Item2)
            {
                pressureData = (pressureData.Item1, pressureState);
            }

            if (entAirAlarm.SensorData.Sum(g => g.Value.TotalMoles) > 1e-8)
            {
                foreach ((var gas, var threshold) in sensorData.GasThresholds)
                {
                    if (!gasData.ContainsKey(gas))
                    {
                        float mol = _airAlarmSystem.CalculateGasMolarConcentrationAverage(entAirAlarm, gas, out var percentage);

                        if (mol < 1e-8)
                            continue;

                        gasData[gas] = (mol, percentage, AtmosAlarmType.Normal);
                    }

                    if (threshold.CheckThreshold(gasData[gas].Item2, out var gasState) &&
                        (int) gasState > (int) gasData[gas].Item3)
                    {
                        gasData[gas] = (gasData[gas].Item1, gasData[gas].Item2, gasState);
                    }
                }
            }
        }

        return new AtmosMonitorFocusDeviceData(GetNetEntity(component.FocusDevice.Value), temperatureData, pressureData, gasData);
    }

    private List<AtmosMonitorData> GetAtmosMonitorData(EntityUid gridUid)
    {
        var data = new List<AtmosMonitorData>();
        var temperatures = new List<float>();
        var pressures = new List<float>();

        var queryAtmosMonitors = AllEntityQuery<AtmosMonitorComponent, TransformComponent>();
        while (queryAtmosMonitors.MoveNext(out var ent, out var _, out var entXform))
        {
            if (entXform.GridUid != gridUid)
                continue;

            if (!entXform.Anchored)
                continue;

            var group = AtmosMonitoringConsoleGroup.AirSensor;

            if (HasComp<GasVentPumpComponent>(ent))
                group = AtmosMonitoringConsoleGroup.GasVentPump;

            else if (HasComp<GasVentScrubberComponent>(ent))
                group = AtmosMonitoringConsoleGroup.GasVentScrubber;

            else
                continue;

            var datum = new AtmosMonitorData(GetNetEntity(ent), GetNetCoordinates(entXform.Coordinates), group);

            if (TryComp<AtmosPipeColorComponent>(ent, out var atmosPipeColor))
                datum.Color = atmosPipeColor.Color;

            data.Add(datum);
        }

        var queryAirAlarms = AllEntityQuery<AirAlarmComponent, ApcPowerReceiverComponent, TransformComponent>();
        while (queryAirAlarms.MoveNext(out var ent, out var entAirAlarm, out var entAPCPower, out var entXform))
        {
            if (entXform.GridUid != gridUid)
                continue;

            if (!entXform.Anchored)
                continue;

            if (!entAPCPower.Powered)
                continue;

            //if (entAirAlarm.State <= AtmosAlarmType.Normal)
            //    continue;

            var datum = new AtmosMonitorData(GetNetEntity(ent), GetNetCoordinates(entXform.Coordinates), AtmosMonitoringConsoleGroup.AirAlarm);
            data.Add(datum);

            foreach ((var id, var sensorData) in entAirAlarm.SensorData)
            {
                temperatures.Add(sensorData.Temperature);
                pressures.Add(sensorData.Pressure);
            }
        }

        return data;
    }

    private Dictionary<Vector2i, AtmosPipeChunk> RefreshAtmosPipeGrid(EntityUid gridUid, MapGridComponent grid)
    {
        // Clears all chunks for the associated grid
        var allChunks = new Dictionary<Vector2i, AtmosPipeChunk>();
        _gridAtmosPipeChunks[gridUid] = allChunks;

        // Adds all atmos pipe to the grid
        var query = AllEntityQuery<AtmosPipeColorComponent, NodeContainerComponent, TransformComponent>();
        while (query.MoveNext(out var ent, out var entAtmosPipeColor, out var entNodeContainer, out var entXform))
        {
            if (entXform.GridUid != gridUid)
                continue;

            if (!entXform.Anchored)
                continue;

            var tile = _sharedMapSystem.GetTileRef(gridUid, grid, entXform.Coordinates);
            var chunkOrigin = SharedMapSystem.GetChunkIndices(tile.GridIndices, SharedNavMapSystem.ChunkSize);

            if (!allChunks.TryGetValue(chunkOrigin, out var chunk))
                chunk = new AtmosPipeChunk(chunkOrigin);

            if (!chunk.AtmosPipeData.TryGetValue(entAtmosPipeColor.Color.ToHex(), out var atmosPipeData))
                atmosPipeData = new AtmosPipeData();

            var relative = SharedMapSystem.GetChunkRelative(tile.GridIndices, SharedNavMapSystem.ChunkSize);

            foreach ((var id, var node) in entNodeContainer.Nodes)
            {
                if (node is not PipeNode)
                    continue;

                var pipeNode = node as PipeNode;
                var pipeDirection = pipeNode!.CurrentPipeDirection;

                var flagNorth = (((int) pipeDirection & (int) PipeDirection.North) > 0) ? (ushort) SharedNavMapSystem.GetFlag(relative) : (ushort) 0;
                var flagSouth = (((int) pipeDirection & (int) PipeDirection.South) > 0) ? (ushort) SharedNavMapSystem.GetFlag(relative) : (ushort) 0;
                var flagEast = (((int) pipeDirection & (int) PipeDirection.East) > 0) ? (ushort) SharedNavMapSystem.GetFlag(relative) : (ushort) 0;
                var flagWest = (((int) pipeDirection & (int) PipeDirection.West) > 0) ? (ushort) SharedNavMapSystem.GetFlag(relative) : (ushort) 0;

                atmosPipeData.NorthFacing |= flagNorth;
                atmosPipeData.SouthFacing |= flagSouth;
                atmosPipeData.EastFacing |= flagEast;
                atmosPipeData.WestFacing |= flagWest;

                chunk.AtmosPipeData[entAtmosPipeColor.Color.ToHex()] = atmosPipeData;
            }

            allChunks[chunkOrigin] = chunk;
        }

        return allChunks;
    }

    private void RefreshAtmosMonitoringConsole(EntityUid uid, AtmosMonitoringConsoleComponent component)
    {
        var xform = Transform(uid);

        if (xform.GridUid == null)
            return;

        var grid = xform.GridUid.Value;

        if (!TryComp<MapGridComponent>(grid, out var map))
            return;

        if (!_gridAtmosPipeChunks.TryGetValue(grid, out var allChunks))
            allChunks = RefreshAtmosPipeGrid(grid, map);

        component.AllChunks = RefreshAtmosPipeGrid(grid, map);
        component.AtmosMonitors = GetAtmosMonitorData(grid);

        Dirty(uid, component);
    }

    public float GetStandardDeviation(IEnumerable<float> values)
    {
        float avg = values.Average();
        return (float) Math.Sqrt(values.Average(v => Math.Pow(v - avg, 2)));
    }
}
