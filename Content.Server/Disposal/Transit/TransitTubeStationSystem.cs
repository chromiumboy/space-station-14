using Content.Shared.Disposal.Components;
using Content.Shared.Disposal.Transit;
using Content.Shared.DoAfter;

namespace Content.Server.Disposal.Transit;

/// <inheritdoc/>
public sealed partial class TransitTubeStationSystem : SharedTransitTubeStationSystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    // TODO: Move this to shared once the issues with animation flickering is resolved.

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TransitTubeStationComponent, DoAfterAttemptEvent<DisposalDoAfterEvent>>(OnStartInsert);
        SubscribeLocalEvent<TransitTubeStationComponent, DisposalDoAfterEvent>(OnInsert);
    }

    private void OnStartInsert(Entity<TransitTubeStationComponent> ent, ref DoAfterAttemptEvent<DisposalDoAfterEvent> args)
    {
        if (ent.Comp.CurrentState == TransitTubeStationState.Open)
            return;

        ent.Comp.CurrentState = TransitTubeStationState.Open;
        Dirty(ent);

        _appearance.SetData(ent, TransitTubeStationVisuals.Key, TransitTubeStationState.Open);
    }

    private void OnInsert(Entity<TransitTubeStationComponent> ent, ref DisposalDoAfterEvent args)
    {
        if (ent.Comp.CurrentState == TransitTubeStationState.Closed)
            return;

        ent.Comp.CurrentState = TransitTubeStationState.Closed;
        Dirty(ent);

        _appearance.SetData(ent, TransitTubeStationVisuals.Key, TransitTubeStationState.Closed);
    }
}
