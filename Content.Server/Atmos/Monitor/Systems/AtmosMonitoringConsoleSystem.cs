using Content.Server.Atmos.Monitor.Components;
using Content.Server.Atmos.Piping.Binary.Components;
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
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Content.Server.Atmos.Monitor.Systems;

public sealed class AtmosMonitoringConsoleSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _userInterfaceSystem = default!;
    [Dependency] private readonly SharedMapSystem _sharedMapSystem = default!;
    [Dependency] private readonly AirAlarmSystem _airAlarmSystem = default!;
    [Dependency] private readonly AtmosDeviceNetworkSystem _atmosDevNet = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

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
        SubscribeLocalEvent<AtmosMonitoringConsoleComponent, AtmosMonitoringConsoleFocusChangeMessage>(OnFocusChangedMessage);
        SubscribeLocalEvent<AtmosMonitoringConsoleComponent, AtmosMonitoringConsoleDeviceSilencedMessage>(OnDeviceSilencedMessage);

        // Grid events
        SubscribeLocalEvent<GridSplitEvent>(OnGridSplit);
        SubscribeLocalEvent<AtmosPipeColorComponent, AtmosPipeColorChangedEvent>(OnPipeColorChanged);
        SubscribeLocalEvent<AtmosPipeColorComponent, NodeGroupsRebuilt>(OnPipeNodeGroupsChanged);
        SubscribeLocalEvent<AtmosMonitoringConsoleDeviceComponent, AnchorStateChangedEvent>(OnAtmosMonitoringConsoleDeviceAnchorChanged);
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

    private void OnFocusChangedMessage(EntityUid uid, AtmosMonitoringConsoleComponent component, AtmosMonitoringConsoleFocusChangeMessage args)
    {
        component.FocusDevice = EntityManager.GetEntity(args.FocusDevice);
    }

    private void OnDeviceSilencedMessage(EntityUid uid, AtmosMonitoringConsoleComponent component, AtmosMonitoringConsoleDeviceSilencedMessage args)
    {
        if (args.SilenceDevice)
            component.SilencedDevices.Add(args.AtmosDevice);

        else
            component.SilencedDevices.Remove(args.AtmosDevice);
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

    private void OnAtmosMonitoringConsoleDeviceAnchorChanged(EntityUid uid, AtmosMonitoringConsoleDeviceComponent component, AnchorStateChangedEvent args)
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

            if (args.Anchored && TryGetAtmosDeviceNavMapData(uid, component, xform, gridUid.Value, out var data))
                entConsole.AtmosDevices.Add(data.Value);

            else if (!args.Anchored)
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

            var airAlarmEntriesForEachGrid = new Dictionary<EntityUid, AtmosMonitoringConsoleEntry[]>();
            var fireAlarmEntriesForEachGrid = new Dictionary<EntityUid, AtmosMonitoringConsoleEntry[]>();

            var query = AllEntityQuery<AtmosMonitoringConsoleComponent, TransformComponent>();
            while (query.MoveNext(out var ent, out var entConsole, out var entXform))
            {
                if (entXform?.GridUid == null)
                    continue;

                // Save a list of the console UI entries for each grid, in case multiple consoles stand on the same one
                if (!airAlarmEntriesForEachGrid.TryGetValue(entXform.GridUid.Value, out var airAlarmEntries))
                {
                    airAlarmEntries = GetAlarmStateData(entXform.GridUid.Value, AtmosMonitoringConsoleGroup.AirAlarm).ToArray();
                    airAlarmEntriesForEachGrid[entXform.GridUid.Value] = airAlarmEntries;
                }

                if (!fireAlarmEntriesForEachGrid.TryGetValue(entXform.GridUid.Value, out var fireAlarmEntries))
                {
                    fireAlarmEntries = GetAlarmStateData(entXform.GridUid.Value, AtmosMonitoringConsoleGroup.FireAlarm).ToArray();
                    fireAlarmEntriesForEachGrid[entXform.GridUid.Value] = fireAlarmEntries;
                }

                // Determine the highest level of alert the console detected (from non-silenced devices)
                var highestAlert = AtmosAlarmType.Invalid;

                foreach (var entry in airAlarmEntries)
                {
                    if (entry.AlarmState > highestAlert && !entConsole.SilencedDevices.Contains(entry.NetEntity))
                        highestAlert = entry.AlarmState;
                }

                foreach (var entry in fireAlarmEntries)
                {
                    if (entry.AlarmState > highestAlert && !entConsole.SilencedDevices.Contains(entry.NetEntity))
                        highestAlert = entry.AlarmState;
                }

                // Update the appearance of the console based on the highest recorded level of alert
                if (TryComp<AppearanceComponent>(ent, out var appearance))
                    _appearance.SetData(ent, AtmosMonitoringConsoleVisuals.ComputerLayerScreen, (int) highestAlert, appearance);

                // If the console UI is open, send its data to each subscribed session
                if (!_userInterfaceSystem.TryGetUi(ent, AtmosMonitoringConsoleUiKey.Key, out var bui))
                    continue;

                foreach (var session in bui.SubscribedSessions)
                    UpdateUIState(ent, airAlarmEntries, fireAlarmEntries, entConsole, entXform, session);
            }
        }
    }

    public void UpdateUIState
        (EntityUid uid,
        AtmosMonitoringConsoleEntry[] airAlarmStateData,
        AtmosMonitoringConsoleEntry[] fireAlarmStateData,
        AtmosMonitoringConsoleComponent component,
        TransformComponent xform,
        ICommonSession session)
    {
        if (!_userInterfaceSystem.TryGetUi(uid, AtmosMonitoringConsoleUiKey.Key, out var bui))
            return;

        var gridUid = xform.GridUid!.Value;

        if (!HasComp<MapGridComponent>(gridUid))
            return;

        // The grid must have a NavMapComponent to visualize the map in the UI
        EnsureComp<NavMapComponent>(gridUid);

        // Gathering remaining data to be send to the client
        var focusAlarmData = GetFocusAlarmData(uid, component, gridUid);

        // Set the UI state
        _userInterfaceSystem.SetUiState(bui,
            new AtmosMonitoringConsoleBoundInterfaceState(airAlarmStateData, fireAlarmStateData, focusAlarmData),
            session);
    }

    private List<AtmosMonitoringConsoleEntry> GetAlarmStateData(EntityUid gridUid, AtmosMonitoringConsoleGroup group)
    {
        var alarmStateData = new List<AtmosMonitoringConsoleEntry>();

        var queryAlarms = AllEntityQuery<AtmosMonitoringConsoleDeviceComponent, AtmosAlarmableComponent, DeviceNetworkComponent, TransformComponent>();
        while (queryAlarms.MoveNext(out var ent, out var entDevice, out var entAtmosAlarmable, out var entDeviceNetwork, out var entXform))
        {
            if (entXform.GridUid != gridUid)
                continue;

            if (!entXform.Anchored)
                continue;

            if (entDevice.Group != group)
                continue;

            // If emagged, change the alarm type to inactive, I guess?
            var alarmState = (entAtmosAlarmable.LastAlarmState == AtmosAlarmType.Emagged) ? AtmosAlarmType.Invalid : entAtmosAlarmable.LastAlarmState;

            // Unpowered alarms can't sound
            if (TryComp<ApcPowerReceiverComponent>(ent, out var entAPCPower) && !entAPCPower.Powered)
                alarmState = AtmosAlarmType.Invalid;

            var entry = new AtmosMonitoringConsoleEntry
                (GetNetEntity(ent),
                GetNetCoordinates(entXform.Coordinates),
                entDevice.Group,
                alarmState,
                MetaData(ent).EntityName,
                entDeviceNetwork.Address);

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
            return null;
        }

        if (!_userInterfaceSystem.TryGetUi(ent, SharedAirAlarmInterfaceKey.Key, out var bui) ||
            bui.SubscribedSessions.Count == 0)
        {
            _atmosDevNet.Register(component.FocusDevice.Value, null);
            _atmosDevNet.Sync(component.FocusDevice.Value, null);

            foreach ((var address, var _) in entAirAlarm.SensorData)
                _atmosDevNet.Register(uid, null);
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

        var query = AllEntityQuery<AtmosMonitoringConsoleDeviceComponent, TransformComponent>();
        while (query.MoveNext(out var ent, out var entComponent, out var entXform))
        {
            if (TryGetAtmosDeviceNavMapData(ent, entComponent, entXform, gridUid, out var data))
                atmosDeviceNavMapData.Add(data.Value);
        }

        return atmosDeviceNavMapData;
    }

    private bool TryGetAtmosDeviceNavMapData
        (EntityUid uid,
        AtmosMonitoringConsoleDeviceComponent component,
        TransformComponent xform,
        EntityUid gridUid,
        [NotNullWhen(true)] out AtmosDeviceNavMapData? output)
    {
        output = null;

        if (xform.GridUid != gridUid)
            return false;

        if (!xform.Anchored)
            return false;

        var device = new AtmosDeviceNavMapData(GetNetEntity(uid), GetNetCoordinates(xform.Coordinates), component.Group);

        if (TryComp<AtmosPipeColorComponent>(uid, out var atmosPipeColor))
            device.Color = atmosPipeColor.Color;

        if (component.Rotatable)
            device.Direction = xform.LocalRotation.GetCardinalDir();

        output = device;

        return true;
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
