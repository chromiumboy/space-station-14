using Content.Server.DeviceNetwork.Components;
using Content.Server.DeviceNetwork.Systems;
using Content.Server.NPC.HTN;
using Content.Server.Turrets;
using Content.Shared.Access.Systems;
using Content.Shared.DeviceNetwork.Systems;
using Content.Shared.Popups;
using Content.Shared.Turrets;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Server.GameObjects;
using System.Linq;

namespace Content.Shared.TurretController;

public sealed partial class DeployableTurretControllerSystem : SharedDeployableTurretControllerSystem
{
    [Dependency] private readonly AccessReaderSystem _accessreader = default!;
    [Dependency] private readonly DeviceNetworkSystem _deviceNet = default!;
    [Dependency] private readonly UserInterfaceSystem _userInterfaceSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popups = default!;
    [Dependency] private readonly BatteryWeaponFireModesSystem _fireModes = default!;
    [Dependency] private readonly DeployableTurretSystem _deployableTurret = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DeployableTurretControllerComponent, DeviceListUpdateEvent>(OnDeviceListUpdate);

        // Handling of client messages
        SubscribeLocalEvent<DeployableTurretControllerComponent, DeployableTurretArmamentSettingChangedMessage>(OnArmamentSettingChanged);
        SubscribeLocalEvent<DeployableTurretControllerComponent, DeployableTurretExemptAccessLevelChangedMessage>(OnAccessLevelChangedChanged);
    }

    private void OnDeviceListUpdate(Entity<DeployableTurretControllerComponent> ent, ref DeviceListUpdateEvent args)
    {
        // Find all turrets linked to the controller
        var turrets = new Dictionary<string, Entity<SharedDeployableTurretComponent>>();

        foreach (var turret in args.Devices)
        {
            if (!TryComp<DeployableTurretComponent>(turret, out var deployableTurret))
                continue;

            if (!TryComp<DeviceNetworkComponent>(turret, out var deviceNetwork))
                continue;

            turrets.Add(deviceNetwork.Address, (turret, deployableTurret));
        }

        // Add new turrets to the controller
        var turretsToAdd = turrets.Keys.Except(ent.Comp.LinkedTurrets.Keys);

        foreach (var address in turretsToAdd)
            ent.Comp.LinkedTurrets.Add(address, turrets[address]);

        // Remove stale turrets from the controller
        var turretsToRemove = ent.Comp.LinkedTurrets.Keys.Except(turrets.Keys);

        foreach (var address in turretsToRemove)
            ent.Comp.LinkedTurrets.Remove(address);

        // Update any open UIs
        UpdateUIState(ent);
    }

    private void OnArmamentSettingChanged(Entity<DeployableTurretControllerComponent> ent, ref DeployableTurretArmamentSettingChangedMessage args)
    {
        if (!_accessreader.IsAllowed(args.Actor, ent))
        {
            _popups.PopupEntity(Loc.GetString("turret-controls-access-denied"), ent, args.Actor);
            // Play sound

            return;
        }

        // Update the controller
        ent.Comp.ArmamentState = args.ArmamentState;
        Dirty(ent);

        // Update linked turrets
        foreach (var (address, turret) in ent.Comp.LinkedTurrets)
        {
            if (TryComp<BatteryWeaponFireModesComponent>(turret, out var batteryWeaponFireModes))
                _fireModes.TrySetFireMode(turret, batteryWeaponFireModes, ent.Comp.ArmamentState, args.Actor);

            _deployableTurret.TrySetState((turret, (DeployableTurretComponent)turret.Comp), ent.Comp.ArmamentState >= 0, args.Actor);
        }

        UpdateUIState(ent);
    }

    private void OnAccessLevelChangedChanged(Entity<DeployableTurretControllerComponent> ent, ref DeployableTurretExemptAccessLevelChangedMessage args)
    {
        if (!_accessreader.IsAllowed(args.Actor, ent))
        {
            _popups.PopupEntity(Loc.GetString("turret-controls-access-denied"), ent, args.Actor);
            return;
        }

        // Update the controller
        if (!TryComp<TurretTargetSettingsComponent>(ent, out var targetSettings))
            return;

        if (args.Enabled)
            targetSettings.ExemptAccessLevels.Add(args.AccessLevel);

        else
            targetSettings.ExemptAccessLevels.Remove(args.AccessLevel);

        Dirty(ent, targetSettings);

        // Update linked turrets
        foreach (var (address, turret) in ent.Comp.LinkedTurrets)
        {
            if (!TryComp<TurretTargetSettingsComponent>(turret, out var turretTargetSettings))
                continue;

            turretTargetSettings.ExemptAccessLevels = targetSettings.ExemptAccessLevels;
            Dirty(turret, turretTargetSettings);
        }

        UpdateUIState(ent);
    }

    private void UpdateUIState(Entity<DeployableTurretControllerComponent> ent)
    {
        if (!TryComp<TurretTargetSettingsComponent>(ent, out var targeting))
            return;

        // Turret states
        var turretStates = new List<(string, string)>();

        foreach (var (address, turret) in ent.Comp.LinkedTurrets)
            turretStates.Add((address, GetTurretStateDescription(turret)));

        // Set the UI state
        var state = new DeployableTurretControllerWindowBoundInterfaceState(turretStates, ent.Comp.ArmamentState, targeting.ExemptAccessLevels);
        _userInterfaceSystem.SetUiState(ent.Owner, DeployableTurretControllerUiKey.StationAi, state);
    }

    private string GetTurretStateDescription(EntityUid uid)
    {
        if (!TryComp<DeployableTurretComponent>(uid, out var deployableTurret) ||
            !TryComp<HTNComponent>(uid, out var htn))
            return "deployable-turret-state-error";

        if (deployableTurret.Broken)
            return "deployable-turret-state-broken";

        if (deployableTurret.Enabled)
            return "turret-controls-window-turret-enabled";

        return "turret-controls-window-turret-disabled";
    }
}
