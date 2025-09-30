using Content.Shared.Disposal.Transit;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Client.Disposal.Transit;

/// <inheritdoc/>
public sealed partial class TransitTubeStationSystem : SharedTransitTubeStationSystem
{
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly AnimationPlayerSystem _animation = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

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
        // Opening animation
        var openingAnimation = new Animation
        {
            Length = ent.Comp.OpeningLength,
            AnimationTracks = {
                new AnimationTrackSpriteFlick() {
                    LayerKey = TransitTubeStationVisualLayers.Base,
                    KeyFrames = {
                        new AnimationTrackSpriteFlick.KeyFrame(ent.Comp.OpeningState, 0f),
                        new AnimationTrackSpriteFlick.KeyFrame(ent.Comp.OpenState, (float)ent.Comp.OpeningLength.TotalSeconds)
                    }
                },
            }
        };

        if (ent.Comp.OpeningSound != null)
        {
            openingAnimation.AnimationTracks.Add(
                new AnimationTrackPlaySound
                {
                    KeyFrames = { new AnimationTrackPlaySound.KeyFrame(_audio.ResolveSound(ent.Comp.OpeningSound), 0) }
                });
        }

        ent.Comp.OpeningAnimation = openingAnimation;

        // Closing animation
        var closingAnimation = new Animation
        {
            Length = ent.Comp.ClosingLength,
            AnimationTracks = {
                new AnimationTrackSpriteFlick() {
                    LayerKey = TransitTubeStationVisualLayers.Base,
                    KeyFrames = {
                        new AnimationTrackSpriteFlick.KeyFrame(ent.Comp.ClosingState, 0f),
                        new AnimationTrackSpriteFlick.KeyFrame(ent.Comp.ClosedState, (float)ent.Comp.ClosingLength.TotalSeconds)
                    }
                },
            }
        };

        if (ent.Comp.ClosingSound != null)
        {
            closingAnimation.AnimationTracks.Add(
                new AnimationTrackPlaySound
                {
                    KeyFrames = { new AnimationTrackPlaySound.KeyFrame(_audio.ResolveSound(ent.Comp.ClosingSound), 0) }
                });
        }

        ent.Comp.ClosingAnimation = closingAnimation;
    }

    private void OnAnimationCompleted(Entity<TransitTubeStationComponent> ent, ref AnimationCompletedEvent args)
    {
        if (args.Key != AnimationKey)
            return;

        if (!_appearance.TryGetData<TransitTubeStationState>(ent, TransitTubeStationVisuals.Key, out var state))
            state = ent.Comp.CurrentState;

        UpdateVisuals(ent, state, args.AnimationPlayer);
    }

    private void OnAppearanceChange(Entity<TransitTubeStationComponent> ent, ref AppearanceChangeEvent args)
    {
        if (!_appearance.TryGetData<TransitTubeStationState>(ent, TransitTubeStationVisuals.Key, out var state, args.Component))
            state = ent.Comp.CurrentState;

        UpdateVisuals(ent, state);
    }

    private void UpdateVisuals(Entity<TransitTubeStationComponent> ent, TransitTubeStationState state, AnimationPlayerComponent? animPlayer = null)
    {
        if (_timing.ApplyingState)
            return;

        if (!Resolve(ent, ref animPlayer))
            return;

        if (_animation.HasRunningAnimation(ent, animPlayer, AnimationKey))
            return;

        var currentState = ent.Comp.VisualState & TransitTubeStationState.Open;
        var nextState = state & TransitTubeStationState.Open;
        ent.Comp.VisualState = nextState;

        // If the current and next states do not match,
        // switch to the appropriate transition state
        if (currentState != nextState)
            nextState |= TransitTubeStationState.Closing;

        // Change the visual state
        switch (nextState)
        {
            case TransitTubeStationState.Opening:
                _animation.Play((ent, animPlayer), (Animation)ent.Comp.OpeningAnimation, AnimationKey);
                break;

            case TransitTubeStationState.Closing:
                _animation.Play((ent, animPlayer), (Animation)ent.Comp.ClosingAnimation, AnimationKey);
                break;
        }
    }
}
