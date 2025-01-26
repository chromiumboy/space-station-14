using Content.Shared.Examine;

namespace Content.Server.NPC.Queries.Considerations;

/// <summary>
/// Placeholder considerations -> returns 1f if they're in LOS or the current target.
/// </summary>
public sealed partial class TargetInLOSOrCurrentCon : UtilityConsideration
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
        const float bufferRange = 0.5f;

        if (blackboard.TryGetValue<EntityUid>("Target", out var currentTarget, _entManager) &&
            currentTarget == targetUid &&
            _entManager.TryGetComponent(owner, out TransformComponent? xform) &&
            _entManager.TryGetComponent(targetUid, out TransformComponent? targetXform) &&
            xform.Coordinates.TryDistance(_entManager, targetXform.Coordinates, out var distance) &&
            distance <= radius + bufferRange)
        {
            return 1f;
        }

        return _examine.InRangeUnOccluded(owner, targetUid, radius + bufferRange, null) ? 1f : 0f;
    }
}
