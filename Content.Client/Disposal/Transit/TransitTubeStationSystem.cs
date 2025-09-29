using Content.Shared.Disposal.Transit;
using Content.Shared.Turrets;
using Robust.Client.Animations;
using Robust.Client.GameObjects;

namespace Content.Client.Disposal.Transit;

public sealed partial class TransitTubeStationSystem : SharedTransitTubeStationSystem
{
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly AnimationPlayerSystem _animation = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public const string AnimationKey = "transit_tube_station_animation";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TransitTubeStationComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<TransitTubeStationComponent, AnimationCompletedEvent>(OnAnimationCompleted);
        SubscribeLocalEvent<TransitTubeStationComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnComponentInit(Entity<TransitTubeStationComponent> ent, ref ComponentInit args)
    {
        ent.Comp.OpeningAnimation = new Animation
        {
            Length = ent.Comp.OpeningLength,
            AnimationTracks = {
                new AnimationTrackSpriteFlick() {
                    LayerKey = TransitTubeStationVisuals.Base,
                    KeyFrames = {new AnimationTrackSpriteFlick.KeyFrame(ent.Comp.OpeningState, 0f)}
                },
            }
        };

        ent.Comp.ClosingAnimation = new Animation
        {
            Length = ent.Comp.ClosingLength,
            AnimationTracks = {
                new AnimationTrackSpriteFlick() {
                    LayerKey = TransitTubeStationVisuals.Base,
                    KeyFrames = {new AnimationTrackSpriteFlick.KeyFrame(ent.Comp.ClosingState, 0f)}
                },
            }
        };
    }

    private void OnAnimationCompleted(Entity<TransitTubeStationComponent> ent, ref AnimationCompletedEvent args)
    {
        if (args.Key != AnimationKey)
            return;

        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        if (!_appearance.TryGetData<TransitTubeStationState>(ent, TransitTubeStationVisuals.Base, out var state))
            state = ent.Comp.CurentState;

        // Convert to terminal state
        var targetState = state & TransitTubeStationState.Open;

        UpdateVisuals(ent, targetState, sprite, args.AnimationPlayer);
    }

    private void OnAppearanceChange(Entity<TransitTubeStationComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!TryComp<AnimationPlayerComponent>(ent, out var animPlayer))
            return;

        if (!_appearance.TryGetData<TransitTubeStationState>(ent, TransitTubeStationVisuals.Base, out var state, args.Component))
            state = TransitTubeStationState.Closed;

        UpdateVisuals(ent, state, args.Sprite, animPlayer);
    }

    private void UpdateVisuals(Entity<TransitTubeStationComponent> ent, TransitTubeStationState state, SpriteComponent sprite, AnimationPlayerComponent? animPlayer = null)
    {
        if (!Resolve(ent, ref animPlayer))
            return;

        if (_animation.HasRunningAnimation(ent, animPlayer, AnimationKey))
            return;

        var targetState = state & TransitTubeStationState.Open;
        var destinationState = ent.Comp.VisualState & TransitTubeStationState.Open;

        if (targetState != destinationState)
            targetState |= TransitTubeStationState.Closing;

        ent.Comp.VisualState = state;

        // Change the visual state
        switch (targetState)
        {
            case TransitTubeStationState.Opening:
                _animation.Play((ent, animPlayer), (Animation)ent.Comp.OpeningAnimation, AnimationKey);
                break;

            case TransitTubeStationState.Closing:
                _animation.Play((ent, animPlayer), (Animation)ent.Comp.ClosingAnimation, AnimationKey);
                break;

            case TransitTubeStationState.Open:
                _sprite.LayerSetRsiState((ent.Owner, sprite), TransitTubeStationVisuals.Base, ent.Comp.OpenState);
                break;

            case TransitTubeStationState.Closed:
                _sprite.LayerSetRsiState((ent.Owner, sprite), TransitTubeStationVisuals.Base, ent.Comp.ClosedState);
                break;
        }
    }
}
