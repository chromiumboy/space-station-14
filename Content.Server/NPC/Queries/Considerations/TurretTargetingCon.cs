using Content.Shared.Turrets;
using Microsoft.Extensions.DependencyModel;

namespace Content.Server.NPC.Queries.Considerations;

public sealed partial class TurretTargetingCon : UtilityConsideration
{
    [Dependency] private readonly IEntityManager _entManager = default!;


    private TurretTargetSettingsSystem _turretTargetSettings = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);

        _turretTargetSettings = _entManager.System<TurretTargetSettingsSystem>();
    }

    public override float GetScore(NPCBlackboard blackboard, EntityUid targetUid, UtilityConsideration consideration)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entManager.TryGetComponent<TurretTargetSettingsComponent>(owner, out var turretTargetSettings) ||
            _turretTargetSettings.EntityIsTargetForTurret((owner, turretTargetSettings), targetUid))
            return 1f;

        return 0f;
    }
}
