using Content.Shared.Weapons.Melee;

namespace Content.Server.NPC.Queries.Considerations;

/// <summary>
/// Gets the DPS out of 100.
/// </summary>
public sealed partial class TargetMeleeCon : UtilityConsideration
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
    }

    public override float GetScore(NPCBlackboard blackboard, EntityUid targetUid, UtilityConsideration consideration)
    {
        if (_entManager.TryGetComponent<MeleeWeaponComponent>(targetUid, out var melee))
            return melee.Damage.GetTotal().Float() * melee.AttackRate / 100f;

        return 0f;
    }
}
