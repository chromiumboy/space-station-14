using Content.Server.NPC.HTN;
using Content.Shared.Destructible;
using Content.Shared.Repairable;
using Robust.Server.GameObjects;

namespace Content.Server.Repairable;

public sealed class RepairableTurretSystem : SharedRepairableSystem
{
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<RepairableTurretComponent, BreakageEventArgs>(OnBroken);
        SubscribeLocalEvent<RepairableTurretComponent, RepairedEvent>(OnRepaired);
    }

    private void OnBroken(Entity<RepairableTurretComponent> ent, ref BreakageEventArgs args)
    {
        if (TryComp<HTNComponent>(ent, out var htn))
            _htn.SetHTNEnabled((ent, htn), false);

        if (TryComp<AppearanceComponent>(ent, out var appearance))
            _appearance.SetData(ent, RepairableTurretVisuals.Broken, true, appearance);
    }

    private void OnRepaired(Entity<RepairableTurretComponent> ent, ref RepairedEvent args)
    {
        if (TryComp<HTNComponent>(ent, out var htn))
            _htn.SetHTNEnabled((ent, htn), true, 0.5f);

        if (TryComp<AppearanceComponent>(ent, out var appearance))
            _appearance.SetData(ent, RepairableTurretVisuals.Broken, false, appearance);
    }
}
