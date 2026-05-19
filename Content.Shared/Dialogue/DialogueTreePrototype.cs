using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using System.IO;

namespace Content.Shared.Dialogue;

[Prototype]
public sealed partial class DialogueTreePrototype : IPrototype, ISerializationHooks
{
    private readonly Dictionary<string, DialogueTreeNode> _nodes = new();

    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// The node that the dialogue will start on.
    /// Must be a valid node in the dialogue tree.
    /// </summary>
    [DataField("start", required: true)]
    public string? Start { get; private set; }

    /// <summary>
    /// A list of all dialogue tree nodes.
    /// </summary>
    [DataField("tree", priority: 0)]
    private List<DialogueTreeNode> _tree = new();

    /// <summary>
    /// Returns all dialogue tree nodes, indexed by their unique identifiers.
    /// </summary>
    [ViewVariables]
    public IReadOnlyDictionary<string, DialogueTreeNode> Nodes => _nodes;

    void ISerializationHooks.AfterDeserialization()
    {
        _nodes.Clear();

        foreach (var node in _tree)
        {
            if (string.IsNullOrEmpty(node.Name))
            {
                throw new InvalidDataException($"Name of node is null in dialogue tree {ID}!");
            }

            _nodes[node.Name] = node;
        }

        if (string.IsNullOrEmpty(Start) || !_nodes.ContainsKey(Start))
            throw new InvalidDataException($"Starting node for dialogue tree {ID} is null, empty or invalid!");
    }
}
