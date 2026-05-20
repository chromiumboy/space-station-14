namespace Content.Shared.Dialogue;

/// <summary>
/// A response that a user can select during a dialogue.
/// </summary>
[Serializable, DataDefinition]
public sealed partial class DialogueResponse
{
    /// <summary>
    /// The response text.
    /// </summary>
    [DataField("option")]
    public string ResponseText = "dialogue-tree-response-text-undefined";

    /// <summary>
    /// List of potential nodes the response may trigger.
    /// The dialogue system will cycle through the list
    /// and pick the first option to pass all tests.
    /// </summary>
    [DataField("to")]
    public string? NextNode;

    /// <summary>
    /// All of the following keys is required for this response to appear.
    /// </summary>
    [DataField("whitelist")]
    public List<string> RequiredKeys = new();

    /// <summary>
    /// Any of the following keys will hide this response.
    /// </summary>
    [DataField("blacklist")]
    public List<string> BlockingKeys = new();

    /// <summary>
    /// This response add the following keys to the dialogue owner.
    /// </summary>
    [DataField("addKeys")]
    public List<string> KeysAdded = new();

    /// <summary>
    /// This response removes the following keys from the dialogue owner.
    /// </summary>
    [DataField("removeKeys")]
    public List<string> KeysRemoved = new();
}
