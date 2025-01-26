namespace Content.Server.NPC.Queries.Considerations;

public sealed partial class OrderedTargetCon : UtilityConsideration
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
    }

    public override float GetScore(NPCBlackboard blackboard, EntityUid targetUid, UtilityConsideration consideration)
    {
        if (!blackboard.TryGetValue<EntityUid>(NPCBlackboard.CurrentOrderedTarget, out var orderedTarget, _entManager))
            return 0f;

        if (targetUid != orderedTarget)
            return 0f;

        return 1f;
    }
}
