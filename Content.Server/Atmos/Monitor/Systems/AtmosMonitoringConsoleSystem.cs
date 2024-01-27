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

namespace Content.Server.Atmos.Monitor.Systems;

public sealed class AtmosMonitoringConsoleSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _userInterfaceSystem = default!;
    [Dependency] private readonly SharedMapSystem _sharedMapSystem = default!;

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

        // Set the UI state
        /*_userInterfaceSystem.SetUiState(bui,
            new PowerMonitoringConsoleBoundInterfaceState
                (totalSources,
                totalBatteryUsage,
                totalLoads,
                allEntries.ToArray(),
                sourcesForFocus.ToArray(),
                loadsForFocus.ToArray()),
            session);*/
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

        Dirty(uid, component);
    }
}
