using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Server.NPC.Queries.Considerations;

public sealed partial class TargetAmmoCon : UtilityConsideration
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
    }

    public override float GetScore(NPCBlackboard blackboard, EntityUid targetUid, UtilityConsideration consideration)
    {
        if (!_entManager.HasComponent<GunComponent>(targetUid))
            return 0f;

        var ev = new GetAmmoCountEvent();
        _entManager.EventBus.RaiseLocalEvent(targetUid, ref ev);

        if (ev.Count == 0)
            return 0f;

        // Wat
        if (ev.Capacity == 0)
            return 1f;

        return (float)ev.Count / ev.Capacity;
    }
}
