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
    /// The dialogue tree that will be used for handling the converstaion.
    /// Consists of a list of nodes, which are naviagated between via user responses.
    /// </summary>
    [DataField("tree", required: true)]
    public ProtoId<DialogueTreePrototype> CurrentTree { get; set; } = string.Empty;

    /// <summary>
    /// Keys that affect dialogue with all players.
    /// Can only be added via functions/events.
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
    /// The current node the conversation is on.
    /// Updated when the player selects a response.
    /// </summary>
    [DataField]
    public DialogueTreeNode? CurrentNode = null;

    /// <summary>
    /// The UI key for the dialogue window.
    /// </summary>
    [DataField]
    public DialogueUiKey UiKey = DialogueUiKey.BasicWindow;
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
