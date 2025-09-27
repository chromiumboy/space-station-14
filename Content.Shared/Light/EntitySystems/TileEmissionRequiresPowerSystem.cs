using Content.Shared.Light.Components;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;

namespace Content.Shared.Light.EntitySystems;

/// <summary>
/// Turns tile emissions on/off depending on whether they are currently receiving power.
/// </summary>
public sealed partial class TileEmissionRequiresPowerSystem : EntitySystem
{
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TileEmissionRequiresPowerComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<TileEmissionRequiresPowerComponent, PowerChangedEvent>(OnPowerChanged);
    }

    private void OnStartup(Entity<TileEmissionRequiresPowerComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<TileEmissionComponent>(ent, out var tileEmission))
            return;

        UpdateTileEmission((ent.Owner, tileEmission), _power.IsPowered(ent.Owner));
    }

    private void OnPowerChanged(Entity<TileEmissionRequiresPowerComponent> ent, ref PowerChangedEvent args)
    {
        if (!TryComp<TileEmissionComponent>(ent, out var tileEmission))
            return;

        UpdateTileEmission((ent.Owner, tileEmission), args.Powered);
    }

    private void UpdateTileEmission(Entity<TileEmissionComponent> ent, bool enable)
    {
        if (ent.Comp.Enabled == enable)
            return;

        ent.Comp.Enabled = enable;
        Dirty(ent);
    }
}
