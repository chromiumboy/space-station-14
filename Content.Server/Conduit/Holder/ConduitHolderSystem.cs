using Content.Server.Atmos.EntitySystems;
using Content.Shared.Conduit;
using Content.Shared.Conduit.Holder;
using Content.Shared.Disposal.Components;
using Robust.Shared.Random;

namespace Content.Server.Conduit.Holder;

/// <inheritdoc/>
public sealed partial class ConduitHolderSystem : SharedConduitHolderSystem
{
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly SharedTransformSystem _xformSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    /// <inheritdoc/>
    public override void TransferAtmos(Entity<ConduitHolderComponent> ent, Entity<DisposalUnitComponent> unit)
    {
        _atmos.Merge(ent.Comp.Air, unit.Comp.Air);
        unit.Comp.Air.Clear();
    }

    /// <inheritdoc/>
    protected override void ExpelAtmos(Entity<ConduitHolderComponent> ent)
    {
        if (_atmos.GetContainingMixture(ent.Owner, false, true) is { } environment)
        {
            _atmos.Merge(environment, ent.Comp.Air);
            ent.Comp.Air.Clear();
        }
    }

    /// <inheritdoc/>
    protected override bool TryEscaping(Entity<ConduitHolderComponent> ent, Entity<ConduitComponent> tube)
    {
        if (!ent.Comp.TubeVisits.TryGetValue(tube, out var visits))
            return false;

        // Check if the holder should attempt to escape the current conduit
        if (visits > ent.Comp.TubeVisitThreshold &&
            _random.NextFloat() <= ent.Comp.TubeEscapeChance)
        {
            var xform = Transform(tube);

            // Unanchor the conduit and exit
            _xformSystem.Unanchor(tube, xform);
            ExitDisposals(ent);

            return true;
        }

        return false;
    }
}
