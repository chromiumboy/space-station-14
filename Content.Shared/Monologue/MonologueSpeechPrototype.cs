using Robust.Shared.Prototypes;

namespace Content.Shared.Monologue;

/// <summary>
/// Prototype for a monologue speech, a collection of lines spoken by an entity.
/// </summary>
[Prototype]
public sealed partial class MonologueSpeechPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// A list of all the lines that constitute the monolgue.
    /// </summary>
    [DataField("speech")]
    private List<MonologueLine> _speech = new();

    /// <summary>
    /// A list of all the lines that constitute the monolgue.
    /// </summary>
    [ViewVariables]
    public IReadOnlyList<MonologueLine> Speech => _speech;
}
