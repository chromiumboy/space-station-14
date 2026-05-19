using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Dialogue;

/// <summary>
/// Attached to entities that can be interacted with to start a dialogue.
/// </summary>
[RegisterComponent]
[Access(typeof(DialogueSystem))]
public sealed partial class DialogueComponent : Component
{
    /// <summary>
    /// Keys that affect dialogue with all players. 
    /// Can only be added via events.
    /// </summary>
    [DataField]
    public HashSet<DialogueKeyPrototype> UniversalKeys = new();

    /// <summary>
    /// Keys that affect dialogue specific players.
    /// Can be added by entering certain dialogue
    /// nodes or selecting certain responses.
    /// </summary>
    [DataField]
    public Dictionary<EntityUid, HashSet<DialogueKeyPrototype>> UserKeys = new();

    /// <summary>
    /// A list of potential nodes to start a dialogue.
    /// The dialogue system will cycle through the list
    /// and pick the first option to pass all tests.
    /// </summary>
    [DataField]
    public List<DialogueNode> PotentialStartNodes = new();

    /// <summary>
    /// The current node the conversation is on.
    /// Updated when the player selects a response.
    /// </summary>
    [DataField]
    public DialogueNode? CurrentNode = null;

    /// <summary>
    /// If no start node is valid, this text is used to end the dialogue.
    /// </summary>
    [DataField]
    public string DefaultNodeText;

    /// <summary>
    /// If no start node is valid, this response is used to end the dialogue.  
    /// </summary>
    [DataField]
    public string DefaultResponseText;
}

/// <summary>
/// A node inside a dialogue tree. Contains the potential responses that user can make.
/// </summary>
[Serializable, NetSerializable]
public sealed class DialogueNode
{
    /// <summary>
    /// The displayed text.
    /// </summary>
    public string NodeText = "dialogue-string-undefined";

    /// <summary>
    /// List of potential responses that can be made by the player.
    /// </summary>
    public List<DialogueResponse> PotentialResponses = new();

    /// <summary>
    /// Having all of the following keys is required for this node to be valid.
    /// </summary>
    public List<DialogueKeyPrototype> RequiredKeys = new();

    /// <summary>
    /// Having any of the following keys will mark this node as invalid.
    /// </summary>
    public List<DialogueKeyPrototype> BlockingKeys = new();

    /// <summary>
    /// Entering this node will add the following keys to the dialogue owner.
    /// </summary>
    public List<DialogueKeyPrototype> KeysAdded = new();

    /// <summary>
    /// Entering this node will remove the following keys from the dialogue owner.
    /// </summary>
    public List<DialogueKeyPrototype> KeysRemoved = new();
}

/// <summary>
/// A response that a user can select during a dialogue.
/// </summary>
[Serializable, NetSerializable]
public sealed class DialogueResponse
{
    /// <summary>
    /// The response text.
    /// </summary>
    public string ResponseText = "dialogue-string-undefined";

    /// <summary>
    /// List of potential nodes the response may trigger.
    /// The dialogue system will cycle through the list
    /// and pick the first option to pass all tests.
    /// </summary>
    public List<DialogueNode> PotentialNodes = new();

    /// <summary>
    /// Having all of the following keys is required for this response to be valid.
    /// </summary>
    public List<DialogueKeyPrototype> RequiredKeys = new();

    /// <summary>
    /// Having any of the following keys will mark this response as invalid.
    /// </summary>
    public List<DialogueKeyPrototype> BlockingKeys = new();

    /// <summary>
    /// This response add the following keys to the dialogue owner.
    /// </summary>
    public List<DialogueKeyPrototype> KeysAdded = new();

    /// <summary>
    /// This response removes the following keys from the dialogue owner.
    /// </summary>
    public List<DialogueKeyPrototype> KeysRemoved = new();
}

[Prototype]
public sealed partial class DialogueKeyPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;
}

/// <summary>
/// Data for the dialogue UI, from by the server to the client 
/// </summary>
[Serializable, NetSerializable]
public sealed class DialogueBoundInterfaceState : BoundUserInterfaceState
{
    public readonly string NodeText;
    public readonly Dictionary<int, string> Responses;

    public DialogueBoundInterfaceState(string nodeText, Dictionary<int, string> responses)
    {
        NodeText = nodeText;
        Responses = responses;
    }
}

/// <summary>
/// A user interface message that indicates which dialogue response was selected by the user.
/// </summary>
[Serializable, NetSerializable]
public sealed class DialogueResponseSelectionMessage : BoundUserInterfaceMessage
{
    public int ResponseIndex;

    public DialogueResponseSelectionMessage(int responseIndex)
    {
        ResponseIndex = responseIndex;
    }
}

/// <summary>
/// Key to the dialogue UI
/// </summary>
[Serializable, NetSerializable]
public enum DialogueUiKey : byte
{
    BasicWindow,
}
