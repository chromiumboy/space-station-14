using Content.Shared.Damage.Components;

namespace Content.Server.NPC.Queries.Considerations;

/// <summary>
/// Returns 1f if the target is crit or 0f if not.
/// </summary>
public sealed partial class TargetIsStamCritCon : UtilityConsideration
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
    }

    public override float GetScore(NPCBlackboard blackboard, EntityUid targetUid, UtilityConsideration consideration)
    {
        if (!_entManager.TryGetComponent<StaminaComponent>(targetUid, out var stamina))
            return 0f;

        return stamina.Critical ? 1f : 0f;
    }
}
