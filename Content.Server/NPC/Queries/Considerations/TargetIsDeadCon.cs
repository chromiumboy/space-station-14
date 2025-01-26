using Content.Shared.Mobs.Systems;

namespace Content.Server.NPC.Queries.Considerations;

/// <summary>
/// Returns 1f if the target is dead or 0f if not.
/// </summary>
public sealed partial class TargetIsDeadCon : UtilityConsideration
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    private readonly MobStateSystem _mobState = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
    }

    public override float GetScore(NPCBlackboard blackboard, EntityUid targetUid, UtilityConsideration consideration)
    {
        return _mobState.IsDead(targetUid) ? 1f : 0f;
    }
}
