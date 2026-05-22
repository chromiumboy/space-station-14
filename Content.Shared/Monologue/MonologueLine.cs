using Content.Shared.Chat;

namespace Content.Shared.Monologue;

/// <summary>
/// A line that is spoken during a monologue.
/// </summary>
[Serializable, DataDefinition]
public sealed partial class MonologueLine
{
    /// <summary>
    /// The line that is spoken.
    /// </summary>
    [DataField]
    public string Line = string.Empty;

    /// <summary>
    /// Length of time to delay before speaking the next line of the monologue.
    /// </summary>
    [DataField("wait")]
    public TimeSpan Delay = TimeSpan.FromSeconds(3);

    /// <summary>
    /// A list of actions that will be executed when this line is spoken.
    /// </summary>
    [DataField("actions", serverOnly: true)]
    private IMonologueAction[] _actions = Array.Empty<IMonologueAction>();

    /// <summary>
    /// A list of actions that will be executed when this line is spoken.
    /// </summary>
    [ViewVariables]
    public IReadOnlyList<IMonologueAction> Actions => _actions;

    /// <summary>
    /// The type of chat message to send when this line is spoken.
    /// </summary>
    [DataField]
    public InGameICChatType ChatType = InGameICChatType.Speak;
}
