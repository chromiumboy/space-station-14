using Content.Shared.NodeContainer;
using Content.Shared.Train.Track;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Content.Server.NodeContainer.Nodes;

/// <summary>
/// Establishes connections between train tracks (<see cref="TrainTrackComponent"/>).
/// </summary>
[DataDefinition]
public sealed partial class TrainTrackNode : Node
{
    private SharedMapSystem _map;

    /// <summary>
    /// Directions in which this node can connect. The dictionary is indexed by the
    /// entry direction, and the value is the possible exits from that direction.
    /// </summary>
    [DataField("directions", required: true)]
    public Dictionary<Direction, Direction[]> OriginalDirections { get; private set; }

    /// <summary>
    /// The directions in <see cref="Directions"/> adjusted for entity rotation.
    /// </summary>
    /// <remarks>
    /// Used to populate <see cref="TrainTrackComponent.Directions"/>.
    /// </remarks>
    [ViewVariables]
    public Dictionary<Direction, Direction[]> CurrentDirections { get; private set; }

    /// <summary>
    /// The directions opposite to those of <see cref="CurrentDirections"/>.
    /// </summary>
    [ViewVariables]
    public Dictionary<Direction, Direction[]> OppositeDirections { get; private set; }

    /// <summary>
    /// The set of adjacent node owners that are currently connected to this one.
    /// </summary>
    /// <remarks>
    /// Used to populate <see cref="TrainTrackComponent.AdjacentTrack"/>.
    /// </remarks>
    [ViewVariables]
    public Dictionary<Direction, EntityUid> AdjacentTrack { get; private set; } = new();

    public override void Initialize(EntityUid owner, IEntityManager entityManager)
    {
        base.Initialize(owner, entityManager);

        IoCManager.InjectDependencies(this);
        _map = entityManager.System<SharedMapSystem>();

        var xform = entityManager.GetComponent<TransformComponent>(Owner);
        UpdateDirections(xform);
    }

    public override void OnAnchorStateChanged(IEntityManager entityManager, bool anchored)
    {
        if (!anchored)
            return;

        var xform = entityManager.GetComponent<TransformComponent>(Owner);
        UpdateDirections(xform);
    }

    private void UpdateDirections(TransformComponent xform)
    {
        CurrentDirections = GetRotatedDirections(OriginalDirections, xform);
        OppositeDirections = GetOppositeDirections(CurrentDirections, xform);
    }

    public override IEnumerable<Node> GetReachableNodes(TransformComponent xform,
        EntityQuery<NodeContainerComponent> nodeQuery,
        EntityQuery<TransformComponent> xformQuery,
        MapGridComponent? grid,
        IEntityManager entMan)
    {
        AdjacentTrack.Clear();

        if (!xform.Anchored || xform.GridUid == null || grid == null)
            yield break;

        foreach (var direction in OppositeDirections.Keys)
        {
            foreach (var entity in _map.GetInDir(xform.GridUid.Value, grid, xform.Coordinates, direction))
            {
                if (!nodeQuery.TryGetComponent(entity, out var container))
                    continue;

                if (TryToFindAConnectingNode(container, direction, out var node))
                {
                    yield return node;

                    AdjacentTrack.Add(direction, node.Owner);

                    break;
                }
            }
        }
    }

    private bool TryToFindAConnectingNode(NodeContainerComponent container, Direction direction, [NotNullWhen(true)] out TrainTrackNode? foundNode)
    {
        foundNode = null;

        foreach (var node in container.Nodes.Values)
        {
            if (node is not TrainTrackNode disposalNode)
                continue;

            if (disposalNode.CurrentDirections.ContainsKey(direction))
            {
                foundNode = disposalNode;
                return true;
            }
        }

        return false;
    }

    private Dictionary<Direction, Direction[]> GetRotatedDirections(Dictionary<Direction, Direction[]> directions, TransformComponent xform)
    {
        var rotated = new Dictionary<Direction, Direction[]>(directions.Count);

        foreach (var kvp in directions)
        {
            rotated.Add
                ((kvp.Key.ToAngle() + xform.LocalRotation).GetDir(),
                kvp.Value.Select(x => (x.ToAngle() + xform.LocalRotation).GetDir()).ToArray());
        }

        return rotated;
    }

    private Dictionary<Direction, Direction[]> GetOppositeDirections(Dictionary<Direction, Direction[]> directions, TransformComponent xform)
    {
        var opposite = new Dictionary<Direction, Direction[]>(directions.Count);

        foreach (var kvp in directions)
        {
            opposite.Add(kvp.Key.GetOpposite(), kvp.Value.Select(x => x.GetOpposite()).ToArray());
        }

        return opposite;
    }
}
