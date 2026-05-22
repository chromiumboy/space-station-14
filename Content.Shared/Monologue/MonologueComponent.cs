using Robust.Shared.Prototypes;

namespace Content.Shared.Monologue;

/// <summary>
/// Attached to entities that can give pre-scripted speeches.
/// The data in this component is only handled by the server.
/// </summary>
[RegisterComponent]
[Access(typeof(SharedMonologueSystem))]
public sealed partial class MonologueComponent : Component
{
    /// <summary>
    /// The speech given by the monologer.
    /// </summary>
    [DataField("speech")]
    public ProtoId<MonologueSpeechPrototype> CurrentSpeech = string.Empty;

    /// <summary>
    /// Timeline of monologue lines with their associated time offsets.
    /// </summary>
    [DataField]
    public List<(TimeSpan, MonologueLine)> Timeline = new();

    /// <summary>
    /// Indicates whether the monologue is currently paused.
    /// When true, the timeline will not advance and no lines will be announced.
    /// </summary>
    [DataField]
    public bool IsPaused = true;

    /// <summary>
    /// The amount of unpaused time that has elapsed since the start of the monologue.
    /// </summary>
    [DataField]
    public TimeSpan TimeElapsed = TimeSpan.FromSeconds(0);

    /// <summary>
    /// Indicates whether the lines spoken by this monologuer should be visible in the chat window.
    /// </summary>
    [DataField]
    public bool HideFromChat;
}
