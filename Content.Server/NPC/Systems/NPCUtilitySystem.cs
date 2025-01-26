using Content.Server.Fluids.EntitySystems;
using Content.Server.NPC.Queries;
using Content.Server.NPC.Queries.Curves;
using Content.Server.NPC.Queries.Queries;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Fluids.Components;
using Content.Shared.Inventory;
using Content.Shared.NPC.Systems;
using Microsoft.Extensions.ObjectPool;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using System.Linq;

namespace Content.Server.NPC.Systems;

/// <summary>
/// Handles utility queries for NPCs.
/// </summary>
public sealed class NPCUtilitySystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly PuddleSystem _puddle = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;

    private EntityQuery<PuddleComponent> _puddleQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    private ObjectPool<HashSet<EntityUid>> _entPool =
        new DefaultObjectPool<HashSet<EntityUid>>(new SetPolicy<EntityUid>(), 256);

    // Temporary caches.
    private List<EntityUid> _entityList = new();
    private HashSet<Entity<IComponent>> _entitySet = new();
    private List<EntityPrototype.ComponentRegistryEntry> _compTypes = new();

    public override void Initialize()
    {
        base.Initialize();

        _puddleQuery = GetEntityQuery<PuddleComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();

        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypeLoad);

        OnLoad();
    }

    private void OnLoad()
    {
        // Add dependencies for all consideration (for parity with preconditions).
        foreach (var proto in _proto.EnumeratePrototypes<UtilityQueryPrototype>())
        {
            UpdateUtilityQuery(proto);
        }
    }

    private void OnPrototypeLoad(PrototypesReloadedEventArgs obj)
    {
        OnLoad();
    }

    private void UpdateUtilityQuery(UtilityQueryPrototype utilityQuery)
    {
        foreach (var con in utilityQuery.Considerations)
        {
            con.Initialize(EntityManager.EntitySysManager);
        }
    }

    /// <summary>
    /// Runs the UtilityQueryPrototype and returns the best-matching entities.
    /// </summary>
    /// <param name="bestOnly">Should we only return the entity with the best score.</param>
    public UtilityResult GetEntities(
        NPCBlackboard blackboard,
        string proto,
        bool bestOnly = true)
    {
        // TODO: PickHostilesop or whatever needs to juse be UtilityQueryOperator

        var weh = _proto.Index<UtilityQueryPrototype>(proto);
        var ents = _entPool.Get();

        foreach (var query in weh.Query)
        {
            switch (query)
            {
                case UtilityQueryFilter filter:
                    Filter(blackboard, ents, filter);
                    break;
                default:
                    Add(blackboard, ents, query);
                    break;
            }
        }

        if (ents.Count == 0)
        {
            _entPool.Return(ents);
            return UtilityResult.Empty;
        }

        foreach (var con in weh.Considerations)
        {
            con.Initialize(EntityManager.EntitySysManager);
        }

        var results = new Dictionary<EntityUid, float>();
        var highestScore = 0f;

        foreach (var ent in ents)
        {
            if (results.Count > weh.Limit)
                break;

            var score = 1f;

            foreach (var con in weh.Considerations)
            {
                var conScore = con.GetScore(blackboard, ent, con);
                var curve = con.Curve;
                var curveScore = GetScore(curve, conScore);

                var adjusted = GetAdjustedScore(curveScore, weh.Considerations.Count);
                score *= adjusted;

                // If the score is too low OR we only care about best entity then early out.
                // Due to the adjusted score only being able to decrease it can never exceed the highest from here.
                if (score <= 0f || bestOnly && score <= highestScore)
                {
                    break;
                }
            }

            if (score <= 0f)
                continue;

            highestScore = MathF.Max(score, highestScore);
            results.Add(ent, score);
        }

        var result = new UtilityResult(results);
        blackboard.Remove<EntityUid>(NPCBlackboard.UtilityTarget);
        _entPool.Return(ents);
        return result;
    }

    private float GetScore(IUtilityCurve curve, float conScore)
    {
        switch (curve)
        {
            case BoolCurve:
                return conScore > 0f ? 1f : 0f;
            case InverseBoolCurve:
                return conScore.Equals(0f) ? 1f : 0f;
            case PresetCurve presetCurve:
                return GetScore(_proto.Index<UtilityCurvePresetPrototype>(presetCurve.Preset).Curve, conScore);
            case QuadraticCurve quadraticCurve:
                return Math.Clamp(quadraticCurve.Slope * MathF.Pow(conScore - quadraticCurve.XOffset, quadraticCurve.Exponent) + quadraticCurve.YOffset, 0f, 1f);
            default:
                throw new NotImplementedException();
        }
    }

    private float GetAdjustedScore(float score, int considerations)
    {
        /*
        * Now using the geometric mean
        * for n scores you take the n-th root of the scores multiplied
        * e.g. a, b, c scores you take Math.Pow(a * b * c, 1/3)
        * To get the ACTUAL geometric mean at any one stage you'd need to divide by the running consideration count
        * however, the downside to this is it will fluctuate up and down over time.
        * For our purposes if we go below the minimum threshold we want to cut it off, thus we take a
        * "running geometric mean" which can only ever go down (and by the final value will equal the actual geometric mean).
        */

        var adjusted = MathF.Pow(score, 1 / (float) considerations);
        return Math.Clamp(adjusted, 0f, 1f);
    }

    private void Add(NPCBlackboard blackboard, HashSet<EntityUid> entities, UtilityQuery query)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var vision = blackboard.GetValueOrDefault<float>(blackboard.GetVisionRadiusKey(EntityManager), EntityManager);

        switch (query)
        {
            case ComponentQuery compQuery:
            {
                if (compQuery.Components.Count == 0)
                    return;

                var mapPos = _transform.GetMapCoordinates(owner, xform: _xformQuery.GetComponent(owner));
                _compTypes.Clear();
                var i = -1;
                EntityPrototype.ComponentRegistryEntry compZero = default!;

                foreach (var compType in compQuery.Components.Values)
                {
                    i++;

                    if (i == 0)
                    {
                        compZero = compType;
                        continue;
                    }

                    _compTypes.Add(compType);
                }

                _entitySet.Clear();
                _lookup.GetEntitiesInRange(compZero.Component.GetType(), mapPos, vision, _entitySet);

                foreach (var comp in _entitySet)
                {
                    var ent = comp.Owner;

                    if (ent == owner)
                        continue;

                    var othersFound = true;

                    foreach (var compOther in _compTypes)
                    {
                        if (!HasComp(ent, compOther.Component.GetType()))
                        {
                            othersFound = false;
                            break;
                        }
                    }

                    if (!othersFound)
                        continue;

                    entities.Add(ent);
                }

                break;
            }
            case InventoryQuery:
            {
                if (!_inventory.TryGetContainerSlotEnumerator(owner, out var enumerator))
                    break;

                while (enumerator.MoveNext(out var slot))
                {
                    foreach (var child in slot.ContainedEntities)
                    {
                        RecursiveAdd(child, entities);
                    }
                }

                break;
            }
            case NearbyHostilesQuery:
            {
                foreach (var ent in _npcFaction.GetNearbyHostiles(owner, vision))
                {
                    entities.Add(ent);
                }
                break;
            }
            default:
                throw new NotImplementedException();
        }
    }

    private void RecursiveAdd(EntityUid uid, HashSet<EntityUid> entities)
    {
        // TODO: Probably need a recursive struct enumerator on engine.
        var xform = _xformQuery.GetComponent(uid);
        var enumerator = xform.ChildEnumerator;
        entities.Add(uid);

        while (enumerator.MoveNext(out var child))
        {
            RecursiveAdd(child, entities);
        }
    }

    private void Filter(NPCBlackboard blackboard, HashSet<EntityUid> entities, UtilityQueryFilter filter)
    {
        switch (filter)
        {
            case ComponentFilter compFilter:
            {
                _entityList.Clear();

                foreach (var ent in entities)
                {
                    foreach (var comp in compFilter.Components)
                    {
                        if (HasComp(ent, comp.Value.Component.GetType()))
                            continue;

                        _entityList.Add(ent);
                        break;
                    }
                }

                foreach (var ent in _entityList)
                {
                    entities.Remove(ent);
                }

                break;
            }
            case RemoveAnchoredFilter:
            {
                _entityList.Clear();

                foreach (var ent in entities)
                {
                    if (!TryComp(ent, out TransformComponent? xform))
                        continue;

                    if (xform.Anchored)
                        _entityList.Add(ent);
                }

                foreach (var ent in _entityList)
                {
                    entities.Remove(ent);
                }

                break;
            }
            case PuddleFilter:
            {
                _entityList.Clear();

                foreach (var ent in entities)
                {
                    if (!_puddleQuery.TryGetComponent(ent, out var puddleComp) ||
                        !_solutions.TryGetSolution(ent, puddleComp.SolutionName, out _, out var sol) ||
                        _puddle.CanFullyEvaporate(sol))
                    {
                        _entityList.Add(ent);
                    }
                }

                foreach (var ent in _entityList)
                {
                    entities.Remove(ent);
                }

                break;
            }
            default:
                throw new NotImplementedException();
        }
    }
}

public readonly record struct UtilityResult(Dictionary<EntityUid, float> Entities)
{
    public static readonly UtilityResult Empty = new(new Dictionary<EntityUid, float>());

    public readonly Dictionary<EntityUid, float> Entities = Entities;

    /// <summary>
    /// Returns the entity with the highest score.
    /// </summary>
    public EntityUid GetHighest()
    {
        if (Entities.Count == 0)
            return EntityUid.Invalid;

        return Entities.MaxBy(x => x.Value).Key;
    }

    /// <summary>
    /// Returns the entity with the lowest score. This does not consider entities with a 0 (invalid) score.
    /// </summary>
    public EntityUid GetLowest()
    {
        if (Entities.Count == 0)
            return EntityUid.Invalid;

        return Entities.MinBy(x => x.Value).Key;
    }
}
