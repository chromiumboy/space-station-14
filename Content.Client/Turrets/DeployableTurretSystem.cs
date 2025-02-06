using Content.Shared.Turrets;
using Robust.Client.Animations;
using Robust.Client.GameObjects;

namespace Content.Client.Turrets;

public sealed partial class DeployableTurretSystem : SharedDeployableTurretSystem
{
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly AnimationPlayerSystem _animation = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DeployableTurretComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<DeployableTurretComponent, AnimationCompletedEvent>(OnAnimationCompleted);
        SubscribeLocalEvent<DeployableTurretComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnComponentInit(Entity<DeployableTurretComponent> ent, ref ComponentInit args)
    {
        ent.Comp.DeploymentAnimation = new Animation
        {
            Length = TimeSpan.FromSeconds(ent.Comp.DeploymentLength),
            AnimationTracks = {
                new AnimationTrackSpriteFlick() {
                    LayerKey = DeployableTurretVisualLayers.Turret,
                    KeyFrames = {new AnimationTrackSpriteFlick.KeyFrame(ent.Comp.DeployingState, 0f)}
                },
            }
        };

        ent.Comp.RetractionAnimation = new Animation
        {
            Length = TimeSpan.FromSeconds(ent.Comp.RetractionLength),
            AnimationTracks = {
                new AnimationTrackSpriteFlick() {
                    LayerKey = DeployableTurretVisualLayers.Turret,
                    KeyFrames = {new AnimationTrackSpriteFlick.KeyFrame(ent.Comp.RetractingState, 0f)}
                },
            }
        };
    }

    private void OnAnimationCompleted(Entity<DeployableTurretComponent> ent, ref AnimationCompletedEvent args)
    {
        if (args.Key != DeployableTurretComponent.AnimationKey)
            return;

        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        if (!TryComp<AnimationPlayerComponent>(ent, out var animPlayer))
            return;

        if (!_appearance.TryGetData<DeployableTurretVisualState>(ent, DeployableTurretVisuals.Turret, out var state))
            state = ent.Comp.VisualState;

        // Convert to terminal state
        var targetState = (state == DeployableTurretVisualState.Deployed || ent.Comp.VisualState == DeployableTurretVisualState.Deploying) ?
            DeployableTurretVisualState.Deployed : DeployableTurretVisualState.Retracted;

        UpdateVisuals(ent, targetState, sprite, animPlayer);
    }

    private void OnAppearanceChange(Entity<DeployableTurretComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!TryComp<AnimationPlayerComponent>(ent, out var animPlayer))
            return;

        if (!_appearance.TryGetData<DeployableTurretVisualState>(ent, DeployableTurretVisuals.Turret, out var state, args.Component))
            state = DeployableTurretVisualState.Retracted;

        UpdateVisuals(ent, state, args.Sprite, animPlayer);
    }

    private void UpdateVisuals(Entity<DeployableTurretComponent> ent, DeployableTurretVisualState state, SpriteComponent sprite, AnimationPlayerComponent? animPlayer = null)
    {
        if (!Resolve(ent, ref animPlayer))
            return;

        if (_animation.HasRunningAnimation(ent, animPlayer, DeployableTurretComponent.AnimationKey))
            return;

        if (state != ent.Comp.VisualState)
        {
            // Compare whether the current destination state matches the one of the target state
            var targetState = DeployableTurretVisualState.Deployed;

            if (state == DeployableTurretVisualState.Retracting || state == DeployableTurretVisualState.Retracted)
                targetState = DeployableTurretVisualState.Retracted;

            var destinationState = DeployableTurretVisualState.Deployed;

            if (ent.Comp.VisualState == DeployableTurretVisualState.Retracting || ent.Comp.VisualState == DeployableTurretVisualState.Retracted)
                destinationState = DeployableTurretVisualState.Retracted;

            // If these two states do not match, start the transition to the target state
            if (targetState != destinationState)
                targetState = (targetState == DeployableTurretVisualState.Deployed) ?
                    DeployableTurretVisualState.Deploying : DeployableTurretVisualState.Retracting;

            ent.Comp.VisualState = state;
            state = targetState;
        }

        // Adjust sprite data
        switch (state)
        {
            case DeployableTurretVisualState.Deploying:
                _animation.Play((ent, animPlayer), (Animation)ent.Comp.DeploymentAnimation, DeployableTurretComponent.AnimationKey);
                break;

            case DeployableTurretVisualState.Retracting:
                _animation.Play((ent, animPlayer), (Animation)ent.Comp.RetractionAnimation, DeployableTurretComponent.AnimationKey);
                break;

            case DeployableTurretVisualState.Deployed:
                sprite.LayerSetState(DeployableTurretVisualLayers.Turret, ent.Comp.DeployedState);
                break;

            case DeployableTurretVisualState.Retracted:
                sprite.LayerSetState(DeployableTurretVisualLayers.Turret, ent.Comp.RetractedState);
                break;
        }
    }
}
