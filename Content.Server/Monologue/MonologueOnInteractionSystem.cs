using Content.Server.Monologue;
using Content.Shared.Interaction;

namespace Content.Shared.Monologue;

/// <summary>
/// System that start monologues when entities with <see cref="MonologueOnInteractionComponent"/> are interacted with.
/// </summary>
public sealed partial class MonologueOnInteractionSystem : EntitySystem
{
    [Dependency] private MonologueSystem _monologue = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MonologueOnInteractionComponent, ActivateInWorldEvent>(OnActivateInWorld);
    }

    private void OnActivateInWorld(Entity<MonologueOnInteractionComponent> ent, ref ActivateInWorldEvent args)
    {
        if (!TryComp<MonologueComponent>(ent, out var monologue) || !monologue.IsPaused)
            return;

        _monologue.StartMonologue((ent.Owner, monologue));
    }
}
