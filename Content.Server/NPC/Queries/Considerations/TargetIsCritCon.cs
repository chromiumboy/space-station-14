using Content.Shared.Mobs.Systems;

namespace Content.Server.NPC.Queries.Considerations;

/// <summary>
/// Returns 1f if the target is crit or 0f if not.
/// </summary>
public sealed partial class TargetIsCritCon : UtilityConsideration
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    private MobStateSystem _mobState = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _mobState = _entManager.System<MobStateSystem>();
    }

    public override float GetScore(NPCBlackboard blackboard, EntityUid targetUid, UtilityConsideration consideration)
    {
        return _mobState.IsCritical(targetUid) ? 1f : 0f;
    }
}
