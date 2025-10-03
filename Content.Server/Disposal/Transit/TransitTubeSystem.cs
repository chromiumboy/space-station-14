using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Shared.Light.Components;
using Content.Shared.NodeContainer;
using System.Diagnostics.CodeAnalysis;

namespace Content.Server.Disposal.Transit;

public sealed partial class TransitTubeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TransitTubeComponent, NodeGroupsRebuilt>(OnNodeGroupsRebuilt);
    }

    private void OnNodeGroupsRebuilt(Entity<TransitTubeComponent> ent, ref NodeGroupsRebuilt args)
    {
        if (!TryComp<TileEmissionComponent>(ent, out var tileEmission))
            return;

        if (!TryGetNode(ent, out var node))
            return;

        var connectionsMissing = node.AdjacentNodes.Count < node.OriginalDirections.Length;

        tileEmission.Color = connectionsMissing ? ent.Comp.WarningLightingColor : ent.Comp.NormalLightingColor;
        Dirty(ent, tileEmission);
    }

    private bool TryGetNode(Entity<TransitTubeComponent> ent, [NotNullWhen(true)] out TransitTubeNode? foundNode)
    {
        foundNode = null;

        if (!TryComp<NodeContainerComponent>(ent, out var nodeContainer))
            return false;

        foreach (var node in nodeContainer.Nodes)
        {
            if (node.Value is TransitTubeNode transitTubeNode)
            {
                foundNode = transitTubeNode;
                return true;
            }
        }

        return false;
    }
}
