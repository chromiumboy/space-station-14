using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;

namespace Content.Server.NPC.Queries.Considerations;

/// <summary>
/// Goes linearly from 1f to 0f, with 0 damage returning 1f and <see cref=TargetState> damage returning 0f
/// </summary>
public sealed partial class TargetHealthCon : UtilityConsideration
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    private MobThresholdSystem _thresholdSystem = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _thresholdSystem = _entManager.System<MobThresholdSystem>();
    }

    /// <summary>
    /// Which MobState the consideration returns 0f at, defaults to choosing earliest incapacitating MobState
    /// </summary>
    [DataField]
    public MobState TargetState = MobState.Invalid;

    public override float GetScore(NPCBlackboard blackboard, EntityUid targetUid, UtilityConsideration consideration)
    {
        if (!_entManager.TryGetComponent(targetUid, out DamageableComponent? damage))
            return 0f;

        if (TargetState != MobState.Invalid && _thresholdSystem.TryGetPercentageForState(targetUid, TargetState, damage.TotalDamage, out var percentage))
            return Math.Clamp((float)(1 - percentage), 0f, 1f);

        if (_thresholdSystem.TryGetIncapPercentage(targetUid, damage.TotalDamage, out var incapPercentage))
            return Math.Clamp((float)(1 - incapPercentage), 0f, 1f);

        return 0f;
    }
}
