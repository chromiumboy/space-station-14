using Content.Shared.Hands.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Whitelist;

namespace Content.Server.NPC.Queries.Considerations;

/// <summary>
/// Returns 1f where the specified target is valid for the active hand's whitelist.
/// </summary>
public sealed partial class TargetAmmoMatchesCon : UtilityConsideration
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    private EntityWhitelistSystem _whitelistSystem = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _whitelistSystem = _entManager.System<EntityWhitelistSystem>();
    }

    public override float GetScore(NPCBlackboard blackboard, EntityUid targetUid, UtilityConsideration consideration)
    {
        if (!blackboard.TryGetValue(NPCBlackboard.ActiveHand, out Hand? activeHand, _entManager) ||
                    !_entManager.TryGetComponent<BallisticAmmoProviderComponent>(activeHand.HeldEntity, out var heldGun))
        {
            return 0f;
        }

        if (_whitelistSystem.IsWhitelistFailOrNull(heldGun.Whitelist, targetUid))
        {
            return 0f;
        }

        return 1f;
    }
}
