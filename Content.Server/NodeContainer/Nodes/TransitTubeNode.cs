using Content.Shared.NodeContainer;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Content.Server.NodeContainer.Nodes;

/// <summary>
/// Establishes connections between disposal tubes.
/// </summary>
[DataDefinition]
public sealed partial class TransitTubeNode : Node
{
    private SharedMapSystem _map;

    /// <summary>
    /// Directions in which this node can connect to adjacent ones.
    /// </summary>
    [DataField("directions")]
    public Direction[] OriginalDirections = { Direction.South };

    /// <summary>
    /// Directions that this node can connect after accounting for the rotation of the entity.
    /// </summary>
    [ViewVariables]
    public Direction[] CurrentDirections { get; private set; }

    /// <summary>
    /// Directions opposite to those of <see cref="CurrentDirections"/>.
    /// </summary>
    [ViewVariables]
    public Direction[] OppositeDirections { get; private set; }

    /// <summary>
    /// Set of nodes that are currently connected to this one.
    /// </summary>
    [ViewVariables]
    public HashSet<TransitTubeNode> AdjacentNodes { get; private set; } = new();

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
        AdjacentNodes.Clear();

        if (!xform.Anchored || xform.GridUid == null || grid == null)
            yield break;

        foreach (var direction in CurrentDirections)
        {
            foreach (var entity in _map.GetInDir(xform.GridUid.Value, grid, xform.Coordinates, direction))
            {
                if (!nodeQuery.TryGetComponent(entity, out var container))
                    continue;

                if (TryToFindAConnectingNode(container, direction, out var node))
                {
                    yield return node;

                    AdjacentNodes.Add(node);

                    break;
                }
            }
        }
    }

    private bool TryToFindAConnectingNode(NodeContainerComponent container, Direction direction, [NotNullWhen(true)] out TransitTubeNode? foundNode)
    {
        foundNode = null;

        foreach (var node in container.Nodes.Values)
        {
            if (node is not TransitTubeNode disposalNode)
                continue;

            if (disposalNode.OppositeDirections.Contains(direction))
            {
                foundNode = disposalNode;
                return true;
            }
        }

        return false;
    }

    private Direction[] GetRotatedDirections(Direction[] directions, TransformComponent xform)
    {
        return directions.Select(x => (x.ToAngle() + xform.LocalRotation).GetDir()).ToArray();
    }

    private Direction[] GetOppositeDirections(Direction[] directions, TransformComponent xform)
    {
        return directions.Select(x => x.GetOpposite()).ToArray();
    }
}
