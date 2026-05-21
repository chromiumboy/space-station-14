using Content.Shared.Monologue;
using Robust.Server.Audio;
using Robust.Shared.Audio;

namespace Content.Server.Monologue;

[DataDefinition]
public sealed partial class MonologueSoundAction : IMonologueAction
{
    [DataField]
    public SoundSpecifier Sound { get; private set; }

    public void PerformAction(Entity<MonologueComponent> ent, IEntityManager entityManager)
    {
        var audio = entityManager.System<AudioSystem>();

        audio.PlayPvs(Sound, ent);
    }
}
