namespace Content.Server.NPC.Queries.Considerations;

public sealed partial class TargetDistanceCon : UtilityConsideration
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
    }

    public override float GetScore(NPCBlackboard blackboard, EntityUid targetUid, UtilityConsideration consideration)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var radius = blackboard.GetValueOrDefault<float>(blackboard.GetVisionRadiusKey(_entManager), _entManager);

        if (!_entManager.TryGetComponent(targetUid, out TransformComponent? targetXform) ||
            !_entManager.TryGetComponent(owner, out TransformComponent? xform))
        {
            return 0f;
        }

        if (!targetXform.Coordinates.TryDistance(_entManager, xform.Coordinates, out var distance))
        {
            return 0f;
        }

        return Math.Clamp(distance / radius, 0f, 1f);
    }
}
