using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Shared.NodeContainer;
using Content.Shared.Train.Track;

namespace Content.Server.Train.Track;

/// <inheritdoc/>
public sealed partial class TrainTrackSystem : SharedTrainTrackSystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TrainTrackComponent, NodeGroupsRebuilt>(OnRebuilt);
    }

    private void OnRebuilt(Entity<TrainTrackComponent> ent, ref NodeGroupsRebuilt args)
    {
        if (!TryComp<NodeContainerComponent>(ent, out var nodeContainer))
            return;

        foreach (var node in nodeContainer.Nodes)
        {
            if (node.Value is not TrainTrackNode track)
                continue;

            ent.Comp.Directions = track.CurrentDirections;
            ent.Comp.AdjacentTrack = track.AdjacentTrack;

            Dirty(ent);

            break;
        }
    }
}
