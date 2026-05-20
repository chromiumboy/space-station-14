using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Dialogue;

/// <summary>
/// Attached to entities that can be interacted with to start a dialogue.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(DialogueSystem))]
public sealed partial class DialogueComponent : Component
{
    /// <summary>
    /// The dialogue tree that will be used for handling the conversation.
    /// Consists of a list of nodes, which are naviagated between via user responses.
    /// </summary>
    [DataField("tree", required: true), AutoNetworkedField]
    public ProtoId<DialogueTreePrototype> CurrentTree { get; set; } = string.Empty;

    /// <summary>
    /// Keys that affect dialogue with all players.
    /// Can only be added via functions/events.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<string> UniversalKeys = new();

    /// <summary>
    /// Keys that affect dialogue specific players.
    /// Can be added by entering certain dialogue
    /// nodes or selecting certain responses.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<EntityUid, HashSet<string>> UserKeys = new();

    /// <summary>
    /// The current node user conversations are on.
    /// Updated when users selects a response.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<EntityUid, string> UserCurrentNodes = new();

    /// <summary>
    /// The UI key for the dialogue window.
    /// </summary>
    [DataField, AutoNetworkedField]
    public DialogueUiKey UiKey = DialogueUiKey.BasicWindow;
}

/// <summary>
/// Data for the dialogue UI, from by the server to the client 
/// </summary>
[Serializable, NetSerializable]
public sealed class DialogueBasicWindowBoundInterfaceState : BoundUserInterfaceState
{
    public readonly string NodeText;
    public readonly Dictionary<int, string> Responses;

    public DialogueBasicWindowBoundInterfaceState(string nodeText, Dictionary<int, string> responses)
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
