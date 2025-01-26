using Content.Server.Nutrition.Components;
using Content.Server.Nutrition.EntitySystems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;

namespace Content.Server.NPC.Queries.Considerations;

public sealed partial class FoodValueCon : UtilityConsideration
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    private OpenableSystem _openable = default!;
    private FoodSystem _food = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _openable = _entManager.System<OpenableSystem>();
        _food = _entManager.System<FoodSystem>();
    }

    public override float GetScore(NPCBlackboard blackboard, EntityUid targetUid, UtilityConsideration consideration)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entManager.TryGetComponent<FoodComponent>(targetUid, out var food))
            return 0f;

        // mice can't eat unpeeled bananas, need monkey's help
        if (_openable.IsClosed(targetUid))
            return 0f;

        if (!_food.IsDigestibleBy(owner, targetUid, food))
            return 0f;

        var avoidBadFood = !_entManager.HasComponent<IgnoreBadFoodComponent>(owner);

        // only eat when hungry or if it will eat anything
        if (_entManager.TryGetComponent<HungerComponent>(owner, out var hunger) && hunger.CurrentThreshold > HungerThreshold.Okay && avoidBadFood)
            return 0f;

        // no mouse don't eat the uranium-235
        if (avoidBadFood && _entManager.HasComponent<BadFoodComponent>(targetUid))
            return 0f;

        return 1f;
    }
}
