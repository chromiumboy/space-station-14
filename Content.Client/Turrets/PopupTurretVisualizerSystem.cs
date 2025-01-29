using Content.Shared.Turrets;
using Robust.Client.Animations;
using Robust.Client.GameObjects;

namespace Content.Client.Turrets;

public sealed partial class PopupTurretVisualizerSystem : VisualizerSystem<PopupTurretComponent>
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PopupTurretComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<PopupTurretComponent, AnimationCompletedEvent>(OnAnimationCompleted);
    }

    private void OnComponentInit(Entity<PopupTurretComponent> ent, ref ComponentInit args)
    {
        ent.Comp.DeploymentAnimation = new Animation
        {
            Length = TimeSpan.FromSeconds(ent.Comp.DeploymentLength),
            AnimationTracks = {
                new AnimationTrackSpriteFlick() {
                    LayerKey = PopupTurretVisualLayers.Turret,
                    KeyFrames = {new AnimationTrackSpriteFlick.KeyFrame(ent.Comp.DeployingState, 0f)}
                },
            }
        };

        ent.Comp.RetractionAnimation = new Animation
        {
            Length = TimeSpan.FromSeconds(ent.Comp.RetractionLength),
            AnimationTracks = {
                new AnimationTrackSpriteFlick() {
                    LayerKey = PopupTurretVisualLayers.Turret,
                    KeyFrames = {new AnimationTrackSpriteFlick.KeyFrame(ent.Comp.RetractingState, 0f)}
                },
            }
        };
    }

    private void OnAnimationCompleted(Entity<PopupTurretComponent> ent, ref AnimationCompletedEvent args)
    {
        if (args.Key != PopupTurretComponent.AnimationKey)
            return;

        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        if (!TryComp<AnimationPlayerComponent>(ent, out var animPlayer))
            return;

        if (!AppearanceSystem.TryGetData<PopupTurretVisualState>(ent, PopupTurretVisuals.Turret, out var state))
            state = ent.Comp.CurrentState;

        // Convert to terminal state
        var targetState = (ent.Comp.CurrentState == PopupTurretVisualState.Deployed || ent.Comp.CurrentState == PopupTurretVisualState.Deploying) ?
            PopupTurretVisualState.Deployed : PopupTurretVisualState.Retracted;

        UpdateVisuals(ent, targetState, sprite, animPlayer);
    }

    protected override void OnAppearanceChange(EntityUid uid, PopupTurretComponent comp, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!TryComp<AnimationPlayerComponent>(uid, out var animPlayer))
            return;

        if (!AppearanceSystem.TryGetData<PopupTurretVisualState>(uid, PopupTurretVisuals.Turret, out var state, args.Component))
            state = PopupTurretVisualState.Retracted;

        UpdateVisuals((uid, comp), state, args.Sprite, animPlayer);
    }

    private void UpdateVisuals(Entity<PopupTurretComponent> ent, PopupTurretVisualState state, SpriteComponent sprite, AnimationPlayerComponent? animPlayer = null)
    {
        if (!Resolve(ent, ref animPlayer))
            return;

        if (AnimationSystem.HasRunningAnimation(ent, animPlayer, PopupTurretComponent.AnimationKey))
            return;

        if (state != ent.Comp.CurrentState)
        {
            // Compare whether the current destination state matches the one of the target state
            var targetState = PopupTurretVisualState.Deployed;

            if (state == PopupTurretVisualState.Retracting || state == PopupTurretVisualState.Retracted)
                targetState = PopupTurretVisualState.Retracted;

            var destinationState = PopupTurretVisualState.Deployed;

            if (ent.Comp.CurrentState == PopupTurretVisualState.Retracting || ent.Comp.CurrentState == PopupTurretVisualState.Retracted)
                destinationState = PopupTurretVisualState.Retracted;

            // If these two states do not match, start the transition to the target state
            if (targetState != destinationState)
                targetState = (targetState == PopupTurretVisualState.Deployed || targetState == PopupTurretVisualState.Deploying) ?
                    PopupTurretVisualState.Deploying : PopupTurretVisualState.Retracting;

            ent.Comp.CurrentState = state;
            state = targetState;
        }

        // Adjust sprite data
        switch (state)
        {
            case PopupTurretVisualState.Deploying:
                AnimationSystem.Play((ent, animPlayer), ent.Comp.DeploymentAnimation, PopupTurretComponent.AnimationKey);
                break;

            case PopupTurretVisualState.Retracting:
                AnimationSystem.Play((ent, animPlayer), ent.Comp.RetractionAnimation, PopupTurretComponent.AnimationKey);
                break;

            case PopupTurretVisualState.Deployed:
                sprite.LayerSetState(PopupTurretVisualLayers.Turret, ent.Comp.DeployedState);
                break;

            case PopupTurretVisualState.Retracted:
                sprite.LayerSetState(PopupTurretVisualLayers.Turret, ent.Comp.RetractedState);
                break;
        }
    }
}
