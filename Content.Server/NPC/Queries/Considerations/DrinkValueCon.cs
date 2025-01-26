using Content.Server.Nutrition.Components;
using Content.Server.Nutrition.EntitySystems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;

namespace Content.Server.NPC.Queries.Considerations;

public sealed partial class DrinkValueCon : UtilityConsideration
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    private OpenableSystem _openable = default!;
    private DrinkSystem _drink = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _openable = _entManager.System<OpenableSystem>();
        _drink = _entManager.System<DrinkSystem>();
    }

    public override float GetScore(NPCBlackboard blackboard, EntityUid targetUid, UtilityConsideration consideration)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entManager.TryGetComponent<DrinkComponent>(targetUid, out var drink))
            return 0f;

        // can't drink closed drinks
        if (_openable.IsClosed(targetUid))
            return 0f;

        // only drink when thirsty
        if (_entManager.TryGetComponent<ThirstComponent>(owner, out var thirst) && thirst.CurrentThirstThreshold > ThirstThreshold.Okay)
            return 0f;

        // no janicow don't drink the blood puddle
        if (_entManager.HasComponent<BadDrinkComponent>(targetUid))
            return 0f;

        // needs to have something that will satiate thirst, mice wont try to drink 100% pure mutagen.
        var hydration = _drink.TotalHydration(targetUid, drink);
        if (hydration <= 1.0f)
            return 0f;

        return 1f;
    }
}
