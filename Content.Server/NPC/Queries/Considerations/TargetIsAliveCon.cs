using Content.Shared.Mobs.Systems;

namespace Content.Server.NPC.Queries.Considerations;

/// <summary>
/// Returns 1f if the target is alive (1f) or not (0f).
/// </summary>
public sealed partial class TargetIsAliveCon : UtilityConsideration
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
        return _mobState.IsAlive(targetUid) ? 1f : 0f;
    }
}
