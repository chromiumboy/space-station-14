namespace Content.Shared.Dialogue;

/// <summary>
/// A node inside a dialogue tree. Contains the potential responses that user can make.
/// </summary>
[Serializable, DataDefinition]
public sealed partial class DialogueTreeNode
{
    [DataField("node", required: true)]
    public string Name { get; private set; } = default!;

    /// <summary>
    /// The displayed text.
    /// </summary>
    [DataField("text")]
    public string NodeText = "dialogue-tree-node-text-undefined";

    /// <summary>
    /// List of potential responses that can be made by the player.
    /// </summary>
    [DataField("responses")]
    public List<DialogueResponse> PotentialResponses = new();

    /// <summary>
    /// Entering this node will add the following keys to the dialogue owner.
    /// </summary>
    [DataField("addKeys")]
    public List<string> KeysAdded = new();

    /// <summary>
    /// Entering this node will remove the following keys from the dialogue owner.
    /// </summary>
    [DataField("removeKeys")]
    public List<string> KeysRemoved = new();

    /// <summary>
    /// A list of actions that will be executed when this node is entered.
    /// </summary>
    [DataField("actions", serverOnly: true)]
    private IDialogueAction[] _actions = Array.Empty<IDialogueAction>();

    /// <summary>
    /// A list of actions that will be executed when this node is entered.
    /// </summary>
    [ViewVariables]
    public IReadOnlyList<IDialogueAction> Actions => _actions;
}
