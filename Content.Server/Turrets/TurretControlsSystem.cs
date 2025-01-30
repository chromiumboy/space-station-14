using Content.Server.DeviceNetwork;
using Content.Server.DeviceNetwork.Components;
using Content.Server.DeviceNetwork.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.DeviceNetwork;
using Content.Shared.DeviceNetwork.Systems;
using Content.Shared.Power;
using Content.Shared.TurretControls;
using System.Linq;

namespace Content.Shared.Turrets;

public sealed partial class TurretControlsSystem : EntitySystem
{
    [Dependency] private readonly AccessReaderSystem _accessreader = default!;
    [Dependency] private readonly DeviceNetworkSystem _deviceNet = default!;

    private readonly HashSet<Entity<TurretControlsComponent>> _activeUserInterfaces = new();
    public const string SyncData = "turret_targeting_sync_data";

    private const float Delay = 1f;
    private float _timer;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TurretControlsComponent, TurretControlSettingsChangedMessage>(OnSettingsChanged);
        SubscribeLocalEvent<TurretControlsComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<TurretControlsComponent, DeviceListUpdateEvent>(OnDeviceListUpdate);
        //SubscribeLocalEvent<TurretControlsComponent, DeviceNetworkPacketEvent>(OnPacketRecv);
        SubscribeLocalEvent<TurretControlsComponent, PowerChangedEvent>(OnPowerChanged);
    }

    private void OnSettingsChanged(Entity<TurretControlsComponent> ent, ref TurretControlSettingsChangedMessage args)
    {
        if (TryComp<AccessReaderComponent>(ent, out var accessReader) && _accessreader.IsAllowed(args.Actor, ent))
            return;

        if (!TryComp<TurretTargetingComponent>(ent, out var turretTargeting))
            return;

        if (args.TargetCyborgs != null)
            turretTargeting.TargetCyborgs = args.TargetCyborgs.Value;

        if (args.TargetBasicSilicons != null)
            turretTargeting.TargetBasicSilicons = args.TargetBasicSilicons.Value;

        if (args.TargetAnimalsAndXenos != null)
            turretTargeting.TargetAnimalsAndXenos = args.TargetAnimalsAndXenos.Value;

        if (args.TargetVisibleContraband != null)
            turretTargeting.TargetVisibleContraband = args.TargetVisibleContraband.Value;

        if (args.TargetWantedCriminals != null)
            turretTargeting.TargetWantedCriminals = args.TargetWantedCriminals.Value;

        if (args.TargetUnauthorizedCrew != null)
            turretTargeting.TargetUnauthorizedCrew = args.TargetUnauthorizedCrew.Value;

        if (args.AuthorizedAccessLevels != null)
            turretTargeting.AuthorizedAccessLevels = args.AuthorizedAccessLevels;

        Dirty(ent.Owner, turretTargeting);

        SyncTurretControlsWithLinkedTurrets(ent);
    }

    private void OnInit(Entity<TurretControlsComponent> ent, ref ComponentInit args)
    {
        //_deviceLink.EnsureSourcePorts(uid, comp.DangerPort, comp.WarningPort, comp.NormalPort);
    }

    private void OnDeviceListUpdate(Entity<TurretControlsComponent> ent, ref DeviceListUpdateEvent args)
    {
        var turrets = new HashSet<Entity<TurretTargetingComponent>>();

        foreach (var turret in args.Devices)
        {
            if (!TryComp<TurretTargetingComponent>(turret, out var turretTargeting))
                continue;

            turrets.Add((turret, turretTargeting));
        }

        var turretsToAdd = turrets.Except(ent.Comp.LinkedTurrets.Keys);
        var turretsToRemove = ent.Comp.LinkedTurrets.Keys.Except(turrets);

        foreach (var turret in turretsToAdd)
        {
            if (!TryComp<DeviceNetworkComponent>(turret, out var deviceNetwork))
                continue;

            ent.Comp.LinkedTurrets.Add(turret, deviceNetwork.Address);
        }

        foreach (var turret in turretsToRemove)
        {
            ent.Comp.LinkedTurrets.Remove(turret);
        }
    }

    private void OnPowerChanged(Entity<TurretControlsComponent> ent, ref PowerChangedEvent args)
    {

    }

    private void SyncTurretControlsWithLinkedTurrets(Entity<TurretControlsComponent> ent)
    {

    }

    /// <summary>
    /// Adds an active interface to be updated.
    /// </summary>
    private void AddActiveInterface(Entity<TurretControlsComponent> ent)
    {
        _activeUserInterfaces.Add(ent);
    }

    /// <summary>
    /// 
    /// </summary>
    private void RemoveActiveInterface(Entity<TurretControlsComponent> ent)
    {
        _activeUserInterfaces.Remove(ent);
    }

    /// <summary>
    /// 
    /// </summary>
    private void SyncTurretToSelf(Entity<TurretControlsComponent> ent, string address)
    {
        var syncPayload = new NetworkPayload
        {
            [DeviceNetworkConstants.Command] = SyncData
        };

        _deviceNet.QueuePacket(ent, address, syncPayload);
    }

    /// <summary>
    ///     
    /// </summary>
    private void SyncAllTurretsToSelf(Entity<TurretControlsComponent> ent)
    {
        foreach (var addr in ent.Comp.LinkedTurrets.Values)
            SyncTurretToSelf(ent, addr);
    }

    public override void Update(float frameTime)
    {
        _timer += frameTime;

        if (_timer >= Delay)
        {
            _timer = 0f;

            foreach (var ent in _activeUserInterfaces)
            {
                SyncAllTurretsToSelf(ent);
            }
        }
    }
}
