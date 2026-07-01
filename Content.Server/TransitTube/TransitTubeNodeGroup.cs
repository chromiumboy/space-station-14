using Content.Server.NodeContainer.NodeGroups;
using Content.Shared.NodeContainer.NodeGroups;

namespace Content.Server.TransitTube;

/// <summary>
/// Dummy node group for transit tubes.
/// </summary>
[NodeGroup(NodeGroupID.TransitTube)]
public sealed class TransitTubeNodeGroup : BaseNodeGroup;
