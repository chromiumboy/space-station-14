using Content.Shared.Disposal.Components;
using Content.Shared.DoAfter;
using Robust.Shared.Timing;

namespace Content.Shared.Disposal.Transit;

public abstract partial class SharedTransitTubeStationSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TransitTubeStationComponent, DoAfterAttemptEvent<DisposalDoAfterEvent>>(OnStartInsert);
        SubscribeLocalEvent<TransitTubeStationComponent, DisposalDoAfterEvent>(OnInsert);
    }

    private void OnStartInsert(Entity<TransitTubeStationComponent> ent, ref DoAfterAttemptEvent<DisposalDoAfterEvent> args)
    {
        if (_timing.ApplyingState)
            return;

        ent.Comp.CurentState = TransitTubeStationState.Open;
        Dirty(ent);

        _appearance.SetData(ent, TransitTubeStationVisuals.Base, TransitTubeStationState.Open);
    }

    private void OnInsert(Entity<TransitTubeStationComponent> ent, ref DisposalDoAfterEvent args)
    {
        if (_timing.ApplyingState)
            return;

        ent.Comp.CurentState = TransitTubeStationState.Closed;
        Dirty(ent);

        _appearance.SetData(ent, TransitTubeStationVisuals.Base, TransitTubeStationState.Closed);
    }
}
