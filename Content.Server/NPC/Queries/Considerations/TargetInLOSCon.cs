using Content.Shared.Examine;

namespace Content.Server.NPC.Queries.Considerations;

/// <summary>
/// Returns whether the target is in line-of-sight.
/// </summary>
public sealed partial class TargetInLOSCon : UtilityConsideration
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    private ExamineSystemShared _examine = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _examine = _entManager.System<ExamineSystemShared>();
    }

    public override float GetScore(NPCBlackboard blackboard, EntityUid targetUid, UtilityConsideration consideration)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var radius = blackboard.GetValueOrDefault<float>(blackboard.GetVisionRadiusKey(_entManager), _entManager);

        return _examine.InRangeUnOccluded(owner, targetUid, radius + 0.5f, null) ? 1f : 0f;
    }
}
