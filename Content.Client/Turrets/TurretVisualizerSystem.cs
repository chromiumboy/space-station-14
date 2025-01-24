using Content.Shared.Turrets;
using Robust.Client.Animations;
using Robust.Client.GameObjects;

namespace Content.Client.Turrets;

public sealed partial class TurretVisualizerSystem : VisualizerSystem<PopupTurretComponent>
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PopupTurretComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<PopupTurretComponent, AnimationCompletedEvent>(OnAnimationCompleted);
    }

    private void OnComponentInit(EntityUid uid, PopupTurretComponent comp, ComponentInit args)
    {
        comp.DeploymentAnimation = new Animation
        {
            Length = TimeSpan.FromSeconds(comp.DeploymentAnimLength),
            AnimationTracks = {
                new AnimationTrackSpriteFlick() {
                    LayerKey = PopupTurretVisualLayers.Cover,
                    KeyFrames = {new AnimationTrackSpriteFlick.KeyFrame(comp.DeployedState, 0f)}
                },
            }
        };

        comp.RetractionAnimation = new Animation
        {
            Length = TimeSpan.FromSeconds(comp.RetractionAnimLength),
            AnimationTracks = {
                new AnimationTrackSpriteFlick() {
                    LayerKey = PopupTurretVisualLayers.Cover,
                    KeyFrames = {new AnimationTrackSpriteFlick.KeyFrame(comp.RetractedState, 0f)}
                },
            }
        };
    }

    private void OnAnimationCompleted(EntityUid uid, PopupTurretComponent comp, AnimationCompletedEvent args)
    {
        if (args.Key != PopupTurretComponent.AnimationKey)
            return;

        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        if (!TryComp<AnimationPlayerComponent>(uid, out var animPlayer))
            return;

        if (!AppearanceSystem.TryGetData<PopupTurretVisualState>(uid, PopupTurretVisuals.State, out var state))
            state = comp.CurrentState;

        // Convert to terminal state
        var targetState = comp.CurrentState == PopupTurretVisualState.Deploying ? PopupTurretVisualState.Deployed : PopupTurretVisualState.Retracted;

        UpdateVisuals(uid, targetState, comp, sprite, animPlayer);
    }

    protected override void OnAppearanceChange(EntityUid uid, PopupTurretComponent comp, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;
        if (!TryComp<AnimationPlayerComponent>(uid, out var animPlayer))
            return;

        if (!AppearanceSystem.TryGetData<PopupTurretVisualState>(uid, PopupTurretVisuals.State, out var state, args.Component))
            state = PopupTurretVisualState.Retracted;

        UpdateVisuals(uid, state, comp, args.Sprite, animPlayer);
    }

    private void UpdateVisuals(EntityUid uid, PopupTurretVisualState state, PopupTurretComponent comp, SpriteComponent sprite, AnimationPlayerComponent? animPlayer = null)
    {
        if (state == comp.CurrentState)
            return;

        if (!Resolve(uid, ref animPlayer))
            return;

        if (AnimationSystem.HasRunningAnimation(uid, animPlayer, PopupTurretComponent.AnimationKey))
            return;

        // Compare whether the current destination state matches the one of the target state
        var targetState = PopupTurretVisualState.Deployed;

        if (state == PopupTurretVisualState.Retracting || state == PopupTurretVisualState.Retracted)
            targetState = PopupTurretVisualState.Retracted;

        var destinationState = PopupTurretVisualState.Deployed;

        if (comp.CurrentState == PopupTurretVisualState.Retracting || comp.CurrentState == PopupTurretVisualState.Retracted)
            targetState = PopupTurretVisualState.Retracted;

        // If these two states do not match, start the transition to the target state
        if (targetState != destinationState)
            targetState = targetState == PopupTurretVisualState.Deployed ? PopupTurretVisualState.Deploying : PopupTurretVisualState.Retracting;

        comp.CurrentState = state;

        // Hide the cover when the turret is deployed
        sprite.LayerSetVisible(PopupTurretVisualLayers.Cover, targetState != PopupTurretVisualState.Deployed);

        // Adjust sprite data
        switch (targetState)
        {
            case PopupTurretVisualState.Deploying:
                AnimationSystem.Play((uid, animPlayer), comp.DeploymentAnimation, PopupTurretComponent.AnimationKey);
                break;

            case PopupTurretVisualState.Retracting:
                AnimationSystem.Play((uid, animPlayer), comp.RetractionAnimation, PopupTurretComponent.AnimationKey);
                break;

            case PopupTurretVisualState.Deployed:
                sprite.LayerSetState(PopupTurretVisualLayers.Cover, comp.DeployingState);
                break;

            case PopupTurretVisualState.Retracted:
                sprite.LayerSetState(PopupTurretVisualLayers.Cover, comp.RetractedState);
                break;
        }
    }
}
