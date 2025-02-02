using Content.Shared.Access.Systems;
using Content.Shared.Turrets;
using Microsoft.Extensions.DependencyModel;

namespace Content.Server.NPC.Queries.Considerations;

public sealed partial class TurretTargetingCon : UtilityConsideration
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    private AccessReaderSystem _accessReader = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);

        _accessReader = _entManager.System<AccessReaderSystem>();
    }

    public override float GetScore(NPCBlackboard blackboard, EntityUid targetUid, UtilityConsideration consideration)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entManager.TryGetComponent<TurretTargetSettingsComponent>(owner, out var targeting))
            return 1f;

        // Check for authorized access
        var idCardAccessLevels = _accessReader.FindAccessTags(targetUid);

        if (targeting.ExemptAccessLevels.Count > 0)
        {
            // If the ID card contains an access level on the exemption list, they will be ignored
            foreach (var accessLevel in idCardAccessLevels)
            {
                if (targeting.ExemptAccessLevels.Contains(accessLevel))
                    return 0f;
            }
        }

        return 1f;
    }
}
