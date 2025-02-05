using Content.Server.DeviceNetwork.Components;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks.Operators.Combat.Ranged;
using Content.Server.Turrets;
using Content.Shared.DeviceNetwork.Systems;
using Content.Shared.Turrets;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Microsoft.Extensions.DependencyModel;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;
using System.Linq;

namespace Content.Shared.TurretController;

public sealed partial class DeployableTurretControllerSystem : SharedDeployableTurretControllerSystem
{
    [Dependency] private readonly UserInterfaceSystem _userInterfaceSystem = default!;
    [Dependency] private readonly BatteryWeaponFireModesSystem _fireModes = default!;
    [Dependency] private readonly DeployableTurretSystem _deployableTurret = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Device networking events
        SubscribeLocalEvent<DeployableTurretControllerComponent, DeviceListUpdateEvent>(OnDeviceListUpdate);
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

    protected override void ChangeArmamentSetting(Entity<DeployableTurretControllerComponent> ent, int armamentState, EntityUid? user = null)
    {
        base.ChangeArmamentSetting(ent, armamentState, user);

        // Update linked turret weapon and deployment status
        foreach (var (address, turret) in ent.Comp.LinkedTurrets)
        {
            if (TryComp<BatteryWeaponFireModesComponent>(turret, out var batteryWeaponFireModes))
                _fireModes.TrySetFireMode(turret, batteryWeaponFireModes, ent.Comp.ArmamentState, user);

            _deployableTurret.TrySetState((turret, (DeployableTurretComponent)turret.Comp), ent.Comp.ArmamentState >= 0, user);
        }
    }

    private void UpdateUIState(Entity<DeployableTurretControllerComponent> ent)
    {
        var turretStates = new List<(string, string)>();

        foreach (var (address, turret) in ent.Comp.LinkedTurrets)
            turretStates.Add((address, GetTurretStateDescription(turret)));

        var state = new DeployableTurretControllerWindowBoundInterfaceState(turretStates);
        _userInterfaceSystem.SetUiState(ent.Owner, DeployableTurretControllerUiKey.Key, state);
    }

    private string GetTurretStateDescription(EntityUid uid)
    {
        if (!TryComp<DeployableTurretComponent>(uid, out var deployableTurret) ||
            !TryComp<HTNComponent>(uid, out var htn))
            return "turret-controls-window-turret-error";

        if (deployableTurret.Broken)
            return "turret-controls-window-turret-broken";

        if (htn.Plan?.CurrentTask.Operator is GunOperator)
            return "turret-controls-window-turret-firing";

        if (deployableTurret.Enabled)
        {
            if (deployableTurret.AnimationCompletionTime > _timing.CurTime)
                return "turret-controls-window-turret-activating";

            return "turret-controls-window-turret-enabled";
        }

        else
        {
            if (deployableTurret.AnimationCompletionTime > _timing.CurTime)
                return "turret-controls-window-turret-deactivating";

            return "turret-controls-window-turret-disabled";
        }
    }

    private TimeSpan _nextUpdate = TimeSpan.Zero;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime > _nextUpdate)
        {
            _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(0.25f);

            var query = AllEntityQuery<DeployableTurretControllerComponent>();

            while (query.MoveNext(out var ent, out var controller))
            {
                UpdateUIState((ent, controller));
            }
        }
    }
}
