using Content.Shared.Disposal.Components;
using Content.Shared.Disposal.Transit;
using Content.Shared.Disposal.Unit;
using Content.Shared.DoAfter;

namespace Content.Server.Disposal.Transit;

/// <inheritdoc/>
public sealed partial class TransitTubeStationSystem : SharedTransitTubeStationSystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedDisposalUnitSystem _disposalUnit = default!;

    // TODO: Move this to shared once the issues with animation flickering is resolved.

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TransitTubeStationComponent, DoAfterAttemptEvent<DisposalDoAfterEvent>>(OnStartInsert);
        SubscribeLocalEvent<TransitTubeStationComponent, DisposalDoAfterEvent>(OnInsert, after: [typeof(SharedDisposalUnitSystem)]);
    }

    private void OnStartInsert(Entity<TransitTubeStationComponent> ent, ref DoAfterAttemptEvent<DisposalDoAfterEvent> args)
    {
        if (ent.Comp.CurrentState == TransitTubeStationState.Open)
            return;

        ent.Comp.CurrentState = TransitTubeStationState.Open;
        Dirty(ent);

        _appearance.SetData(ent, TransitTubeStationVisuals.Key, TransitTubeStationState.Open);

        if (ent.Comp.CurrentPodEffect == null)
        {
            var effect = Spawn(ent.Comp.PodCreationEffect, Transform(ent).Coordinates);
            Transform(effect).LocalRotation = Transform(ent).LocalRotation;

            ent.Comp.CurrentPodEffect = effect;
        }
    }

    private void OnInsert(Entity<TransitTubeStationComponent> ent, ref DisposalDoAfterEvent args)
    {
        if (ent.Comp.CurrentState == TransitTubeStationState.Closed)
            return;

        ent.Comp.CurrentState = TransitTubeStationState.Closed;
        Dirty(ent);

        _appearance.SetData(ent, TransitTubeStationVisuals.Key, TransitTubeStationState.Closed);

        QueueDel(ent.Comp.CurrentPodEffect);
        ent.Comp.CurrentPodEffect = null;

        if (TryComp<DisposalUnitComponent>(ent, out var disposalUnit) &&
            _disposalUnit.GetContainedEntityCount((ent, disposalUnit)) == 0)
        {
            var effect = Spawn(ent.Comp.PodVanishEffect, Transform(ent).Coordinates);
            Transform(effect).LocalRotation = Transform(ent).LocalRotation;
        }
    }
}
