using Content.Server.NPC.HTN;
using Content.Server.Power.Components;
using Content.Server.Repairable;
using Content.Shared.Destructible;
using Content.Shared.Turrets;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Timing;
using Content.Shared.Power;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Turrets;

public sealed partial class DeployableTurretSystem : SharedDeployableTurretSystem
{
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DeployableTurretComponent, AmmoShotEvent>(OnAmmoShot);
        SubscribeLocalEvent<DeployableTurretComponent, ChargeChangedEvent>(OnChargeChanged);
        SubscribeLocalEvent<DeployableTurretComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<DeployableTurretComponent, BreakageEventArgs>(OnBroken, after: [typeof(RepairableTurretSystem)]);
        SubscribeLocalEvent<DeployableTurretComponent, RepairedEvent>(OnRepaired, after: [typeof(RepairableTurretSystem)]);
    }

    private void OnAmmoShot(Entity<DeployableTurretComponent> ent, ref AmmoShotEvent args)
    {
        if (ent.Comp.Enabled && !HasAmmo(ent))
            TryToggleState(ent);
    }

    private void OnChargeChanged(Entity<DeployableTurretComponent> ent, ref ChargeChangedEvent args)
    {
        if (ent.Comp.Enabled && !HasAmmo(ent))
            TryToggleState(ent);
    }

    private void OnPowerChanged(Entity<DeployableTurretComponent> ent, ref PowerChangedEvent args)
    {
        ent.Comp.Powered = args.Powered;
        Dirty(ent);

        if (!ent.Comp.Powered && !HasAmmo(ent))
            TrySetState(ent, false);
    }

    private void OnBroken(Entity<DeployableTurretComponent> ent, ref BreakageEventArgs args)
    {
        ent.Comp.Broken = true;
        Dirty(ent);
    }

    private void OnRepaired(Entity<DeployableTurretComponent> ent, ref RepairedEvent args)
    {
        ent.Comp.Broken = false;
        Dirty(ent);
    }

    protected override void SetState(Entity<DeployableTurretComponent> ent, bool enabled, EntityUid? user = null)
    {
        if (ent.Comp.Enabled == enabled)
            return;

        base.SetState(ent, enabled, user);
        Dirty(ent);

        // Determine how much time is remaining in the current animation and the one next in queue
        var animTimeRemaining = MathF.Max((float)(ent.Comp.AnimationCompletionTime - _timing.CurTime).TotalSeconds, 0f);
        var animTimeNext = ent.Comp.Enabled ? ent.Comp.DeploymentLength : ent.Comp.RetractionLength;

        // End/restart any tasks the NPC was doing
        // Delay the resumption of any tasks based on the total animation length (plus a buffer)
        var planCooldown = animTimeRemaining + animTimeNext + 0.5f;

        if (TryComp<HTNComponent>(ent, out var htn))
            _htn.SetHTNEnabled((ent, htn), ent.Comp.Enabled, planCooldown);

        // Update appearance and play audio
        // This isn't shared to prevent visual/audio mispredicts
        if (TryComp<AppearanceComponent>(ent, out var appearance))
        {
            var state = ent.Comp.Enabled ? DeployableTurretVisualState.Deployed : DeployableTurretVisualState.Retracted;
            _appearance.SetData(ent, DeployableTurretVisuals.Turret, state, appearance);
        }

        _audio.PlayPvs(ent.Comp.Enabled ? ent.Comp.DeploymentSound : ent.Comp.RetractionSound, ent, new AudioParams { Volume = -10f });
    }
}
