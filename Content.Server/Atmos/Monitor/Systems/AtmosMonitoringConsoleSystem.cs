using Content.Server.Atmos.Monitor.Components;
using Content.Server.Atmos.Piping.Components;
using Content.Server.Atmos.Piping.Unary.Components;
using Content.Server.DeviceNetwork.Components;
using Content.Server.NodeContainer;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Server.Power.Components;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.Monitor;
using Content.Shared.Atmos.Monitor.Components;
using Content.Shared.Pinpointer;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using System.Linq;

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

        // UI events
        SubscribeLocalEvent<AtmosMonitoringConsoleComponent, AtmosMonitoringConsoleMessage>(OnAtmosMonitoringConsoleMessage);

        // Grid events
        SubscribeLocalEvent<GridSplitEvent>(OnGridSplit);
        SubscribeLocalEvent<AtmosPipeColorComponent, AtmosPipeColorChangedEvent>(OnPipeColorChanged);
        SubscribeLocalEvent<AtmosMonitorComponent, AnchorStateChangedEvent>(OnAtmosMonitorAnchoringChanged);
        SubscribeLocalEvent<AirAlarmComponent, AnchorStateChangedEvent>(OnAirAlarmAnchoringChanged);
        SubscribeLocalEvent<AtmosPipeColorComponent, NodeGroupsRebuilt>(OnPipeNodeGroupsChanged);
    }

    #region Event handling 

    private void OnConsoleInit(EntityUid uid, AtmosMonitoringConsoleComponent component, ComponentInit args)
    {
        InitalizeAtmosMonitoringConsole(uid, component);
    }

    private void OnConsoleParentChanged(EntityUid uid, AtmosMonitoringConsoleComponent component, EntParentChangedMessage args)
    {
        InitalizeAtmosMonitoringConsole(uid, component);
    }

    private void OnAtmosMonitoringConsoleMessage(EntityUid uid, AtmosMonitoringConsoleComponent component, AtmosMonitoringConsoleMessage args)
    {
        component.FocusDevice = EntityManager.GetEntity(args.FocusDevice);
    }

    private void OnGridSplit(ref GridSplitEvent args)
    {
        // Collect grids
        var allGrids = args.NewGrids.ToList();

        if (!allGrids.Contains(args.Grid))
            allGrids.Add(args.Grid);

        // Rebuild the pipe networks on the affected grids
        foreach (var ent in allGrids)
        {
            if (!TryComp<MapGridComponent>(ent, out var grid))
                continue;

            RebuildAtmosPipeGrid(ent, grid);
        }

        // Update atmos monitoring consoles that stand upon an updated grid
        var query = AllEntityQuery<AtmosMonitoringConsoleComponent, TransformComponent>();
        while (query.MoveNext(out var ent, out var entConsole, out var entXform))
        {
            if (entXform.GridUid == null)
                continue;

            if (!allGrids.Contains(entXform.GridUid.Value))
                continue;

            InitalizeAtmosMonitoringConsole(ent, entConsole);
        }
    }

    private void OnPipeColorChanged(EntityUid uid, AtmosPipeColorComponent component, ref AtmosPipeColorChangedEvent args)
    {
        OnPipeChange(uid);
    }

    private void OnPipeNodeGroupsChanged(EntityUid uid, AtmosPipeColorComponent component, NodeGroupsRebuilt args)
    {
        OnPipeChange(uid);
    }

    private void OnPipeChange(EntityUid uid)
    {
        var xform = Transform(uid);
        var gridUid = xform.GridUid;

        if (gridUid == null)
            return;

        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        RebuildSingleTileOfPipeNetwork(gridUid.Value, grid, xform.Coordinates);
    }

    private void OnAtmosMonitorAnchoringChanged(EntityUid uid, AtmosMonitorComponent component, AnchorStateChangedEvent args)
    {
        OnAtmosMonitorChanged(uid);
    }

    private void OnAirAlarmAnchoringChanged(EntityUid uid, AirAlarmComponent component, AnchorStateChangedEvent args)
    {
        OnAtmosMonitorChanged(uid);
    }

    private void OnAtmosMonitorChanged(EntityUid uid)
    {
        var xform = Transform(uid);
        var gridUid = xform.GridUid;

        if (gridUid == null)
            return;

        var netEntity = EntityManager.GetNetEntity(uid);

        var query = AllEntityQuery<AtmosMonitoringConsoleComponent, TransformComponent>();
        while (query.MoveNext(out var ent, out var entConsole, out var entXform))
        {
            if (gridUid != entXform.GridUid)
                continue;

            if (xform.Anchored)
                entConsole.AtmosDevices.Add(GetAtmosDeviceNavMapData(uid, xform));
            else
                entConsole.AtmosDevices.RemoveWhere(x => x.NetEntity == netEntity);
        }
    }

    #endregion

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

        if (!HasComp<MapGridComponent>(gridUid))
            return;

        // The grid must have a NavMapComponent to visualize the map in the UI
        EnsureComp<NavMapComponent>(gridUid);

        // Gathering data to be send to the client
        var airAlarmStateData = GetAirAlarmStateData(gridUid).ToArray();
        var focusAlarmData = GetFocusAlarmData(uid, component, gridUid);

        // Set the UI state
        _userInterfaceSystem.SetUiState(bui,
            new AtmosMonitoringConsoleBoundInterfaceState(airAlarmStateData, focusAlarmData),
            session);
    }

    private List<AtmosMonitoringConsoleEntry> GetAirAlarmStateData(EntityUid gridUid)
    {
        var alarmStateData = new List<AtmosMonitoringConsoleEntry>();

        var queryAirAlarms = AllEntityQuery<AirAlarmComponent, DeviceNetworkComponent, ApcPowerReceiverComponent, TransformComponent>();
        while (queryAirAlarms.MoveNext(out var ent, out var entAirAlarm, out var entDeviceNetwork, out var entAPCPower, out var entXform))
        {
            if (entXform.GridUid != gridUid)
                continue;

            if (!entXform.Anchored)
                continue;

            // If emagged, change the alarm type to danger, I guess?
            var alarmState = (entAirAlarm.State == AtmosAlarmType.Emagged) ? AtmosAlarmType.Danger : entAirAlarm.State;

            // If unpowered the alarm can't sound
            if (!entAPCPower.Powered)
                alarmState = AtmosAlarmType.Invalid;

            var entry = new AtmosMonitoringConsoleEntry(GetNetEntity(ent), GetNetCoordinates(entXform.Coordinates), alarmState, entDeviceNetwork.Address);
            alarmStateData.Add(entry);
        }

        return alarmStateData;
    }

    private AtmosFocusDeviceData? GetFocusAlarmData(EntityUid uid, AtmosMonitoringConsoleComponent component, EntityUid gridUid)
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

        return new AtmosFocusDeviceData(GetNetEntity(component.FocusDevice.Value), temperatureData, pressureData, gasData);
    }

    private HashSet<AtmosDeviceNavMapData> GetAllAtmosDeviceNavMapData(EntityUid gridUid)
    {
        var atmosDeviceNavMapData = new HashSet<AtmosDeviceNavMapData>();

        var queryAtmosMonitors = AllEntityQuery<AtmosMonitorComponent, TransformComponent>();
        while (queryAtmosMonitors.MoveNext(out var ent, out var _, out var entXform))
        {
            if (entXform.GridUid != gridUid)
                continue;

            if (!entXform.Anchored)
                continue;

            atmosDeviceNavMapData.Add(GetAtmosDeviceNavMapData(ent, entXform));
        }

        var queryAirAlarms = AllEntityQuery<AirAlarmComponent, ApcPowerReceiverComponent, TransformComponent>();
        while (queryAirAlarms.MoveNext(out var ent, out var entAirAlarm, out var entAPCPower, out var entXform))
        {
            if (entXform.GridUid != gridUid)
                continue;

            if (!entXform.Anchored)
                continue;

            atmosDeviceNavMapData.Add(GetAtmosDeviceNavMapData(ent, entXform));
        }

        return atmosDeviceNavMapData;
    }

    private AtmosDeviceNavMapData GetAtmosDeviceNavMapData(EntityUid uid, TransformComponent xform)
    {
        var group = AtmosMonitoringConsoleGroup.Invalid;

        if (HasComp<GasVentPumpComponent>(uid))
            group = AtmosMonitoringConsoleGroup.GasVentPump;

        else if (HasComp<GasVentScrubberComponent>(uid))
            group = AtmosMonitoringConsoleGroup.GasVentScrubber;

        else if (HasComp<AirAlarmComponent>(uid))
            group = AtmosMonitoringConsoleGroup.AirAlarm;

        var data = new AtmosDeviceNavMapData(GetNetEntity(uid), GetNetCoordinates(xform.Coordinates), group);

        if (TryComp<AtmosPipeColorComponent>(uid, out var atmosPipeColor))
            data.Color = atmosPipeColor.Color;

        return data;
    }

    private Dictionary<Vector2i, AtmosPipeChunk> RebuildAtmosPipeGrid(EntityUid gridUid, MapGridComponent grid)
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


    private void RebuildSingleTileOfPipeNetwork(EntityUid gridUid, MapGridComponent grid, EntityCoordinates coordinates)
    {
        if (!_gridAtmosPipeChunks.TryGetValue(gridUid, out var allChunks))
            allChunks = new Dictionary<Vector2i, AtmosPipeChunk>();

        var tile = _sharedMapSystem.GetTileRef(gridUid, grid, coordinates);
        var chunkOrigin = SharedMapSystem.GetChunkIndices(tile.GridIndices, SharedNavMapSystem.ChunkSize);
        var relative = SharedMapSystem.GetChunkRelative(tile.GridIndices, SharedNavMapSystem.ChunkSize);

        if (!allChunks.TryGetValue(chunkOrigin, out var chunk))
            chunk = new AtmosPipeChunk(chunkOrigin);

        foreach (var ent in _sharedMapSystem.GetAnchoredEntities(gridUid, grid, coordinates))
        {
            if (!TryComp<NodeContainerComponent>(ent, out var entNodeContainer))
                continue;

            if (!TryComp<AtmosPipeColorComponent>(ent, out var entAtmosPipeColor))
                continue;

            if (!chunk.AtmosPipeData.TryGetValue(entAtmosPipeColor.Color.ToHex(), out var atmosPipeData))
                atmosPipeData = new AtmosPipeData();

            chunk.AtmosPipeData[entAtmosPipeColor.Color.ToHex()] = UpdateAtmosPipeData(atmosPipeData, relative, entNodeContainer);
        }

        allChunks[chunkOrigin] = chunk;

        var query = AllEntityQuery<AtmosMonitoringConsoleComponent, TransformComponent>();
        while (query.MoveNext(out var ent, out var entConsole, out var entXform))
        {
            if (gridUid != entXform.GridUid)
                continue;

            entConsole.AtmosPipeChunks = allChunks;
            Dirty(ent, entConsole);
        }
    }

    private AtmosPipeData UpdateAtmosPipeData(AtmosPipeData atmosPipeData, Vector2i positionInChunk, NodeContainerComponent nodeContainer)
    {
        foreach ((var _, var node) in nodeContainer.Nodes)
        {
            if (node is not PipeNode)
                continue;

            var pipeNode = node as PipeNode;
            var pipeDirection = pipeNode!.CurrentPipeDirection;

            var flagNorth = (((int) pipeDirection & (int) PipeDirection.North) > 0) ? (ushort) SharedNavMapSystem.GetFlag(positionInChunk) : (ushort) 0;
            var flagSouth = (((int) pipeDirection & (int) PipeDirection.South) > 0) ? (ushort) SharedNavMapSystem.GetFlag(positionInChunk) : (ushort) 0;
            var flagEast = (((int) pipeDirection & (int) PipeDirection.East) > 0) ? (ushort) SharedNavMapSystem.GetFlag(positionInChunk) : (ushort) 0;
            var flagWest = (((int) pipeDirection & (int) PipeDirection.West) > 0) ? (ushort) SharedNavMapSystem.GetFlag(positionInChunk) : (ushort) 0;

            atmosPipeData.NorthFacing |= flagNorth;
            atmosPipeData.SouthFacing |= flagSouth;
            atmosPipeData.EastFacing |= flagEast;
            atmosPipeData.WestFacing |= flagWest;
        }

        return atmosPipeData;
    }

    private void InitalizeAtmosMonitoringConsole(EntityUid uid, AtmosMonitoringConsoleComponent component)
    {
        var xform = Transform(uid);

        if (xform.GridUid == null)
            return;

        var grid = xform.GridUid.Value;

        if (!TryComp<MapGridComponent>(grid, out var map))
            return;

        if (!_gridAtmosPipeChunks.TryGetValue(grid, out var allChunks))
            allChunks = RebuildAtmosPipeGrid(grid, map);

        component.AtmosPipeChunks = allChunks;
        component.AtmosDevices = GetAllAtmosDeviceNavMapData(grid);

        Dirty(uid, component);
    }
}
