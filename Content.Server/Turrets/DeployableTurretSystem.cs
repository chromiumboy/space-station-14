using Content.Server.NPC.HTN;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Repairable;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.Destructible;
using Content.Shared.Interaction;
using Content.Shared.Timing;
using Content.Shared.Turrets;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Utility;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Physics;
using Content.Shared.Popups;
using Robust.Shared.Timing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio;

namespace Content.Server.Turrets;

public sealed partial class DeployableTurretSystem : EntitySystem
{
    [Dependency] private readonly UseDelaySystem _useDelay = default!;
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<DeployableTurretComponent, GetVerbsEvent<Verb>>(OnGetVerb);
        SubscribeLocalEvent<DeployableTurretComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<DeployableTurretComponent, AmmoShotEvent>(OnAmmoShot);
        SubscribeLocalEvent<DeployableTurretComponent, ChargeChangedEvent>(OnChargeChanged);
        SubscribeLocalEvent<DeployableTurretComponent, BreakageEventArgs>(OnBroken, after: [typeof(RepairableTurretSystem)]);
        SubscribeLocalEvent<DeployableTurretComponent, RepairedEvent>(OnRepaired, after: [typeof(RepairableTurretSystem)]);
    }

    private void OnGetVerb(Entity<DeployableTurretComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !args.CanComplexInteract)
            return;

        if (TryComp<AccessReaderComponent>(ent, out var accessReader) && !_accessReader.IsAllowed(args.User, ent, accessReader))
            return;

        var user = args.User;

        var verb = new Verb
        {
            Priority = 1,
            Text = ent.Comp.Enabled ? Loc.GetString("deployable-turret-component-deactivate") : Loc.GetString("deployable-turret-component-activate"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/Spare/poweronoff.svg.192dpi.png")),
            Disabled = !HasAmmo(ent),
            Impact = LogImpact.Low,
            Act = () => { TryToggleState(ent, user); }
        };

        args.Verbs.Add(verb);
    }

    private void OnActivate(Entity<DeployableTurretComponent> ent, ref ActivateInWorldEvent args)
    {
        if (TryComp(ent, out UseDelayComponent? useDelay) && !_useDelay.TryResetDelay((ent, useDelay), true))
            return;

        if (TryComp<AccessReaderComponent>(ent, out var reader) && !_accessReader.IsAllowed(args.User, ent, reader))
        {
            _popup.PopupEntity(Loc.GetString("deployable-turret-component-access-denied"), ent, args.User);
            _audio.PlayPvs(ent.Comp.AccessDeniedSound, ent);

            return;
        }

        TryToggleState(ent, args.User);
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

    private void OnBroken(Entity<DeployableTurretComponent> ent, ref BreakageEventArgs args)
    {
        ent.Comp.Broken = true;
    }

    private void OnRepaired(Entity<DeployableTurretComponent> ent, ref RepairedEvent args)
    {
        ent.Comp.Broken = false;
    }

    public void TryToggleState(Entity<DeployableTurretComponent> ent, EntityUid? user = null)
    {
        TrySetState(ent, !ent.Comp.Enabled, user);
    }

    public bool TrySetState(Entity<DeployableTurretComponent> ent, bool enabled, EntityUid? user = null)
    {
        if (ent.Comp.Broken)
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("deployable-turret-component-is-broken"), ent, user.Value);

            return false;
        }

        if (enabled && !HasAmmo(ent))
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("deployable-turret-component-no-ammo"), ent, user.Value);

            return false;
        }

        SetState(ent, enabled, null);

        return true;
    }

    private void SetState(Entity<DeployableTurretComponent> ent, bool enabled, EntityUid? user = null)
    {
        if (ent.Comp.Enabled == enabled)
            return;

        ent.Comp.Enabled = enabled;

        // If animating, determine how much time is remaining
        var animTimeRemaining = MathF.Max((float)(ent.Comp.AnimationCompletionTime - _timing.CurTime).TotalSeconds, 0f);

        // Calculate the time the queued animation should end
        var animTimeNext = ent.Comp.Enabled ? ent.Comp.DeploymentLength : ent.Comp.RetractionLength;
        ent.Comp.AnimationCompletionTime = _timing.CurTime + TimeSpan.FromSeconds(animTimeNext + animTimeRemaining);

        // End/restart any tasks the NPC was doing
        // Delay the resumption of any tasks based on the queued animation length
        var planCooldown = animTimeRemaining + animTimeNext;

        if (TryComp<HTNComponent>(ent, out var htn))
            _htn.SetHTNEnabled((ent, htn), ent.Comp.Enabled, planCooldown + 0.5f);

        // Change the turret's damage modifiers
        if (TryComp<DamageableComponent>(ent, out var damageable))
        {
            var damageSetID = ent.Comp.Enabled ? ent.Comp.DeployedDamageModifierSetId : ent.Comp.RetractedDamageModifierSetId;
            _damageable.SetDamageModifierSetId(ent, damageSetID, damageable);
        }

        // Change the turret's fixtures
        if (ent.Comp.DeployedFixture != null &&
            TryComp(ent, out FixturesComponent? fixtures) &&
            fixtures.Fixtures.TryGetValue(ent.Comp.DeployedFixture, out var fixture))
        {
            _physics.SetHard(ent, fixture, ent.Comp.Enabled);
        }

        // Messages / audio
        if (user != null)
        {
            var msg = ent.Comp.Enabled ? "deployable-turret-component-activating" : "deployable-turret-component-deactivating";
            _popup.PopupEntity(Loc.GetString(msg), ent, user.Value);
        }

        _audio.PlayPvs(ent.Comp.Enabled ? ent.Comp.DeploymentSound : ent.Comp.RetractionSound, ent, new AudioParams { Volume = -10f });

        // Update appearance
        UpdateAppearance(ent);
    }

    private void UpdateAppearance(Entity<DeployableTurretComponent> ent, AppearanceComponent? appearance = null)
    {
        if (!Resolve(ent, ref appearance))
            return;

        var state = ent.Comp.Enabled ? PopupTurretVisualState.Deployed : PopupTurretVisualState.Retracted;
        _appearance.SetData(ent, PopupTurretVisuals.Turret, state, appearance);
    }

    private bool HasAmmo(Entity<DeployableTurretComponent> ent)
    {
        if (TryComp<ProjectileBatteryAmmoProviderComponent>(ent, out var projectilebatteryAmmo) &&
            (projectilebatteryAmmo.Shots > 0 || this.IsPowered(ent, EntityManager)))
            return true;

        if (TryComp<HitscanBatteryAmmoProviderComponent>(ent, out var hitscanBatteryAmmo) &&
            (hitscanBatteryAmmo.Shots > 0 || this.IsPowered(ent, EntityManager)))
            return true;

        if (TryComp<BallisticAmmoProviderComponent>(ent, out var ballisticAmmo) &&
            ballisticAmmo.Count > 0)
            return true;

        return false;
    }
}
