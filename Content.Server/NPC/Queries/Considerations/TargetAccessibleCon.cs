using Content.Server.Storage.Components;
using Content.Shared.Tools.Systems;
using Robust.Server.Containers;

namespace Content.Server.NPC.Queries.Considerations;

/// <summary>
/// Returns 1f if the target is freely accessible (e.g. not in locked storage).
/// </summary>
public sealed partial class TargetAccessibleCon : UtilityConsideration
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    private ContainerSystem _container = default!;
    private WeldableSystem _weldable = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _container = _entManager.System<ContainerSystem>();
        _weldable = _entManager.System<WeldableSystem>();
    }

    public override float GetScore(NPCBlackboard blackboard, EntityUid targetUid, UtilityConsideration consideration)
    {
        if (_container.TryGetContainingContainer(targetUid, out var container))
        {
            if (_entManager.TryGetComponent<EntityStorageComponent>(container.Owner, out var storageComponent))
            {
                if (storageComponent is { Open: false } && _weldable.IsWelded(container.Owner))
                {
                    return 0.0f;
                }
            }
            else
            {
                // If we're in a container (e.g. held or whatever) then we probably can't get it. Only exception
                // Is a locker / crate
                // TODO: Some mobs can break it so consider that.
                return 0.0f;
            }
        }

        // TODO: Pathfind there, though probably do it in a separate con.
        return 1f;
    }
}
