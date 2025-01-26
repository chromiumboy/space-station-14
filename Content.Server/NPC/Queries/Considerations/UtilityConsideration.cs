using Content.Server.NPC.Queries.Curves;
using JetBrains.Annotations;

namespace Content.Server.NPC.Queries.Considerations;

[ImplicitDataDefinitionForInheritors, MeansImplicitUse]
public abstract partial class UtilityConsideration
{
    [DataField(required: true)]
    public IUtilityCurve Curve = default!;

    /// <summary>
    /// Handles one-time initialization of this consideration.
    /// </summary>
    /// <param name="sysManager"></param>
    public virtual void Initialize(IEntitySystemManager sysManager)
    {
        IoCManager.InjectDependencies(this);
    }

    /// <summary>
    /// Has this consideration been met?
    /// </summary>
    public abstract float GetScore(NPCBlackboard blackboard, EntityUid targetUid, UtilityConsideration consideration);
}
