using Content.Server.NPC.Queries.Considerations;
using Content.Shared.Silicons.Borgs.Components;

namespace Content.Server.NPC.HTN.Preconditions;

/// <summary>
/// Returns 1f if the target is a borg
/// </summary>
public sealed partial class TargetIsBorgCon : UtilityConsideration
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
    }

    public override float GetScore(NPCBlackboard blackboard, EntityUid targetUid, UtilityConsideration consideration)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (_entManager.HasComponent<BorgChassisComponent>(targetUid))
            return 1f;

        return 0f;
    }
}
