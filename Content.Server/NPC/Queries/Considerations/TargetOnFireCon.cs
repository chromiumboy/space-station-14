using Content.Server.Atmos.Components;

namespace Content.Server.NPC.Queries.Considerations;

/// <summary>
/// Returns 1f if the target is on fire or 0f if not.
/// </summary>
public sealed partial class TargetOnFireCon : UtilityConsideration
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
    }

    public override float GetScore(NPCBlackboard blackboard, EntityUid targetUid, UtilityConsideration consideration)
    {
        if (_entManager.TryGetComponent(targetUid, out FlammableComponent? fire) && fire.OnFire)
            return 1f;

        return 0f;
    }
}
