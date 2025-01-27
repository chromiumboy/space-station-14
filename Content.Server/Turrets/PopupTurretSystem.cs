using Content.Server.NPC.HTN;
using Content.Server.Popups;
using Content.Server.Power.EntitySystems;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.Interaction;
using Content.Shared.Power;
using Content.Shared.Timing;
using Content.Shared.Turrets;
using Content.Shared.Verbs;
using Robust.Shared.Utility;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Physics;

namespace Content.Server.Turrets;

public sealed partial class PopupTurretSystem : EntitySystem
{
    [Dependency] private readonly UseDelaySystem _useDelay = default!;
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<PopupTurretComponent, GetVerbsEvent<Verb>>(OnGetVerb);
        SubscribeLocalEvent<PopupTurretComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<PopupTurretComponent, PowerChangedEvent>(OnPowerChanged);
    }

    private void OnGetVerb(Entity<PopupTurretComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !args.CanComplexInteract)
            return;

        if (TryComp<AccessReaderComponent>(ent, out var accessReader) && !_accessReader.IsAllowed(args.User, ent, accessReader))
            return;

        var user = args.User;

        var verb = new Verb
        {
            Priority = 1,
            Text = ent.Comp.Enabled ? Loc.GetString("popup-turret-component-deactivate") : Loc.GetString("popup-turret-component-activate"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/Spare/poweronoff.svg.192dpi.png")),
            Disabled = !this.IsPowered(ent, EntityManager),
            Impact = LogImpact.Low,
            Act = () => { ToggleTurret(ent, user); }
        };

        args.Verbs.Add(verb);
    }

    private void OnActivate(Entity<PopupTurretComponent> ent, ref ActivateInWorldEvent args)
    {
        if (TryComp(ent, out UseDelayComponent? useDelay) && !_useDelay.TryResetDelay((ent, useDelay), true))
            return;

        if (TryComp<AccessReaderComponent>(ent, out var reader) && !_accessReader.IsAllowed(args.User, ent, reader))
        {
            _popup.PopupEntity(Loc.GetString("popup-turret-component-no-access"), ent, args.User);
            return;
        }

        ToggleTurret(ent, args.User);
    }

    private void OnPowerChanged(Entity<PopupTurretComponent> ent, ref PowerChangedEvent args)
    {
        if (ent.Comp.Enabled && !args.Powered)
            ToggleTurret(ent);
    }

    private void ToggleTurret(Entity<PopupTurretComponent> ent, EntityUid? user = null)
    {
        if (!this.IsPowered(ent, EntityManager))
            return;

        ent.Comp.Enabled = !ent.Comp.Enabled;

        // End/restart any tasks the NPC was doing
        var planCooldown = ent.Comp.Enabled ? ent.Comp.DeploymentAnimLength : ent.Comp.RetractionAnimLength;

        if (TryComp<HTNComponent>(ent, out var htn))
            _htn.SetHTNEnabled((ent, htn), ent.Comp.Enabled, planCooldown);

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

        // Show message to the player
        if (user != null)
        {
            var msg = ent.Comp.Enabled ? "popup-turret-component-activating" : "popup-turret-component-deactivating";
            _popup.PopupEntity(Loc.GetString(msg), ent, user.Value);
        }

        // Update appearance
        UpdateAppearance(ent);
    }

    private void UpdateAppearance(Entity<PopupTurretComponent> ent, AppearanceComponent? appearance = null)
    {
        if (!Resolve(ent, ref appearance))
            return;

        var state = ent.Comp.Enabled ? PopupTurretVisualState.Deployed : PopupTurretVisualState.Retracted;
        _appearance.SetData(ent, PopupTurretVisuals.Turret, state, appearance);
    }
}
